using System.Net;
using System.Net.Mail;
using Inventario.Core.Dominio;

namespace Inventario.EnviadorCorreos;

/// <summary>Cómo sale un correo de la cola: por SMTP real o escrito en la consola del Enviador.</summary>
public interface IEnvioCorreo
{
    Task EnviarAsync(CorreoEnCola correo);
}

/// <summary>CORREO_MODO=smtp: envía el correo de verdad con las variables SMTP_*.</summary>
public class EnvioSmtp : IEnvioCorreo
{
    private readonly ConfiguracionSmtp _smtp;

    public EnvioSmtp(ConfiguracionSmtp smtp)
    {
        _smtp = smtp;
    }

    public async Task EnviarAsync(CorreoEnCola correo)
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

/// <summary>
/// CORREO_MODO=consola: escribe el correo completo (destinatario, asunto y cuerpo con el enlace o el código)
/// en la salida del Enviador. Se lee con «docker compose logs enviador». Sirve para probar el proyecto
/// sin una cuenta SMTP; la cola funciona igual que con SMTP.
/// </summary>
public class EnvioConsola : IEnvioCorreo
{
    public Task EnviarAsync(CorreoEnCola correo)
    {
        Console.WriteLine("  ----------------------------------------------------------------");
        Console.WriteLine($"  Para:   {correo.Destinatario}");
        Console.WriteLine($"  Asunto: {correo.Asunto}");
        Console.WriteLine();
        foreach (var linea in correo.Cuerpo.Split('\n'))
            Console.WriteLine("  " + linea.TrimEnd('\r'));
        Console.WriteLine("  ----------------------------------------------------------------");
        return Task.CompletedTask;
    }
}
