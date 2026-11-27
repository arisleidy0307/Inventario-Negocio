namespace Inventario.Core.Dominio;

/// <summary>Estados de un correo en la cola (RF-NOT-11).</summary>
public enum EstadoCorreo
{
    Pendiente = 1,
    Enviado = 2,

    /// <summary>RF-NOT-10: agotó el máximo de intentos (CORREO_MAX_INTENTOS). Ya no se reintenta ni se borra.</summary>
    Fallido = 3
}

/// <summary>RF-NOT-08: el correo no se envía dentro de la operación; se registra aquí y otro proceso lo envía.</summary>
public class CorreoEnCola
{
    public int Id { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public EstadoCorreo Estado { get; set; } = EstadoCorreo.Pendiente;
    public int Intentos { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }

    /// <summary>Cuándo el enviador lo intentó por última vez, con éxito o no.</summary>
    public DateTime? FechaUltimoIntento { get; set; }
    public string? UltimoError { get; set; }
}
