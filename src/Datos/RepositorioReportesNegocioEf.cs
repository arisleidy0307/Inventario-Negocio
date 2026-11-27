using Inventario.Negocio.Estados;
using Inventario.Negocio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Datos;

/// <summary>
/// Agregación del reporte del negocio en SQL Server (RF-REP-03): una sola consulta con GROUP BY por proveedor,
/// COUNT(DISTINCT orden), SUM(cantidad) y SUM(cantidad × costo). No se traen las órdenes al servidor.
/// </summary>
public class RepositorioReportesNegocioEf : IRepositorioReportesNegocio
{
    private readonly AppDbContext _db;

    public RepositorioReportesNegocioEf(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ComprasDeProveedor>> ComprasRecibidasPorProveedorAsync(int? creadaPor,
        DateTime? desdeUtc, DateTime? hastaUtcExclusivo)
    {
        var consulta =
            from d in _db.DetallesOrdenCompra.AsNoTracking()
            join o in _db.OrdenesCompra on d.OrdenCompraId equals o.Id
            join p in _db.Proveedores on o.ProveedorId equals p.Id
            where o.Estado == EstadoOrdenCompra.Recibida
                  && (creadaPor == null || o.CreadaPorUsuarioId == creadaPor)
                  && (desdeUtc == null || o.FechaRecepcion >= desdeUtc)
                  && (hastaUtcExclusivo == null || o.FechaRecepcion < hastaUtcExclusivo)
            group new { d.Cantidad, Monto = d.Cantidad * d.CostoUnitario, OrdenId = o.Id }
                by new { p.Id, p.Nombre } into g
            select new ComprasDeProveedor(
                g.Key.Id,
                g.Key.Nombre,
                g.Select(x => x.OrdenId).Distinct().Count(),
                g.Sum(x => x.Cantidad),
                g.Sum(x => x.Monto));

        return await consulta.ToListAsync();
    }
}
