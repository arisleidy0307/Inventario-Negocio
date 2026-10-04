namespace Inventario.Core.Servicios;

/// <summary>Parámetros del Core. Se cargan desde variables de entorno en Program.cs.</summary>
public class OpcionesCore
{
    /// <summary>APP_BASE_URL: con ella se construyen los enlaces de los correos.</summary>
    public string AppBaseUrl { get; set; } = "https://localhost:7001";

    /// <summary>ACTIVACION_MINUTOS: vigencia del enlace de activación (por defecto 24 h).</summary>
    public int MinutosActivacion { get; set; } = 24 * 60;

    /// <summary>SESION_HORAS: vigencia de la credencial de sesión (por defecto 8 h).</summary>
    public int HorasSesion { get; set; } = 8;

    /// <summary>RF-CA-19: intentos fallidos consecutivos antes del bloqueo.</summary>
    public int IntentosAntesDeBloqueo { get; set; } = 5;

    /// <summary>RF-CA-19: minutos de bloqueo.</summary>
    public int MinutosBloqueo { get; set; } = 15;
}
