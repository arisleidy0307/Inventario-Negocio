using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Servicios;

/// <summary>Recuperación, restablecimiento, restablecimiento forzado y cambio de contraseña (RF-CA-09 a 13, 22).</summary>
public class ServicioContrasenas
{
    public const string MensajeRecuperacion =
        "Si el correo está registrado, recibirás un código para restablecer tu contraseña.";

    private readonly CoreDbContext _db;
    private readonly HasherContrasena _hasher;
    private readonly GeneradorTokens _tokens;
    private readonly ColaCorreos _cola;
    private readonly IReloj _reloj;
    private readonly OpcionesCore _opciones;

    public ServicioContrasenas(CoreDbContext db, HasherContrasena hasher, GeneradorTokens tokens,
        ColaCorreos cola, IReloj reloj, OpcionesCore opciones)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _cola = cola;
        _reloj = reloj;
        _opciones = opciones;
    }

    /// <summary>RF-CA-09: la respuesta es idéntica exista o no el correo.</summary>
    public async Task SolicitarRecuperacionAsync(string correo)
    {
        var correoNormalizado = ValidadorCorreo.Normalizar(correo);
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);
        if (usuario is null || usuario.Deshabilitado)
            return;

        await EmitirCodigoRecuperacionAsync(usuario,
            "Recupera tu contraseña - Inventario",
            "Recibimos una solicitud para restablecer tu contraseña.");
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// RF-CA-10 / RF-CA-11 / RF-CA-12: con un código válido define una nueva contraseña.
    /// Código usado o vencido → rechazo y la contraseña no cambia.
    /// </summary>
    public async Task RestablecerAsync(string? codigo, string nuevaContrasena)
    {
        PoliticaContrasena.Validar(nuevaContrasena); // RF-CA-14

        if (string.IsNullOrWhiteSpace(codigo))
            throw ExcepcionControlada.Solicitud("El código de recuperación no es válido, ya fue usado o está vencido.");

        var ahora = _reloj.UtcNow;
        var hash = _tokens.Hashear(codigo.Trim());
        var registro = await _db.CodigosVerificacion
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.CodigoHash == hash && c.Proposito == PropositoCodigo.Recuperacion);

        if (registro?.Usuario is null || !registro.EsValido(ahora))
            throw ExcepcionControlada.Solicitud("El código de recuperación no es válido, ya fue usado o está vencido.");

        registro.Usado = true;
        registro.FechaUso = ahora;
        await AplicarNuevaContrasenaAsync(registro.Usuario, nuevaContrasena);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// RF-CA-13: el Administrador fuerza el restablecimiento. La contraseña anterior deja de servir
    /// y el usuario recibe por la cola un código para definir una nueva.
    /// </summary>
    public async Task ForzarRestablecimientoAsync(int usuarioId)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId)
                      ?? throw ExcepcionControlada.NoEncontrado("No existe un usuario con ese identificador.");

        // Hash inutilizable: ninguna contraseña coincide con él.
        usuario.ContrasenaHash = "!restablecimiento-forzado";
        usuario.ContrasenaSal = "!";
        await InvalidarSesionesAsync(usuario);

        await EmitirCodigoRecuperacionAsync(usuario,
            "Debes definir una nueva contraseña - Inventario",
            "Un administrador restableció tu contraseña. Tu contraseña anterior ya no funciona.");
        await _db.SaveChangesAsync();
    }

    /// <summary>RF-CA-22: con sesión, cambia la propia contraseña indicando la actual.</summary>
    public async Task CambiarAsync(int usuarioId, string actual, string nueva)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId)
                      ?? throw ExcepcionControlada.NoEncontrado("Usuario no encontrado.");

        if (!_hasher.Verificar(actual ?? string.Empty, usuario.ContrasenaHash, usuario.ContrasenaSal))
            throw ExcepcionControlada.Solicitud("La contraseña actual no es correcta.");

        PoliticaContrasena.Validar(nueva); // RF-CA-14
        await AplicarNuevaContrasenaAsync(usuario, nueva);
        await _db.SaveChangesAsync();
    }

    private async Task AplicarNuevaContrasenaAsync(Usuario usuario, string nueva)
    {
        var (hash, sal) = _hasher.Hashear(nueva);
        usuario.ContrasenaHash = hash;
        usuario.ContrasenaSal = sal;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        await InvalidarSesionesAsync(usuario); // RF-CA-12
    }

    private async Task InvalidarSesionesAsync(Usuario usuario)
    {
        var ahora = _reloj.UtcNow;
        usuario.ContrasenaCambiadaEn = ahora;
        var sesiones = await _db.Sesiones.Where(s => s.UsuarioId == usuario.Id && !s.Revocada).ToListAsync();
        foreach (var s in sesiones)
        {
            s.Revocada = true;
            s.FechaRevocacion = ahora;
        }
    }

    private async Task EmitirCodigoRecuperacionAsync(Usuario usuario, string asunto, string introduccion)
    {
        var ahora = _reloj.UtcNow;

        // Un solo código vigente a la vez: los anteriores se invalidan.
        var anteriores = await _db.CodigosVerificacion
            .Where(c => c.UsuarioId == usuario.Id && c.Proposito == PropositoCodigo.Recuperacion && !c.Usado)
            .ToListAsync();
        foreach (var anterior in anteriores)
        {
            anterior.Usado = true;
            anterior.FechaUso = ahora;
        }

        var (codigo, hash) = _tokens.Generar();
        _db.CodigosVerificacion.Add(new CodigoVerificacion
        {
            UsuarioId = usuario.Id,
            Proposito = PropositoCodigo.Recuperacion,
            CodigoHash = hash,
            FechaEmision = ahora,
            FechaVencimiento = ahora.AddMinutes(_opciones.MinutosRecuperacion),
            Usado = false
        });

        var url = $"{_opciones.AppBaseUrl.TrimEnd('/')}/api/auth/restablecer";
        _cola.Encolar(usuario.Correo, asunto,
            $"Hola {usuario.Nombre}:\n\n{introduccion}\n\n" +
            $"Tu código de recuperación es:\n\n{codigo}\n\n" +
            $"Úsalo una sola vez en POST {url} con el cuerpo:\n" +
            "{ \"codigo\": \"<el código>\", \"nuevaContrasena\": \"<tu nueva contraseña>\" }\n\n" +
            $"El código vence en {_opciones.MinutosRecuperacion} minutos. Si no lo solicitaste, ignora este correo.");
    }
}
