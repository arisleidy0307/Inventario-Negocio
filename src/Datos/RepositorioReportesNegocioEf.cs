using Inventario.Negocio.Estados;
using Inventario.Negocio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Datos;

/// <summary>
/// Agregación del reporte del negocio en SQL Server (RF-REP-03): una sola consulta con GROUP BY por proveedor,
/// COUNT(DISTINCT orden), SUM(cantidad) y SUM(cantidad × costo). No se traen las órdenes al servidor.
/// El monto se suma en centavos (entero): los costos tienen 2 decimales, así la suma es exacta y la misma
/// consulta también funciona en SQLite, que no suma valores decimal.
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
            group new { d.Cantidad, Centavos = d.Cantidad * (long)(d.CostoUnitario * 100), OrdenId = o.Id }
                by new { p.Id, p.Nombre } into g
            select new ComprasDeProveedor(
                g.Key.Id,
                g.Key.Nombre,
                g.Select(x => x.OrdenId).Distinct().Count(),
                g.Sum(x => x.Cantidad),
                g.Sum(x => x.Centavos));  // en centavos; se pasa a pesos al volver de la base

        var filas = await consulta.ToListAsync();
        return filas.Select(f => f with { MontoTotal = f.MontoTotal / 100m }).ToList();
    }
}
