namespace Inventario.Core.Notificaciones;

/// <summary>
/// Lo que usan los demás servicios (Gestión de permisos, el negocio) para avisar de un evento.
/// No saben cómo se entrega: el motor crea la notificación interna y, si corresponde, encola el correo.
/// </summary>
public interface INotificador
{
    /// <summary>Notifica a un usuario concreto.</summary>
    Task NotificarAsync(int destinatarioId, TipoNotificacion tipo, string titulo, string mensaje);

    /// <summary>RF-NOT-01: notifica a cada Administrador activo y a nadie más.</summary>
    Task NotificarAdministradoresAsync(TipoNotificacion tipo, string titulo, string mensaje);
}
