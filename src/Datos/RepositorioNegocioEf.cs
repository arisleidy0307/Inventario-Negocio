using Inventario.Negocio.Dominio;
using Inventario.Negocio.Servicios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Datos;

/// <summary>Persistencia del módulo de negocio con EF Core: los datos sobreviven a reiniciar la aplicación (RD-09).</summary>
public class RepositorioNegocioEf : IRepositorioNegocio
{
    private readonly AppDbContext _db;

    public RepositorioNegocioEf(AppDbContext db)
    {
        _db = db;
    }

    public Task<Producto?> ObtenerProductoAsync(int id) => _db.Productos.FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExisteCodigoProductoAsync(string codigo) => _db.Productos.AnyAsync(p => p.Codigo == codigo);

    public async Task<IReadOnlyList<Producto>> ProductosAsync() =>
        await _db.Productos.AsNoTracking().OrderBy(p => p.Codigo).ToListAsync();

    public void AgregarProducto(Producto producto) => _db.Productos.Add(producto);

    public Task<Proveedor?> ObtenerProveedorAsync(int id) => _db.Proveedores.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<Proveedor>> ProveedoresAsync() =>
        await _db.Proveedores.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync();

    public void AgregarProveedor(Proveedor proveedor) => _db.Proveedores.Add(proveedor);

    public Task<OrdenCompra?> ObtenerOrdenAsync(int id) =>
        _db.OrdenesCompra.Include(o => o.Proveedor).Include(o => o.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<IReadOnlyList<OrdenCompra>> OrdenesAsync(int? creadaPor) =>
        await _db.OrdenesCompra.Include(o => o.Proveedor).Include(o => o.Detalles)
            .Where(o => creadaPor == null || o.CreadaPorUsuarioId == creadaPor)
            .OrderByDescending(o => o.FechaCreacion).ToListAsync();

    public void AgregarOrden(OrdenCompra orden) => _db.OrdenesCompra.Add(orden);

    public async Task<IReadOnlyList<MovimientoStock>> MovimientosDeProductoAsync(int productoId) =>
        await _db.MovimientosStock.AsNoTracking().Where(m => m.ProductoId == productoId)
            .OrderBy(m => m.Fecha).ToListAsync();

    public void AgregarMovimiento(MovimientoStock movimiento) => _db.MovimientosStock.Add(movimiento);

    public Task GuardarAsync() => _db.SaveChangesAsync();
}
