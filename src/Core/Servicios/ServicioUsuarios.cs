using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Servicios;

/// <summary>Vista del usuario para el listado: nunca incluye hashes, sales ni tokens (RF-CA-21).</summary>
public record UsuarioResumen(int Id, string Nombre, string Correo, string Rol, string Estado, bool Activo,
    bool Deshabilitado, DateTime FechaCreacion);

/// <summary>Administración de usuarios (RF-CA-04, 08, 20, 21). Las exigencias de rol viven en PoliticasAcceso.</summary>
public class ServicioUsuarios
{
    private readonly CoreDbContext _db;
    private readonly HasherContrasena _hasher;
    private readonly IReloj _reloj;

    public ServicioUsuarios(CoreDbContext db, HasherContrasena hasher, IReloj reloj)
    {
        _db = db;
        _hasher = hasher;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<UsuarioResumen>> ListarAsync()
    {
        var ahora = _reloj.UtcNow;
        var usuarios = await _db.Usuarios.AsNoTracking().OrderBy(u => u.Id).ToListAsync();
        return usuarios.Select(u => new UsuarioResumen(u.Id, u.Nombre, u.Correo, u.Rol.ToString(),
            DescribirEstado(u, ahora), u.Activo, u.Deshabilitado, u.FechaCreacion)).ToList();
    }

    /// <summary>RF-CA-08: solo un Administrador llega aquí (PoliticasAcceso). No puede cambiarse a sí mismo.</summary>
    public async Task CambiarRolAsync(int actorId, int usuarioId, string? rolTexto)
    {
        if (!Enum.TryParse<Rol>(rolTexto, ignoreCase: true, out var rol) || !Enum.IsDefined(rol) ||
            int.TryParse(rolTexto, out _))
            throw ExcepcionControlada.Solicitud("El rol debe ser 'Administrador' o 'Estandar'.");

        if (actorId == usuarioId)
            throw ExcepcionControlada.Solicitud("No puedes cambiar tu propio rol.");

        var usuario = await Buscar(usuarioId);
        usuario.Rol = rol;
        await _db.SaveChangesAsync();
    }

    /// <summary>RF-CA-20: desactivar invalida sus sesiones abiertas. Un Administrador no puede desactivarse.</summary>
    public async Task DesactivarAsync(int actorId, int usuarioId)
    {
        if (actorId == usuarioId)
            throw ExcepcionControlada.Solicitud("No puedes desactivar tu propia cuenta.");

        var usuario = await Buscar(usuarioId);
        usuario.Deshabilitado = true;

        var ahora = _reloj.UtcNow;
        var sesiones = await _db.Sesiones.Where(s => s.UsuarioId == usuarioId && !s.Revocada).ToListAsync();
        foreach (var s in sesiones)
        {
            s.Revocada = true;
            s.FechaRevocacion = ahora;
        }
        await _db.SaveChangesAsync();
    }

    public async Task ReactivarAsync(int usuarioId)
    {
        var usuario = await Buscar(usuarioId);
        usuario.Deshabilitado = false;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Crea el primer Administrador al arrancar a partir de ADMIN_EMAIL y ADMIN_PASSWORD.
    /// Si ya existe un usuario con ese correo no hace nada.
    /// </summary>
    public async Task<bool> AsegurarAdministradorAsync(string? correo, string? contrasena)
    {
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
            return false;

        var correoNormalizado = ValidadorCorreo.Normalizar(correo);
        PoliticaContrasena.Validar(contrasena);
        if (await _db.Usuarios.AnyAsync(u => u.Correo == correoNormalizado))
            return false;

        var (hash, sal) = _hasher.Hashear(contrasena);
        _db.Usuarios.Add(new Usuario
        {
            Nombre = "Administrador",
            Correo = correoNormalizado,
            ContrasenaHash = hash,
            ContrasenaSal = sal,
            Rol = Rol.Administrador,
            Activo = true,
            FechaCreacion = _reloj.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<Usuario> Buscar(int id) =>
        await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id)
        ?? throw ExcepcionControlada.NoEncontrado("No existe un usuario con ese identificador.");

    private static string DescribirEstado(Usuario u, DateTime ahora) =>
        u.Deshabilitado ? "Desactivado"
        : !u.Activo ? "Pendiente de activación"
        : u.BloqueadoHasta > ahora ? "Bloqueado temporalmente"
        : "Activo";
}
