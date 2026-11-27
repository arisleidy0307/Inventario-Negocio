namespace Inventario.Core.Notificaciones;

/// <summary>Qué evento originó la notificación.</summary>
public enum TipoNotificacion
{
    SolicitudCreada = 1,
    SolicitudAprobada = 2,
    SolicitudRechazada = 3,
    OrdenEmitida = 4,
    OrdenRecibida = 5
}

/// <summary>
/// Notificación del canal interno (la bandeja del usuario). El canal de correo se resuelve aparte:
/// al crearla, el servicio encola además un correo si el destinatario no lo desactivó (RF-NOT-06, 07).
/// </summary>
public class Notificacion
{
    public const int LongitudMaximaTitulo = 200;
    public const int LongitudMaximaMensaje = 1000;

    public int Id { get; set; }
    public int DestinatarioId { get; set; }
    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public bool Leida { get; private set; }
    public DateTime? FechaLectura { get; private set; }

    /// <summary>RF-NOT-05. Marcarla otra vez no cambia la fecha de la primera lectura.</summary>
    public void MarcarLeida(DateTime ahora)
    {
        if (Leida)
            return;
        Leida = true;
        FechaLectura = ahora;
    }
}
