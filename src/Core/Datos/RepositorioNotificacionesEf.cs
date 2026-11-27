using Inventario.Core.Dominio;
using Inventario.Core.Notificaciones;
using Inventario.Core.Servicios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Datos;

/// <summary>Notificaciones en SQL Server. El correo del canal externo va a la misma cola que el resto (ColaCorreos).</summary>
public class RepositorioNotificacionesEf : IRepositorioNotificaciones
{
    private readonly CoreDbContext _db;
    private readonly ColaCorreos _cola;

    public RepositorioNotificacionesEf(CoreDbContext db, ColaCorreos cola)
    {
        _db = db;
        _cola = cola;
    }

    public Task<Usuario?> UsuarioAsync(int id) => _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<IReadOnlyList<Usuario>> AdministradoresActivosAsync() =>
        await _db.Usuarios
            .Where(u => u.Rol == Rol.Administrador && u.Activo && !u.Deshabilitado)
            .OrderBy(u => u.Id).ToListAsync();

    public Task<Notificacion?> ObtenerAsync(int id) => _db.Notificaciones.FirstOrDefaultAsync(n => n.Id == id);

    public async Task<IReadOnlyList<Notificacion>> DeUsuarioAsync(int usuarioId, bool soloNoLeidas) =>
        await _db.Notificaciones
            .Where(n => n.DestinatarioId == usuarioId && (!soloNoLeidas || !n.Leida))
            .OrderByDescending(n => n.FechaCreacion).ThenByDescending(n => n.Id)
            .ToListAsync();

    public Task<int> NoLeidasAsync(int usuarioId) =>
        _db.Notificaciones.CountAsync(n => n.DestinatarioId == usuarioId && !n.Leida);

    public void Agregar(Notificacion notificacion) => _db.Notificaciones.Add(notificacion);

    public void EncolarCorreo(string destinatario, string asunto, string cuerpo) => _cola.Encolar(destinatario, asunto, cuerpo);

    public Task GuardarAsync() => _db.SaveChangesAsync();
}
