using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Permisos;
using Inventario.Core.Seguridad;
using Inventario.Datos;
using Inventario.Negocio.Dominio;
using Inventario.Negocio.Estados;
using Inventario.Negocio.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventario.Core.Tests.Negocio;

/// <summary>
/// Prueba de integración: Gestión de permisos y el módulo de negocio REALES (servicios, repositorios EF Core
/// y AppDbContext) sobre una base SQLite en memoria. Sin dobles de ninguno de los dos y sin levantar la API.
/// Cada prueba crea su propia base, así que son independientes.
/// </summary>
public sealed class IntegracionNegocioPermisosTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly AppDbContext _db;
    private readonly ServicioPermisos _permisos;
    private readonly ServicioOrdenesCompra _ordenes;
    private readonly ServicioCatalogo _catalogo;

    public IntegracionNegocioPermisosTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _db = NuevoContexto();
        _db.Database.EnsureCreated();

        IReloj reloj = new RelojSistema();
        _permisos = new ServicioPermisos(new RepositorioPermisosEf(_db), reloj);
        var repoNegocio = new RepositorioNegocioEf(_db);
        _catalogo = new ServicioCatalogo(repoNegocio);
        _ordenes = new ServicioOrdenesCompra(repoNegocio, _permisos, reloj);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private AppDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexion).Options);

    private async Task<(Usuario Admin, Usuario Estandar, OrdenCompra Orden, Producto Producto)> OrdenEmitida()
    {
        var admin = NuevoUsuario("admin@stockcore.test", Rol.Administrador);
        var estandar = NuevoUsuario("almacen@stockcore.test", Rol.Estandar);
        _db.Usuarios.AddRange(admin, estandar);
        await _db.SaveChangesAsync();

        var proveedor = await _catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var producto = await _catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);
        var orden = await _ordenes.CrearAsync(estandar.Id, proveedor.Id);
        await _ordenes.AgregarDetalleAsync(orden.Id, estandar.Id, Rol.Estandar, producto.Id, 5, 850m);
        await _ordenes.EmitirAsync(orden.Id);
        return (admin, estandar, orden, producto);
    }

    private static Usuario NuevoUsuario(string correo, Rol rol) => new()
    {
        Nombre = correo, Correo = correo, ContrasenaHash = "x", ContrasenaSal = "x",
        Rol = rol, Activo = true, FechaCreacion = DateTime.UtcNow
    };

    private async Task<(EstadoOrdenCompra Estado, int Stock)> LeerDeLaBase(int ordenId, int productoId)
    {
        await using var otro = NuevoContexto();
        var orden = await otro.OrdenesCompra.AsNoTracking().SingleAsync(o => o.Id == ordenId);
        var producto = await otro.Productos.AsNoTracking().SingleAsync(p => p.Id == productoId);
        return (orden.Estado, producto.Stock);
    }

    [Trait("Integración", "Negocio + Gestión de permisos")]
    [Fact]
    public async Task RecibirOrden_SeRechazaMientrasLaSolicitudNoEstaAprobada_YSePermiteTrasLaAprobacion()
    {
        // Preparar: orden emitida y solicitud de permiso Ordenes.Recibir del Estándar, todavía Pendiente
        var (admin, estandar, orden, producto) = await OrdenEmitida();
        var solicitud = await _permisos.CrearSolicitudAsync(estandar.Id, Operaciones.RecibirOrden, "Recibo la mercancía");

        // Actuar 1: el Estándar intenta recibir antes de la aprobación
        var rechazo = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => _ordenes.RecibirAsync(orden.Id, estandar.Id, Rol.Estandar));

        // Verificar 1: rechazo y nada cambió en la base
        Assert.Equal(403, rechazo.CodigoEstado);
        Assert.Equal((EstadoOrdenCompra.Emitida, 0), await LeerDeLaBase(orden.Id, producto.Id));

        // Actuar 2: un Administrador aprueba la solicitud y el Estándar vuelve a intentarlo
        await _permisos.AprobarAsync(solicitud.Id, admin.Id);
        await _ordenes.RecibirAsync(orden.Id, estandar.Id, Rol.Estandar);

        // Verificar 2: la orden quedó Recibida y el stock sumado, leído de la base
        Assert.Equal((EstadoOrdenCompra.Recibida, 5), await LeerDeLaBase(orden.Id, producto.Id));
    }

    [Trait("Integración", "Negocio + Gestión de permisos")]
    [Fact]
    public async Task RecibirOrden_EstandarSinSolicitud_SeRechazaYLaOrdenSigueEmitida()
    {
        // Preparar
        var (_, estandar, orden, producto) = await OrdenEmitida();

        // Actuar
        var rechazo = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => _ordenes.RecibirAsync(orden.Id, estandar.Id, Rol.Estandar));

        // Verificar
        Assert.Equal(403, rechazo.CodigoEstado);
        Assert.Equal((EstadoOrdenCompra.Emitida, 0), await LeerDeLaBase(orden.Id, producto.Id));
    }

    [Trait("Integración", "Negocio + Gestión de permisos")]
    [Fact]
    public async Task RecibirOrden_SolicitudRechazada_SigueSinPermitirse()
    {
        // Preparar
        var (admin, estandar, orden, producto) = await OrdenEmitida();
        var solicitud = await _permisos.CrearSolicitudAsync(estandar.Id, Operaciones.RecibirOrden, null);
        await _permisos.RechazarAsync(solicitud.Id, admin.Id, "No trabajas en el almacén");

        // Actuar
        var rechazo = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => _ordenes.RecibirAsync(orden.Id, estandar.Id, Rol.Estandar));

        // Verificar
        Assert.Equal(403, rechazo.CodigoEstado);
        Assert.Equal((EstadoOrdenCompra.Emitida, 0), await LeerDeLaBase(orden.Id, producto.Id));
    }
}
