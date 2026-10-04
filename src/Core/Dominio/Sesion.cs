namespace Inventario.Core.Dominio;

/// <summary>
/// Credencial de sesión opaca. Al cliente se le entrega un valor aleatorio; aquí solo se guarda su hash.
/// Al estar en la base de datos se puede invalidar: cierre de sesión (RF-CA-18), cambio de contraseña
/// (RF-CA-12) y desactivación del usuario (RF-CA-20).
/// </summary>
public class Sesion
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public bool Revocada { get; set; }
    public DateTime? FechaRevocacion { get; set; }
}
