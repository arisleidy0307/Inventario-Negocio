namespace Inventario.Core.Permisos;

/// <summary>
/// Acceso a datos de Gestión de permisos. El servicio depende de esta interfaz y no de EF,
/// así las pruebas lo ejercitan directamente con un repositorio en memoria (RD-12).
/// </summary>
public interface IRepositorioPermisos
{
    Task<SolicitudPermiso?> ObtenerSolicitudAsync(int id);
    Task<IReadOnlyList<SolicitudPermiso>> SolicitudesPendientesAsync();
    Task<IReadOnlyList<SolicitudPermiso>> SolicitudesDeUsuarioAsync(int usuarioId);
    Task<bool> ExisteSolicitudPendienteAsync(int usuarioId, string operacion);
    Task<bool> TienePermisoAsync(int usuarioId, string operacion);
    Task<IReadOnlyList<string>> PermisosDeUsuarioAsync(int usuarioId);
    void AgregarSolicitud(SolicitudPermiso solicitud);
    void AgregarPermiso(PermisoUsuario permiso);
    Task GuardarAsync();
}
