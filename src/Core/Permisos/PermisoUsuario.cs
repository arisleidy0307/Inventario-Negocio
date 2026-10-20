using Inventario.Core.Dominio;

namespace Inventario.Core.Permisos;

/// <summary>
/// Permiso otorgado a un usuario para ejecutar una operación concreta, además de lo que su rol le permite.
/// Lo crea el sistema al aplicar una solicitud aprobada (RF-GP-05).
/// </summary>
public class PermisoUsuario
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public DateTime FechaOtorgado { get; set; }
    public int SolicitudPermisoId { get; set; }
}
