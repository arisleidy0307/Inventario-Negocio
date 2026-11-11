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

    public const string CatalogoPermisos = "Permisos.Catalogo";
    public const string SolicitarPermiso = "Permisos.Solicitar";
    public const string MisSolicitudes = "Permisos.MisSolicitudes";
    public const string VerSolicitud = "Permisos.VerSolicitud";
    public const string ListarSolicitudesPendientes = "Permisos.ListarPendientes";
    public const string AprobarSolicitud = "Permisos.Aprobar";
    public const string RechazarSolicitud = "Permisos.Rechazar";

    // Manejador de documentos (Pieza 3 del Core)
    public const string SubirDocumento = "Documentos.Subir";
    public const string ListarDocumentos = "Documentos.Listar";
    public const string VerDocumento = "Documentos.Ver";
    public const string DescargarDocumento = "Documentos.Descargar";
    public const string EliminarDocumento = "Documentos.Eliminar";

    // Módulo de negocio (Inventario). Core solo conoce los nombres, no depende de Negocio (RD-03).
    public const string CrearProducto = "Productos.Crear";
    public const string ListarProductos = "Productos.Listar";
    public const string MovimientosProducto = "Productos.Movimientos";
    public const string CrearProveedor = "Proveedores.Crear";
    public const string ListarProveedores = "Proveedores.Listar";
    public const string CrearOrden = "Ordenes.Crear";
    public const string AgregarDetalleOrden = "Ordenes.AgregarDetalle";
    public const string ListarOrdenes = "Ordenes.Listar";
    public const string VerOrden = "Ordenes.Ver";
    public const string EmitirOrden = "Ordenes.Emitir";
    public const string RecibirOrden = "Ordenes.Recibir";
    public const string CancelarOrden = "Ordenes.Cancelar";
    public const string CotizarOrden = "Ordenes.Cotizar";
}

/// <summary>
/// Qué exige una operación: nada (pública), una sesión válida, o una sesión con un rol concreto.
/// Si es <c>Otorgable</c>, un usuario sin ese rol también puede ejecutarla cuando tiene el permiso
/// otorgado por una solicitud aprobada (Gestión de permisos, RF-GP-05).
/// </summary>
public sealed record Exigencia(bool RequiereSesion, Rol? RolRequerido, bool Otorgable = false)
{
    public static readonly Exigencia Publica = new(false, null);
    public static readonly Exigencia Autenticado = new(true, null);
    public static Exigencia SoloRol(Rol rol) => new(true, rol);
    public static Exigencia RolUPermisoOtorgado(Rol rol) => new(true, rol, Otorgable: true);

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

        // Otorgable: un Estándar puede pedir este permiso con una solicitud (Gestión de permisos).
        [Operaciones.ListarUsuarios]     = Exigencia.RolUPermisoOtorgado(Rol.Administrador),
        [Operaciones.CambiarRol]         = Administrador,
        [Operaciones.Desactivar]         = Administrador,
        [Operaciones.Reactivar]          = Administrador,
        [Operaciones.ForzarRestablecimiento] = Administrador,

        // Gestión de permisos (RF-GP-01 a 07)
        [Operaciones.CatalogoPermisos]            = Exigencia.Autenticado,
        [Operaciones.SolicitarPermiso]            = Exigencia.Autenticado,
        [Operaciones.MisSolicitudes]              = Exigencia.Autenticado,
        [Operaciones.VerSolicitud]                = Exigencia.Autenticado, // el servicio exige ser dueño o Administrador
        [Operaciones.ListarSolicitudesPendientes] = Administrador,
        [Operaciones.AprobarSolicitud]            = Administrador,
        [Operaciones.RechazarSolicitud]           = Administrador,

        // Manejador de documentos (RF-DOC-01 a 04): todas exigen sesión; el servicio exige ser dueño o Administrador
        [Operaciones.SubirDocumento]     = Exigencia.Autenticado,
        [Operaciones.ListarDocumentos]   = Exigencia.Autenticado, // un Estándar solo ve los suyos
        [Operaciones.VerDocumento]       = Exigencia.Autenticado,
        [Operaciones.DescargarDocumento] = Exigencia.Autenticado,
        [Operaciones.EliminarDocumento]  = Exigencia.Autenticado,

        // Módulo de negocio (RF-NEG-06): ninguna operación sin sesión válida y exigencia de rol
        [Operaciones.CrearProducto]       = Administrador,
        [Operaciones.ListarProductos]     = Exigencia.Autenticado,
        [Operaciones.MovimientosProducto] = Exigencia.Autenticado,
        [Operaciones.CrearProveedor]      = Administrador,
        [Operaciones.ListarProveedores]   = Exigencia.Autenticado,
        [Operaciones.CrearOrden]          = Exigencia.Autenticado,
        [Operaciones.AgregarDetalleOrden] = Exigencia.Autenticado, // el servicio exige ser quien la creó o Administrador
        [Operaciones.ListarOrdenes]       = Exigencia.Autenticado, // un Estándar solo ve las suyas
        [Operaciones.VerOrden]            = Exigencia.Autenticado, // un Estándar solo ve las suyas
        [Operaciones.EmitirOrden]         = Administrador,
        // Otorgable: un Estándar la solicita en Gestión de permisos y solo la ejecuta tras la aprobación.
        [Operaciones.RecibirOrden]        = Exigencia.RolUPermisoOtorgado(Rol.Administrador),
        [Operaciones.CancelarOrden]       = Administrador,
        // Usa la API externa de tasas de cambio; un Estándar solo cotiza sus propias órdenes
        [Operaciones.CotizarOrden]        = Exigencia.Autenticado,
    };

    /// <summary>Operaciones que se pueden pedir con una solicitud de permiso.</summary>
    public static IReadOnlyList<string> OperacionesOtorgables { get; } =
        PorOperacion.Where(p => p.Value.Otorgable).Select(p => p.Key).OrderBy(k => k).ToList();

    public static bool EsOtorgable(string? operacion) =>
        operacion is not null && PorOperacion.TryGetValue(operacion, out var e) && e.Otorgable;

    /// <summary>
    /// Decide si un usuario puede ejecutar una operación: por su rol o, si la operación es otorgable,
    /// porque tiene el permiso aplicado tras una solicitud aprobada.
    /// </summary>
    public static bool Permite(string operacion, bool autenticado, Rol? rol, IEnumerable<string>? permisosOtorgados)
    {
        var exigencia = Obtener(operacion);
        if (exigencia.Permite(autenticado, rol))
            return true;
        return autenticado && exigencia.Otorgable && (permisosOtorgados?.Contains(operacion) ?? false);
    }

    public static Exigencia Obtener(string operacion) =>
        PorOperacion.TryGetValue(operacion, out var exigencia)
            ? exigencia
            : throw new InvalidOperationException($"La operación '{operacion}' no declara su exigencia de rol en PoliticasAcceso.");
}
