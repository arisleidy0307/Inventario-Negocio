using Inventario.Negocio.Dominio;
using Inventario.Negocio.Servicios;

namespace Inventario.Core.Tests.Negocio;

/// <summary>Repositorio en memoria para probar los servicios del negocio sin base de datos. Uno por prueba.</summary>
public sealed class RepositorioNegocioEnMemoria : IRepositorioNegocio
{
    private int _id = 1;
    public List<Producto> Productos { get; } = new();
    public List<Proveedor> Proveedores { get; } = new();
    public List<OrdenCompra> Ordenes { get; } = new();
    public List<MovimientoStock> Movimientos { get; } = new();

    public Task<Producto?> ObtenerProductoAsync(int id) => Task.FromResult(Productos.FirstOrDefault(p => p.Id == id));
    public Task<bool> ExisteCodigoProductoAsync(string codigo) => Task.FromResult(Productos.Any(p => p.Codigo == codigo));
    public Task<IReadOnlyList<Producto>> ProductosAsync() => Task.FromResult<IReadOnlyList<Producto>>(Productos.ToList());
    public void AgregarProducto(Producto producto) { producto.Id = _id++; Productos.Add(producto); }

    public Task<Proveedor?> ObtenerProveedorAsync(int id) => Task.FromResult(Proveedores.FirstOrDefault(p => p.Id == id));
    public Task<IReadOnlyList<Proveedor>> ProveedoresAsync() => Task.FromResult<IReadOnlyList<Proveedor>>(Proveedores.ToList());
    public void AgregarProveedor(Proveedor proveedor) { proveedor.Id = _id++; Proveedores.Add(proveedor); }

    public Task<OrdenCompra?> ObtenerOrdenAsync(int id) => Task.FromResult(Ordenes.FirstOrDefault(o => o.Id == id));
    public Task<IReadOnlyList<OrdenCompra>> OrdenesAsync(int? creadaPor) =>
        Task.FromResult<IReadOnlyList<OrdenCompra>>(Ordenes.Where(o => creadaPor == null || o.CreadaPorUsuarioId == creadaPor).ToList());
    public void AgregarOrden(OrdenCompra orden) { orden.Id = _id++; Ordenes.Add(orden); }

    public Task<IReadOnlyList<MovimientoStock>> MovimientosDeProductoAsync(int productoId) =>
        Task.FromResult<IReadOnlyList<MovimientoStock>>(Movimientos.Where(m => m.ProductoId == productoId).ToList());
    public void AgregarMovimiento(MovimientoStock movimiento) { movimiento.Id = _id++; Movimientos.Add(movimiento); }

    public Task GuardarAsync() => Task.CompletedTask;
}
