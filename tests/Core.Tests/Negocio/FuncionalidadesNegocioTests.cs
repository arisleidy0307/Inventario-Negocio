using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Permisos;
using Inventario.Core.Seguridad;
using Inventario.Core.Tests.GestionPermisos;
using Inventario.Negocio.Dominio;
using Inventario.Negocio.Estados;
using Inventario.Negocio.Servicios;
using Xunit;

namespace Inventario.Core.Tests.Negocio;

/// <summary>
/// Las funcionalidades del módulo de negocio (RF-NEG-02), cada una probada por separado con repositorios
/// en memoria. La integración real con Gestión de permisos está en IntegracionNegocioPermisosTests.
/// </summary>
public class FuncionalidadesNegocioTests
{
    private const int AdminId = 1;
    private const int EstandarId = 10;
    private const int OtroEstandarId = 20;
    private static readonly DateTime Ahora = new(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);

    private sealed record Contexto(RepositorioNegocioEnMemoria Repo, ServicioCatalogo Catalogo, ServicioOrdenesCompra Ordenes);

    private static Contexto Crear()
    {
        var repo = new RepositorioNegocioEnMemoria();
        var permisos = new ServicioPermisos(new RepositorioPermisosEnMemoria(), new RelojFijo(Ahora));
        return new Contexto(repo, new ServicioCatalogo(repo), new ServicioOrdenesCompra(repo, permisos, new RelojFijo(Ahora)));
    }

    private static async Task<OrdenCompra> OrdenEmitidaConDetalle(Contexto c, int cantidad = 5)
    {
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var producto = await c.Catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);
        var orden = await c.Ordenes.CrearAsync(EstandarId, proveedor.Id);
        await c.Ordenes.AgregarDetalleAsync(orden.Id, EstandarId, Rol.Estandar, producto.Id, cantidad, 850m);
        await c.Ordenes.EmitirAsync(orden.Id);
        return orden;
    }

    // ---------- Funcionalidad 1: productos ----------

    [Trait("Funcionalidad", "1 Productos")]
    [Fact]
    public async Task CrearProducto_CodigoNuevo_SeGuardaConStockCero()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var producto = await c.Catalogo.CrearProductoAsync(" p-001 ", "Arroz 25 lb", 900m);

        // Verificar
        Assert.Equal("P-001", producto.Codigo);
        Assert.Equal(0, producto.Stock);
    }

    [Trait("Funcionalidad", "1 Productos")]
    [Fact]
    public async Task CrearProducto_CodigoRepetido_SeRechazaConError409()
    {
        // Preparar
        var c = Crear();
        await c.Catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Catalogo.CrearProductoAsync("p-001", "Otro", 1m));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
    }

    [Trait("Funcionalidad", "1 Productos")]
    [Fact]
    public void CrearProducto_UsuarioEstandar_NoEstaPermitido()
    {
        // Preparar
        var operacion = Operaciones.CrearProducto;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, Array.Empty<string>());

        // Verificar
        Assert.False(permitido);
    }

    // ---------- Funcionalidad 2: proveedores ----------

    [Trait("Funcionalidad", "2 Proveedores")]
    [Fact]
    public async Task CrearProveedor_SinNombre_SeRechazaConError400()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Catalogo.CrearProveedorAsync("  ", null, null));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
    }

    [Trait("Funcionalidad", "2 Proveedores")]
    [Fact]
    public async Task ListarProveedores_DevuelveLosCreados()
    {
        // Preparar
        var c = Crear();
        await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", "ventas@caribe.com", "809-555-0101");

        // Actuar
        var proveedores = await c.Catalogo.ProveedoresAsync();

        // Verificar
        Assert.Equal(new[] { "Distribuidora Caribe" }, proveedores.Select(p => p.Nombre).ToArray());
    }

    // ---------- Funcionalidad 3: órdenes en Borrador y sus detalles ----------

    [Trait("Funcionalidad", "3 Órdenes y detalles")]
    [Fact]
    public async Task CrearOrden_ProveedorExistente_NaceEnBorrador()
    {
        // Preparar
        var c = Crear();
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);

        // Actuar
        var orden = await c.Ordenes.CrearAsync(EstandarId, proveedor.Id);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Borrador, orden.Estado);
        Assert.Equal(EstandarId, orden.CreadaPorUsuarioId);
    }

    [Trait("Funcionalidad", "3 Órdenes y detalles")]
    [Fact]
    public async Task CrearOrden_ProveedorInexistente_SeRechazaConError400()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Ordenes.CrearAsync(EstandarId, 999));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
    }

    [Trait("Funcionalidad", "3 Órdenes y detalles")]
    [Fact]
    public async Task AgregarDetalle_OrdenDeOtroUsuario_SeRechazaConError403()
    {
        // Preparar
        var c = Crear();
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var producto = await c.Catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);
        var ajena = await c.Ordenes.CrearAsync(OtroEstandarId, proveedor.Id);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => c.Ordenes.AgregarDetalleAsync(ajena.Id, EstandarId, Rol.Estandar, producto.Id, 1, 10m));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.Empty(ajena.Detalles);
    }

    [Trait("Funcionalidad", "3 Órdenes y detalles")]
    [Fact]
    public async Task AgregarDetalle_CantidadCero_SeRechazaConError400()
    {
        // Preparar
        var c = Crear();
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var producto = await c.Catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);
        var orden = await c.Ordenes.CrearAsync(EstandarId, proveedor.Id);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => c.Ordenes.AgregarDetalleAsync(orden.Id, EstandarId, Rol.Estandar, producto.Id, 0, 10m));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
    }

    [Trait("Funcionalidad", "3 Órdenes y detalles")]
    [Fact]
    public async Task ObtenerOrden_EstandarPideLaDeOtroUsuario_SeRechazaConError403()
    {
        // Preparar
        var c = Crear();
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var ajena = await c.Ordenes.CrearAsync(OtroEstandarId, proveedor.Id);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Ordenes.ObtenerAsync(ajena.Id, EstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
    }

    // ---------- Funcionalidad 4: emitir ----------

    [Trait("Funcionalidad", "4 Emitir")]
    [Fact]
    public void EmitirOrden_UsuarioEstandar_NoEstaPermitido()
    {
        // Preparar
        var operacion = Operaciones.EmitirOrden;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, Array.Empty<string>());

        // Verificar
        Assert.False(permitido);
    }

    // ---------- Funcionalidad 5: recibir (suma stock y registra movimientos) ----------

    [Trait("Funcionalidad", "5 Recibir")]
    [Fact]
    public async Task RecibirOrden_Administrador_SumaElStockDelProducto()
    {
        // Preparar
        var c = Crear();
        var orden = await OrdenEmitidaConDetalle(c, cantidad: 5);

        // Actuar
        await c.Ordenes.RecibirAsync(orden.Id, AdminId, Rol.Administrador);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
        Assert.Equal(5, c.Repo.Productos.Single().Stock);
    }

    [Trait("Funcionalidad", "5 Recibir")]
    [Fact]
    public async Task RecibirOrden_Administrador_RegistraUnMovimientoDeEntradaPorDetalle()
    {
        // Preparar
        var c = Crear();
        var orden = await OrdenEmitidaConDetalle(c, cantidad: 5);

        // Actuar
        await c.Ordenes.RecibirAsync(orden.Id, AdminId, Rol.Administrador);

        // Verificar
        var movimiento = Assert.Single(c.Repo.Movimientos);
        Assert.Equal(TipoMovimiento.Entrada, movimiento.Tipo);
        Assert.Equal(5, movimiento.Cantidad);
        Assert.Equal(orden.Id, movimiento.OrdenCompraId);
    }

    [Trait("Funcionalidad", "5 Recibir")]
    [Fact]
    public async Task RecibirOrden_EstandarSinPermiso_SeRechazaYLaOrdenSigueEmitida()
    {
        // Preparar
        var c = Crear();
        var orden = await OrdenEmitidaConDetalle(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Ordenes.RecibirAsync(orden.Id, EstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Emitida, orden.Estado);
        Assert.Equal(0, c.Repo.Productos.Single().Stock);
    }

    // ---------- Funcionalidad 6: cancelar ----------

    [Trait("Funcionalidad", "6 Cancelar")]
    [Fact]
    public async Task CancelarOrden_EmitidaConMotivo_NoModificaElStock()
    {
        // Preparar
        var c = Crear();
        var orden = await OrdenEmitidaConDetalle(c);

        // Actuar
        await c.Ordenes.CancelarAsync(orden.Id, "El proveedor no tiene existencia");

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
        Assert.Equal(0, c.Repo.Productos.Single().Stock);
        Assert.Empty(c.Repo.Movimientos);
    }

    // ---------- Funcionalidad 7: movimientos de stock ----------

    [Trait("Funcionalidad", "7 Movimientos")]
    [Fact]
    public async Task Movimientos_ProductoInexistente_SeRechazaConError404()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => c.Catalogo.MovimientosAsync(999));

        // Verificar
        Assert.Equal(404, error.CodigoEstado);
    }

    [Trait("Funcionalidad", "7 Movimientos")]
    [Fact]
    public async Task Movimientos_TrasRecibirUnaOrden_DevuelveLaEntrada()
    {
        // Preparar
        var c = Crear();
        var orden = await OrdenEmitidaConDetalle(c, cantidad: 3);
        await c.Ordenes.RecibirAsync(orden.Id, AdminId, Rol.Administrador);
        var producto = c.Repo.Productos.Single();

        // Actuar
        var movimientos = await c.Catalogo.MovimientosAsync(producto.Id);

        // Verificar
        Assert.Equal(new[] { 3 }, movimientos.Select(m => m.Cantidad).ToArray());
    }
}
