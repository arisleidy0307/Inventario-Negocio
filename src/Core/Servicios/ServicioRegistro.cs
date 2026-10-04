using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Servicios;

/// <summary>Registro de usuarios y activación de la cuenta por correo (RF-CA-01, 02, 14, 15, 16, 17).</summary>
public class ServicioRegistro
{
    public const string MensajeReenvio =
        "Si el correo corresponde a una cuenta pendiente de activar, recibirás un nuevo enlace de activación.";

    private readonly CoreDbContext _db;
    private readonly HasherContrasena _hasher;
    private readonly GeneradorTokens _tokens;
    private readonly ColaCorreos _cola;
    private readonly IReloj _reloj;
    private readonly OpcionesCore _opciones;

    public ServicioRegistro(CoreDbContext db, HasherContrasena hasher, GeneradorTokens tokens,
        ColaCorreos cola, IReloj reloj, OpcionesCore opciones)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _cola = cola;
        _reloj = reloj;
        _opciones = opciones;
    }

    public async Task RegistrarAsync(string nombre, string correo, string contrasena)
    {
        nombre = (nombre ?? string.Empty).Trim();
        if (nombre.Length is 0 or > 100)
            throw ExcepcionControlada.Solicitud("El nombre es obligatorio y admite hasta 100 caracteres.");

        var correoNormalizado = ValidadorCorreo.Normalizar(correo);   // RD-07
        PoliticaContrasena.Validar(contrasena);                        // RF-CA-14

        if (await _db.Usuarios.AnyAsync(u => u.Correo == correoNormalizado)) // RF-CA-01
            throw ExcepcionControlada.Conflicto("Ya existe una cuenta registrada con ese correo.");

        var (hash, sal) = _hasher.Hashear(contrasena);                 // RF-CA-02
        var usuario = new Usuario
        {
            Nombre = nombre,
            Correo = correoNormalizado,
            ContrasenaHash = hash,
            ContrasenaSal = sal,
            Rol = Rol.Estandar,
            Activo = false,                                            // RF-CA-15
            FechaCreacion = _reloj.UtcNow
        };
        _db.Usuarios.Add(usuario);
        EmitirEnlaceActivacion(usuario);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // El índice único protege de dos registros simultáneos con el mismo correo.
            throw ExcepcionControlada.Conflicto("Ya existe una cuenta registrada con ese correo.");
        }
    }

    /// <summary>RF-CA-16: el enlace activa la cuenta una sola vez y solo antes de vencer.</summary>
    public async Task ActivarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw ExcepcionControlada.Solicitud("El enlace de activación no es válido.");

        var hash = _tokens.Hashear(token);
        var ahora = _reloj.UtcNow;
        var codigo = await _db.CodigosVerificacion
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.CodigoHash == hash && c.Proposito == PropositoCodigo.Activacion);

        if (codigo is null || !codigo.EsValido(ahora) || codigo.Usuario is null)
            throw ExcepcionControlada.Solicitud("El enlace de activación no es válido, ya fue usado o está vencido.");

        codigo.Usado = true;
        codigo.FechaUso = ahora;
        codigo.Usuario.Activo = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// RF-CA-17: la respuesta es la misma exista o no el correo. Si la cuenta existe y no está activa,
    /// se invalida el enlace anterior y se encola uno nuevo.
    /// </summary>
    public async Task ReenviarActivacionAsync(string correo)
    {
        var correoNormalizado = ValidadorCorreo.Normalizar(correo);
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);
        if (usuario is null || usuario.Activo)
            return;

        EmitirEnlaceActivacion(usuario);
        await _db.SaveChangesAsync();
    }

    private void EmitirEnlaceActivacion(Usuario usuario)
    {
        var ahora = _reloj.UtcNow;

        if (usuario.Id != 0)
        {
            // Invalida cualquier enlace anterior todavía utilizable.
            var anteriores = _db.CodigosVerificacion
                .Where(c => c.UsuarioId == usuario.Id && c.Proposito == PropositoCodigo.Activacion && !c.Usado)
                .ToList();
            foreach (var anterior in anteriores)
            {
                anterior.Usado = true;
                anterior.FechaUso = ahora;
            }
        }

        var (token, hash) = _tokens.Generar();
        _db.CodigosVerificacion.Add(new CodigoVerificacion
        {
            Usuario = usuario,
            Proposito = PropositoCodigo.Activacion,
            CodigoHash = hash,
            FechaEmision = ahora,
            FechaVencimiento = ahora.AddMinutes(_opciones.MinutosActivacion),
            Usado = false
        });

        var enlace = $"{_opciones.AppBaseUrl.TrimEnd('/')}/api/auth/activar?token={Uri.EscapeDataString(token)}";
        _cola.Encolar(usuario.Correo, "Activa tu cuenta - Inventario",
            $"Hola {usuario.Nombre}:\n\n" +
            $"Para activar tu cuenta abre este enlace:\n{enlace}\n\n" +
            $"El enlace se puede usar una sola vez y vence en {_opciones.MinutosActivacion} minutos.\n\n" +
            "Si no creaste esta cuenta, ignora este correo.");
    }
}
