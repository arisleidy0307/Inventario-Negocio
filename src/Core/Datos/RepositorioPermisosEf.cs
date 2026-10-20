using Inventario.Core.Permisos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Datos;

/// <summary>Implementación con EF Core / SQL Server: las solicitudes sobreviven a reiniciar la aplicación (RD-09).</summary>
public class RepositorioPermisosEf : IRepositorioPermisos
{
    private readonly CoreDbContext _db;

    public RepositorioPermisosEf(CoreDbContext db)
    {
        _db = db;
    }

    public Task<SolicitudPermiso?> ObtenerSolicitudAsync(int id) =>
        _db.SolicitudesPermiso.Include(s => s.Solicitante).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<SolicitudPermiso>> SolicitudesPendientesAsync() =>
        await _db.SolicitudesPermiso.Include(s => s.Solicitante)
            .Where(s => s.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(s => s.FechaSolicitud).ToListAsync();

    public async Task<IReadOnlyList<SolicitudPermiso>> SolicitudesDeUsuarioAsync(int usuarioId) =>
        await _db.SolicitudesPermiso.Include(s => s.Solicitante)
            .Where(s => s.SolicitanteId == usuarioId)
            .OrderByDescending(s => s.FechaSolicitud).ToListAsync();

    public Task<bool> ExisteSolicitudPendienteAsync(int usuarioId, string operacion) =>
        _db.SolicitudesPermiso.AnyAsync(s =>
            s.SolicitanteId == usuarioId && s.Operacion == operacion && s.Estado == EstadoSolicitudPermiso.Pendiente);

    public Task<bool> TienePermisoAsync(int usuarioId, string operacion) =>
        _db.PermisosUsuario.AnyAsync(p => p.UsuarioId == usuarioId && p.Operacion == operacion);

    public async Task<IReadOnlyList<string>> PermisosDeUsuarioAsync(int usuarioId) =>
        await _db.PermisosUsuario.Where(p => p.UsuarioId == usuarioId)
            .Select(p => p.Operacion).OrderBy(o => o).ToListAsync();

    public void AgregarSolicitud(SolicitudPermiso solicitud) => _db.SolicitudesPermiso.Add(solicitud);

    public void AgregarPermiso(PermisoUsuario permiso) => _db.PermisosUsuario.Add(permiso);

    public Task GuardarAsync() => _db.SaveChangesAsync();
}
