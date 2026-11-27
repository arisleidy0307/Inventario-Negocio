using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Datos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.EnviadorCorreos;

/// <summary>Resultado de una pasada por la cola.</summary>
public record ResultadoProcesamiento(int Enviados, int Reintentaran, int Fallidos);

/// <summary>
/// Toma los correos pendientes, los envía (por SMTP o a la consola, según CORREO_MODO) y registra el resultado
/// (RF-NOT-09, RF-NOT-11).
/// RF-NOT-12: cada fila se reclama con un UPDATE condicionado a Estado = 'Pendiente' antes de enviarla,
/// así dos ejecuciones (seguidas o simultáneas) nunca envían el mismo correo dos veces.
/// RF-NOT-10: si el envío falla, vuelve a Pendiente para otro intento; al llegar a <c>maxIntentos</c> queda
/// Fallido con su último error y ya no se vuelve a intentar.
/// </summary>
public class ProcesadorCola
{
    /// <summary>Máximo de intentos por defecto (CORREO_MAX_INTENTOS).</summary>
    public const int MaxIntentosPorDefecto = 3;

    private readonly AppDbContext _db;
    private readonly IEnvioCorreo _envio;
    private readonly IReloj _reloj;
    private readonly int _maxIntentos;

    public ProcesadorCola(AppDbContext db, IEnvioCorreo envio, IReloj reloj, int maxIntentos = MaxIntentosPorDefecto)
    {
        _db = db;
        _envio = envio;
        _reloj = reloj;
        _maxIntentos = maxIntentos > 0 ? maxIntentos : MaxIntentosPorDefecto;
    }

    public async Task<ResultadoProcesamiento> ProcesarAsync(int maximo = 50)
    {
        var ids = await _db.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente && c.Intentos < _maxIntentos)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(maximo)
            .ToListAsync();

        int enviados = 0, reintentaran = 0, fallidos = 0;
        foreach (var id in ids)
        {
            var ahora = _reloj.UtcNow;

            // Se reclama la fila antes de enviar: si otra ejecución ya la tomó, este UPDATE no afecta ninguna.
            var reclamado = await _db.CorreosEnCola
                .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadoCorreo.Enviado)
                    .SetProperty(c => c.FechaEnvio, ahora)
                    .SetProperty(c => c.FechaUltimoIntento, ahora)
                    .SetProperty(c => c.Intentos, c => c.Intentos + 1));

            if (reclamado == 0)
                continue; // otra ejecución ya lo tomó

            var correo = await _db.CorreosEnCola.AsNoTracking().FirstAsync(c => c.Id == id);
            try
            {
                await _envio.EnviarAsync(correo);
                // Si un intento anterior falló, se limpia el error: el correo ya salió.
                await _db.CorreosEnCola.Where(c => c.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UltimoError, (string?)null));
                enviados++;
                Console.WriteLine($"  [enviado] #{correo.Id} -> {correo.Destinatario}: {correo.Asunto}");
            }
            catch (Exception ex)
            {
                var mensaje = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                var agotado = correo.Intentos >= _maxIntentos;
                var estado = agotado ? EstadoCorreo.Fallido : EstadoCorreo.Pendiente;

                await _db.CorreosEnCola
                    .Where(c => c.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Estado, estado)
                        .SetProperty(c => c.FechaEnvio, (DateTime?)null)
                        .SetProperty(c => c.UltimoError, mensaje));

                if (agotado)
                {
                    fallidos++;
                    Console.WriteLine($"  [fallido] #{correo.Id} -> {correo.Destinatario}: intento {correo.Intentos} de {_maxIntentos}. {mensaje}");
                }
                else
                {
                    reintentaran++;
                    Console.WriteLine($"  [error]   #{correo.Id} -> {correo.Destinatario}: intento {correo.Intentos} de {_maxIntentos}, se reintenta. {mensaje}");
                }
            }
        }

        return new ResultadoProcesamiento(enviados, reintentaran, fallidos);
    }
}
