using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Servicios;

public record SesionIniciada(string Token, DateTime Vence, Usuario Usuario);

public record SesionValidada(Usuario Usuario, Sesion Sesion);

/// <summary>Inicio y cierre de sesión, bloqueo por intentos y validación de credenciales (RF-CA-03, 07, 18, 19).</summary>
public class ServicioSesion
{
    /// <summary>RF-CA-03: el mismo mensaje para correo inexistente y para contraseña incorrecta.</summary>
    public const string MensajeCredencialesInvalidas = "Credenciales inválidas.";

    private readonly CoreDbContext _db;
    private readonly HasherContrasena _hasher;
    private readonly GeneradorTokens _tokens;
    private readonly IReloj _reloj;
    private readonly OpcionesCore _opciones;

    public ServicioSesion(CoreDbContext db, HasherContrasena hasher, GeneradorTokens tokens, IReloj reloj, OpcionesCore opciones)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _reloj = reloj;
        _opciones = opciones;
    }

    /// <summary>Orden: usuario → bloqueo → contraseña → cuenta activa.</summary>
    public async Task<SesionIniciada> IniciarSesionAsync(string correo, string contrasena)
    {
        var correoNormalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();
        var ahora = _reloj.UtcNow;

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);
        if (usuario is null)
            throw ExcepcionControlada.NoAutorizado(MensajeCredencialesInvalidas);

        // RF-CA-19: durante el bloqueo se rechaza aunque la contraseña sea correcta.
        if (usuario.BloqueadoHasta is { } hasta && hasta > ahora)
        {
            var minutos = (int)Math.Ceiling((hasta - ahora).TotalMinutes);
            throw new ExcepcionControlada(423,
                $"La cuenta está bloqueada temporalmente por intentos fallidos. Intenta de nuevo en {minutos} minuto(s).");
        }

        if (!_hasher.Verificar(contrasena ?? string.Empty, usuario.ContrasenaHash, usuario.ContrasenaSal))
        {
            usuario.IntentosFallidos++;
            if (usuario.IntentosFallidos >= _opciones.IntentosAntesDeBloqueo)
            {
                usuario.BloqueadoHasta = ahora.AddMinutes(_opciones.MinutosBloqueo);
                usuario.IntentosFallidos = 0;
            }
            await _db.SaveChangesAsync();
            throw ExcepcionControlada.NoAutorizado(MensajeCredencialesInvalidas);
        }

        // Contraseña correcta: el contador vuelve a cero (RF-CA-19).
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        if (!usuario.Activo)
        {
            await _db.SaveChangesAsync();
            throw ExcepcionControlada.Prohibido(
                "La cuenta no está activa. Ábrela con el enlace que te enviamos por correo o pide uno nuevo.");
        }

        if (usuario.Deshabilitado)
        {
            await _db.SaveChangesAsync();
            throw ExcepcionControlada.Prohibido("La cuenta está desactivada. Contacta al administrador.");
        }

        var (token, hash) = _tokens.Generar();
        var vence = ahora.AddHours(_opciones.HorasSesion);
        _db.Sesiones.Add(new Sesion
        {
            UsuarioId = usuario.Id,
            TokenHash = hash,
            FechaEmision = ahora,
            FechaVencimiento = vence
        });
        await _db.SaveChangesAsync();

        return new SesionIniciada(token, vence, usuario);
    }

    /// <summary>
    /// Regla única de validez de una credencial: existe, no está revocada ni vencida y su usuario puede operar.
    /// </summary>
    public async Task<SesionValidada?> ValidarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = _tokens.Hashear(token);
        var ahora = _reloj.UtcNow;
        var sesion = await _db.Sesiones.Include(s => s.Usuario).FirstOrDefaultAsync(s => s.TokenHash == hash);

        if (sesion?.Usuario is null) return null;
        if (sesion.Revocada || sesion.FechaVencimiento <= ahora) return null;
        if (!sesion.Usuario.Activo || sesion.Usuario.Deshabilitado) return null; // RF-CA-20

        return new SesionValidada(sesion.Usuario, sesion);
    }

    /// <summary>RF-CA-18: la credencial cerrada deja de servir.</summary>
    public async Task CerrarSesionAsync(int sesionId)
    {
        var sesion = await _db.Sesiones.FirstOrDefaultAsync(s => s.Id == sesionId);
        if (sesion is null || sesion.Revocada) return;
        sesion.Revocada = true;
        sesion.FechaRevocacion = _reloj.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<Usuario> ObtenerUsuarioAsync(int usuarioId) =>
        await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId)
        ?? throw ExcepcionControlada.NoEncontrado("Usuario no encontrado.");
}
