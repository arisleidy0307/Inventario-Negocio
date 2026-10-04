namespace Inventario.Core.Dominio;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Correo normalizado (sin espacios y en minúsculas). Único (RF-CA-01).</summary>
    public string Correo { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 de la contraseña. Nunca se guarda la contraseña (RF-CA-02).</summary>
    public string ContrasenaHash { get; set; } = string.Empty;

    /// <summary>Sal aleatoria propia de este usuario (RD-05).</summary>
    public string ContrasenaSal { get; set; } = string.Empty;

    public Rol Rol { get; set; } = Rol.Estandar;

    /// <summary>La cuenta nace inactiva y se activa con el enlace del correo (RF-CA-15, RF-CA-16).</summary>
    public bool Activo { get; set; }

    /// <summary>RF-CA-19: intentos fallidos consecutivos de inicio de sesión.</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>RF-CA-19: mientras sea posterior a la hora actual, el inicio de sesión se rechaza.</summary>
    public DateTime? BloqueadoHasta { get; set; }

    public DateTime FechaCreacion { get; set; }
}
