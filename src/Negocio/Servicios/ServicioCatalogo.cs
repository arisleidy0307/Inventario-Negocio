using Inventario.Core.Comun;
using Inventario.Negocio.Dominio;

namespace Inventario.Negocio.Servicios;

/// <summary>Productos, proveedores y movimientos de stock.</summary>
public class ServicioCatalogo
{
    private readonly IRepositorioNegocio _repo;

    public ServicioCatalogo(IRepositorioNegocio repo)
    {
        _repo = repo;
    }

    public async Task<Producto> CrearProductoAsync(string? codigo, string? nombre, decimal precioCosto)
    {
        var cod = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nom = (nombre ?? string.Empty).Trim();
        if (cod.Length is 0 or > 30)
            throw ExcepcionControlada.Solicitud("El código del producto es obligatorio y admite hasta 30 caracteres.");
        if (nom.Length is 0 or > 150)
            throw ExcepcionControlada.Solicitud("El nombre del producto es obligatorio y admite hasta 150 caracteres.");
        if (precioCosto < 0)
            throw ExcepcionControlada.Solicitud("El precio de costo no puede ser negativo.");
        if (await _repo.ExisteCodigoProductoAsync(cod))
            throw ExcepcionControlada.Conflicto("Ya existe un producto con ese código.");

        var producto = new Producto { Codigo = cod, Nombre = nom, PrecioCosto = precioCosto, Stock = 0, Activo = true };
        _repo.AgregarProducto(producto);
        await _repo.GuardarAsync();
        return producto;
    }

    public Task<IReadOnlyList<Producto>> ProductosAsync() => _repo.ProductosAsync();

    public async Task<Proveedor> CrearProveedorAsync(string? nombre, string? correo, string? telefono)
    {
        var nom = (nombre ?? string.Empty).Trim();
        if (nom.Length is 0 or > 150)
            throw ExcepcionControlada.Solicitud("El nombre del proveedor es obligatorio y admite hasta 150 caracteres.");

        var proveedor = new Proveedor
        {
            Nombre = nom,
            Correo = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim(),
            Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim()
        };
        _repo.AgregarProveedor(proveedor);
        await _repo.GuardarAsync();
        return proveedor;
    }

    public Task<IReadOnlyList<Proveedor>> ProveedoresAsync() => _repo.ProveedoresAsync();

    public async Task<IReadOnlyList<MovimientoStock>> MovimientosAsync(int productoId)
    {
        _ = await _repo.ObtenerProductoAsync(productoId)
            ?? throw ExcepcionControlada.NoEncontrado("No existe un producto con ese identificador.");
        return await _repo.MovimientosDeProductoAsync(productoId);
    }
}
