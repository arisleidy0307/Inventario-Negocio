using System.Net;
using System.Net.Mail;
using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Datos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.EnviadorCorreos;

/// <summary>
/// Toma los correos pendientes, los envía por SMTP y los marca como enviados (RF-NOT-09).
/// RF-NOT-12: cada fila se reclama con un UPDATE condicionado a Estado = 'Pendiente' antes de enviarla,
/// así dos ejecuciones (seguidas o simultáneas) nunca envían el mismo correo dos veces.
/// </summary>
public class ProcesadorCola
{
    private readonly AppDbContext _db;
    private readonly ConfiguracionSmtp _smtp;
    private readonly IReloj _reloj;

    public ProcesadorCola(AppDbContext db, ConfiguracionSmtp smtp, IReloj reloj)
    {
        _db = db;
        _smtp = smtp;
        _reloj = reloj;
    }

    public async Task<(int Enviados, int ConError)> ProcesarAsync(int maximo = 50)
    {
        var ids = await _db.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(maximo)
            .ToListAsync();

        int enviados = 0, conError = 0;
        foreach (var id in ids)
        {
            var ahora = _reloj.UtcNow;
            var reclamado = await _db.CorreosEnCola
                .Where(c => c.Id == id && c.Estado == EstadoCorreo.Pendiente)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Estado, EstadoCorreo.Enviado)
                    .SetProperty(c => c.FechaEnvio, ahora)
                    .SetProperty(c => c.Intentos, c => c.Intentos + 1));

            if (reclamado == 0)
                continue; // otra ejecución ya lo tomó

            var correo = await _db.CorreosEnCola.AsNoTracking().FirstAsync(c => c.Id == id);
            try
            {
                await EnviarAsync(correo);
                enviados++;
                Console.WriteLine($"  [enviado] #{correo.Id} -> {correo.Destinatario}: {correo.Asunto}");
            }
            catch (Exception ex)
            {
                // Vuelve a pendiente para un próximo intento y guarda el último error.
                var mensaje = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                await _db.CorreosEnCola
                    .Where(c => c.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Estado, EstadoCorreo.Pendiente)
                        .SetProperty(c => c.FechaEnvio, (DateTime?)null)
                        .SetProperty(c => c.UltimoError, mensaje));
                conError++;
                Console.WriteLine($"  [error]   #{correo.Id} -> {correo.Destinatario}: {mensaje}");
            }
        }

        return (enviados, conError);
    }

    private async Task EnviarAsync(CorreoEnCola correo)
    {
        using var mensaje = new MailMessage(_smtp.Remitente, correo.Destinatario, correo.Asunto, correo.Cuerpo)
        {
            IsBodyHtml = false
        };
        using var cliente = new SmtpClient(_smtp.Host, _smtp.Puerto)
        {
            EnableSsl = _smtp.UsarSsl,
            Credentials = new NetworkCredential(_smtp.Usuario, _smtp.Contrasena),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20000
        };
        await cliente.SendMailAsync(mensaje);
    }
}
