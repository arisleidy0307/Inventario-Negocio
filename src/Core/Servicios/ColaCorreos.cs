using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;

namespace Inventario.Core.Servicios;

/// <summary>
/// RF-NOT-08: solo registra el correo en la cola. No toca SMTP, así que la operación de negocio
/// termina bien aunque el servidor de correo no responda. El envío lo hace EnviadorCorreos.
/// El correo se guarda junto con el resto de cambios cuando el servicio llama a SaveChanges.
/// </summary>
public class ColaCorreos
{
    private readonly CoreDbContext _db;
    private readonly IReloj _reloj;

    public ColaCorreos(CoreDbContext db, IReloj reloj)
    {
        _db = db;
        _reloj = reloj;
    }

    public void Encolar(string destinatario, string asunto, string cuerpo)
    {
        _db.CorreosEnCola.Add(new CorreoEnCola
        {
            Destinatario = destinatario,
            Asunto = asunto,
            Cuerpo = cuerpo,
            Estado = EstadoCorreo.Pendiente,
            FechaCreacion = _reloj.UtcNow
        });
    }
}
