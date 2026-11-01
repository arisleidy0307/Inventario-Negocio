using Inventario.Negocio.Dominio;

namespace Inventario.Negocio.Servicios;

/// <summary>Acceso a datos del módulo de negocio. La implementación con EF Core está en el proyecto Datos.</summary>
public interface IRepositorioNegocio
{
    Task<Producto?> ObtenerProductoAsync(int id);
    Task<bool> ExisteCodigoProductoAsync(string codigo);
    Task<IReadOnlyList<Producto>> ProductosAsync();
    void AgregarProducto(Producto producto);

    Task<Proveedor?> ObtenerProveedorAsync(int id);
    Task<IReadOnlyList<Proveedor>> ProveedoresAsync();
    void AgregarProveedor(Proveedor proveedor);

    /// <summary>Incluye proveedor y detalles.</summary>
    Task<OrdenCompra?> ObtenerOrdenAsync(int id);
    /// <summary>Si <paramref name="creadaPor"/> tiene valor, solo las órdenes de ese usuario.</summary>
    Task<IReadOnlyList<OrdenCompra>> OrdenesAsync(int? creadaPor);
    void AgregarOrden(OrdenCompra orden);

    Task<IReadOnlyList<MovimientoStock>> MovimientosDeProductoAsync(int productoId);
    void AgregarMovimiento(MovimientoStock movimiento);

    Task GuardarAsync();
}
