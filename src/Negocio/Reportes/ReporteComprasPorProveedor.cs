using Inventario.Core.Reportes;

namespace Inventario.Negocio.Reportes;

/// <summary>Lo que devuelve la base para cada proveedor: ya agrupado y sumado.</summary>
public record ComprasDeProveedor(int ProveedorId, string Proveedor, int OrdenesRecibidas, int Unidades, decimal MontoTotal);

public record FilaComprasPorProveedor(int ProveedorId, string Proveedor, int OrdenesRecibidas, int Unidades,
    decimal MontoTotal, decimal MontoPromedioPorOrden);

public record ReporteComprasPorProveedor(RangoFechas Rango, string Alcance, string Moneda,
    IReadOnlyList<FilaComprasPorProveedor> Filas, int TotalOrdenes, int TotalUnidades, decimal MontoTotal);

/// <summary>Consulta agregada del reporte del negocio. La implementación con EF Core (GROUP BY) está en Datos.</summary>
public interface IRepositorioReportesNegocio
{
    /// <param name="creadaPor">Con valor, solo las órdenes que creó ese usuario.</param>
    /// <param name="desdeUtc">Inicio inclusivo de la fecha de recepción.</param>
    /// <param name="hastaUtcExclusivo">Fin exclusivo de la fecha de recepción.</param>
    Task<IReadOnlyList<ComprasDeProveedor>> ComprasRecibidasPorProveedorAsync(int? creadaPor, DateTime? desdeUtc,
        DateTime? hastaUtcExclusivo);
}
