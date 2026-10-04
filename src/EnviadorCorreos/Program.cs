using Inventario.Core.Comun;
using Inventario.Datos;
using Inventario.EnviadorCorreos;
using Microsoft.EntityFrameworkCore;

// Proceso independiente de la API (RF-NOT-09).
// Uso:  dotnet run --project src/EnviadorCorreos              -> procesa los pendientes una vez y termina
//       dotnet run --project src/EnviadorCorreos -- --continuo -> revisa la cola cada 15 segundos

var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(cadena))
{
    Console.Error.WriteLine("Falta la variable de entorno ConnectionStrings__Default.");
    return 1;
}

var smtp = ConfiguracionSmtp.DesdeEntorno(out var error);
if (smtp is null)
{
    Console.Error.WriteLine(error);
    return 1;
}

var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cadena).Options;
var continuo = args.Contains("--continuo");
IReloj reloj = new RelojSistema();

do
{
    await using (var db = new AppDbContext(opciones))
    {
        var procesador = new ProcesadorCola(db, smtp, reloj);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Revisando la cola de correos...");
        var (enviados, conError) = await procesador.ProcesarAsync();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Enviados: {enviados}. Con error (siguen pendientes): {conError}.");
    }

    if (continuo)
        await Task.Delay(TimeSpan.FromSeconds(15));
} while (continuo);

return 0;
