using Inventario.Core.Dominio;
using Inventario.Core.Reportes;

namespace Inventario.Negocio.Reportes;

/// <summary>
/// Reporte del negocio (RF-REP-02): «¿cuánto le compré a cada proveedor en un período?». Solo cuentan las órdenes
/// Recibidas, por su fecha de recepción. Por proveedor: órdenes, unidades, monto y monto promedio por orden.
/// RF-REP-04: un Estándar solo ve las órdenes que él creó.
/// </summary>
public class ServicioReportesNegocio
{
    public const string Moneda = "DOP";

    private readonly IRepositorioReportesNegocio _repo;

    public ServicioReportesNegocio(IRepositorioReportesNegocio repo)
    {
        _repo = repo;
    }

    public async Task<ReporteComprasPorProveedor> ComprasPorProveedorAsync(int actorId, Rol rolActor, RangoFechas rango)
    {
        int? soloDe = rolActor == Rol.Administrador ? null : actorId;
        var compras = await _repo.ComprasRecibidasPorProveedorAsync(soloDe, rango.DesdeUtc, rango.HastaUtcExclusivo);

        var filas = compras
            .OrderByDescending(c => c.MontoTotal).ThenBy(c => c.Proveedor)
            .Select(c => new FilaComprasPorProveedor(c.ProveedorId, c.Proveedor, c.OrdenesRecibidas, c.Unidades,
                c.MontoTotal,
                c.OrdenesRecibidas == 0 ? 0 : Math.Round(c.MontoTotal / c.OrdenesRecibidas, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new ReporteComprasPorProveedor(rango, ServicioReportesCore.Alcance(soloDe), Moneda, filas,
            filas.Sum(f => f.OrdenesRecibidas), filas.Sum(f => f.Unidades), filas.Sum(f => f.MontoTotal));
    }
}
