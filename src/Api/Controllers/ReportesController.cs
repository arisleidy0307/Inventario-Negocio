using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Reportes;
using Inventario.Core.Seguridad;
using Inventario.Negocio.Reportes;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Reportes con agregación (RF-REP-01 a 05). Fechas opcionales AAAA-MM-DD, ambas inclusivas.</summary>
[ApiController]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly ServicioReportesCore _core;
    private readonly ServicioReportesNegocio _negocio;

    public ReportesController(ServicioReportesCore core, ServicioReportesNegocio negocio)
    {
        _core = core;
        _negocio = negocio;
    }

    /// <summary>Solicitudes de permiso por mes y estado.</summary>
    [HttpGet("solicitudes-permiso")]
    [RequiereOperacion(Operaciones.ReporteSolicitudesPermiso)]
    public async Task<ActionResult<ReporteSolicitudesResponse>> SolicitudesPermiso([FromQuery] string? desde,
        [FromQuery] string? hasta) =>
        Ok(ReporteSolicitudesResponse.De(await _core.SolicitudesPermisoAsync(User.UsuarioId(), User.Rol(),
            RangoFechas.Crear(desde, hasta))));

    /// <summary>¿Cuánto le compré a cada proveedor en el período?</summary>
    [HttpGet("compras-por-proveedor")]
    [RequiereOperacion(Operaciones.ReporteComprasPorProveedor)]
    public async Task<ActionResult<ReporteComprasResponse>> ComprasPorProveedor([FromQuery] string? desde,
        [FromQuery] string? hasta) =>
        Ok(ReporteComprasResponse.De(await _negocio.ComprasPorProveedorAsync(User.UsuarioId(), User.Rol(),
            RangoFechas.Crear(desde, hasta))));
}
