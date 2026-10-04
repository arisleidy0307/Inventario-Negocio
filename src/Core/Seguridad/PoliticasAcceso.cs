using Inventario.Core.Dominio;

namespace Inventario.Core.Seguridad;

/// <summary>Nombres de las operaciones del sistema. Se usan en el atributo [RequiereOperacion] de la API.</summary>
public static class Operaciones
{
    public const string Registrar = "Auth.Registrar";
    public const string Activar = "Auth.Activar";
    public const string ReenviarActivacion = "Auth.ReenviarActivacion";
    public const string Login = "Auth.Login";
    public const string Yo = "Auth.Yo";
    public const string Logout = "Auth.Logout";
    public const string Recuperar = "Auth.Recuperar";
    public const string Restablecer = "Auth.Restablecer";
    public const string CambiarPassword = "Auth.CambiarPassword";

    public const string ListarUsuarios = "Usuarios.Listar";
    public const string CambiarRol = "Usuarios.CambiarRol";
    public const string Desactivar = "Usuarios.Desactivar";
    public const string Reactivar = "Usuarios.Reactivar";
    public const string ForzarRestablecimiento = "Usuarios.ForzarRestablecimiento";
}

/// <summary>Qué exige una operación: nada (pública), una sesión válida, o una sesión con un rol concreto.</summary>
public sealed record Exigencia(bool RequiereSesion, Rol? RolRequerido)
{
    public static readonly Exigencia Publica = new(false, null);
    public static readonly Exigencia Autenticado = new(true, null);
    public static Exigencia SoloRol(Rol rol) => new(true, rol);

    public bool Permite(bool autenticado, Rol? rolUsuario) =>
        !RequiereSesion || (autenticado && (RolRequerido is null || rolUsuario == RolRequerido));
}

/// <summary>
/// RF-CA-05: ÚNICO lugar del código donde se lee qué rol puede ejecutar cada operación.
/// La API lo aplica en el servidor con [RequiereOperacion], así que una petición construida a mano
/// por un usuario Estándar también se rechaza (RF-CA-06, RD-06).
/// </summary>
public static class PoliticasAcceso
{
    private static readonly Exigencia Administrador = Exigencia.SoloRol(Rol.Administrador);

    public static readonly IReadOnlyDictionary<string, Exigencia> PorOperacion = new Dictionary<string, Exigencia>
    {
        [Operaciones.Registrar]          = Exigencia.Publica,
        [Operaciones.Activar]            = Exigencia.Publica,
        [Operaciones.ReenviarActivacion] = Exigencia.Publica,
        [Operaciones.Login]              = Exigencia.Publica,
        [Operaciones.Yo]                 = Exigencia.Autenticado,
        [Operaciones.Logout]             = Exigencia.Autenticado,
        [Operaciones.Recuperar]          = Exigencia.Publica,
        [Operaciones.Restablecer]        = Exigencia.Publica,
        [Operaciones.CambiarPassword]    = Exigencia.Autenticado,

        [Operaciones.ListarUsuarios]     = Administrador,
        [Operaciones.CambiarRol]         = Administrador,
        [Operaciones.Desactivar]         = Administrador,
        [Operaciones.Reactivar]          = Administrador,
        [Operaciones.ForzarRestablecimiento] = Administrador,
    };

    public static Exigencia Obtener(string operacion) =>
        PorOperacion.TryGetValue(operacion, out var exigencia)
            ? exigencia
            : throw new InvalidOperationException($"La operación '{operacion}' no declara su exigencia de rol en PoliticasAcceso.");
}
