using Inventario.Core.Reportes;
using Inventario.Negocio.Reportes;

namespace Inventario.Api.Contratos;

public record ReporteSolicitudesResponse(string? Desde, string? Hasta, string Alcance, int Total,
    IEnumerable<TotalSolicitudesPorEstado> PorEstado, IEnumerable<FilaSolicitudesPorMes> Filas)
{
    public static ReporteSolicitudesResponse De(ReporteSolicitudesPermiso r) => new(Texto(r.Rango.Desde),
        Texto(r.Rango.Hasta), r.Alcance, r.Total, r.PorEstado, r.Filas);

    internal static string? Texto(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd");
}

public record ReporteComprasResponse(string Pregunta, string? Desde, string? Hasta, string Alcance, string Moneda,
    int TotalOrdenes, int TotalUnidades, decimal MontoTotal, IEnumerable<FilaComprasPorProveedor> Filas)
{
    public const string PreguntaDelReporte = "¿Cuánto le compré a cada proveedor en el período? (órdenes recibidas)";

    public static ReporteComprasResponse De(ReporteComprasPorProveedor r) => new(PreguntaDelReporte,
        ReporteSolicitudesResponse.Texto(r.Rango.Desde), ReporteSolicitudesResponse.Texto(r.Rango.Hasta), r.Alcance,
        r.Moneda, r.TotalOrdenes, r.TotalUnidades, r.MontoTotal, r.Filas);
}
