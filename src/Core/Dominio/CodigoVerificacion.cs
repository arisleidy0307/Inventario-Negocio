namespace Inventario.Core.Dominio;

public enum PropositoCodigo
{
    Activacion = 1,
    Recuperacion = 2
}

/// <summary>
/// Token o código de un solo uso con vencimiento (activación de cuenta y recuperación de contraseña).
/// Solo se guarda el hash SHA-256 del valor enviado por correo.
/// </summary>
public class CodigoVerificacion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public PropositoCodigo Proposito { get; set; }
    public string CodigoHash { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public bool Usado { get; set; }
    public DateTime? FechaUso { get; set; }

    public bool EsValido(DateTime ahora) => !Usado && ahora < FechaVencimiento;
}
