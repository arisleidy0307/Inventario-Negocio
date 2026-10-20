using Inventario.Core.Comun;
using Inventario.Core.Permisos;

namespace Inventario.Core.Tests.GestionPermisos;

/// <summary>Reloj fijo: permite verificar exactamente "cuándo" registra cada transición.</summary>
public sealed class RelojFijo : IReloj
{
    public RelojFijo(DateTime ahora) => UtcNow = ahora;
    public DateTime UtcNow { get; set; }
}

/// <summary>
/// Repositorio en memoria para ejercitar ServicioPermisos sin base de datos ni API (RD-12).
/// Cada prueba crea el suyo, así ninguna comparte estado con otra.
/// </summary>
public sealed class RepositorioPermisosEnMemoria : IRepositorioPermisos
{
    private int _siguienteId = 1;
    public List<SolicitudPermiso> Solicitudes { get; } = new();
    public List<PermisoUsuario> Permisos { get; } = new();
    public int VecesGuardado { get; private set; }

    public Task<SolicitudPermiso?> ObtenerSolicitudAsync(int id) =>
        Task.FromResult(Solicitudes.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<SolicitudPermiso>> SolicitudesPendientesAsync() =>
        Task.FromResult<IReadOnlyList<SolicitudPermiso>>(
            Solicitudes.Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente).ToList());

    public Task<IReadOnlyList<SolicitudPermiso>> SolicitudesDeUsuarioAsync(int usuarioId) =>
        Task.FromResult<IReadOnlyList<SolicitudPermiso>>(Solicitudes.Where(s => s.SolicitanteId == usuarioId).ToList());

    public Task<bool> ExisteSolicitudPendienteAsync(int usuarioId, string operacion) =>
        Task.FromResult(Solicitudes.Any(s => s.SolicitanteId == usuarioId && s.Operacion == operacion
                                             && s.Estado == EstadoSolicitudPermiso.Pendiente));

    public Task<bool> TienePermisoAsync(int usuarioId, string operacion) =>
        Task.FromResult(Permisos.Any(p => p.UsuarioId == usuarioId && p.Operacion == operacion));

    public Task<IReadOnlyList<string>> PermisosDeUsuarioAsync(int usuarioId) =>
        Task.FromResult<IReadOnlyList<string>>(Permisos.Where(p => p.UsuarioId == usuarioId).Select(p => p.Operacion).ToList());

    public void AgregarSolicitud(SolicitudPermiso solicitud)
    {
        solicitud.Id = _siguienteId++;
        Solicitudes.Add(solicitud);
    }

    public void AgregarPermiso(PermisoUsuario permiso) => Permisos.Add(permiso);

    public Task GuardarAsync()
    {
        VecesGuardado++;
        return Task.CompletedTask;
    }
}
