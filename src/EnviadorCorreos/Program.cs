using Inventario.Core.Comun;
using Inventario.Datos;
using Inventario.EnviadorCorreos;
using Microsoft.EntityFrameworkCore;

// Proceso independiente de la API (RF-NOT-09).
// Uso:  dotnet run --project src/EnviadorCorreos              -> procesa los pendientes una vez y termina
//       dotnet run --project src/EnviadorCorreos -- --continuo -> revisa la cola cada 15 segundos (así corre en Docker)
// CORREO_MODO=smtp (por defecto) envía por SMTP; CORREO_MODO=consola escribe cada correo en esta salida.

var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(cadena))
{
    Console.Error.WriteLine("Falta la variable de entorno ConnectionStrings__Default.");
    return 1;
}

var modo = (Environment.GetEnvironmentVariable("CORREO_MODO") ?? "smtp").Trim().ToLowerInvariant();
IEnvioCorreo envio;
if (modo == "consola")
{
    envio = new EnvioConsola();
    Console.WriteLine("CORREO_MODO=consola: los correos se escriben aquí (docker compose logs enviador).");
}
else if (modo == "smtp")
{
    var smtp = ConfiguracionSmtp.DesdeEntorno(out var error);
    if (smtp is null)
    {
        Console.Error.WriteLine(error);
        return 1;
    }
    envio = new EnvioSmtp(smtp);
}
else
{
    Console.Error.WriteLine("CORREO_MODO debe ser «smtp» o «consola».");
    return 1;
}

var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cadena).Options;
var continuo = args.Contains("--continuo");
IReloj reloj = new RelojSistema();

do
{
    try
    {
        await using var db = new AppDbContext(opciones);
        var procesador = new ProcesadorCola(db, envio, reloj);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Revisando la cola de correos...");
        var (enviados, conError) = await procesador.ProcesarAsync();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Enviados: {enviados}. Con error (siguen pendientes): {conError}.");
    }
    catch (Exception ex) when (continuo)
    {
        // En modo continuo no se detiene: por ejemplo, la base todavía arranca o la API aún no crea las tablas.
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] No se pudo revisar la cola ({ex.GetType().Name}). Se reintenta en 15 s.");
    }

    if (continuo)
        await Task.Delay(TimeSpan.FromSeconds(15));
} while (continuo);

return 0;
