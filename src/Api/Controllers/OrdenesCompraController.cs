using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Seguridad;
using Inventario.Negocio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Órdenes de compra. La exigencia de rol de cada acción está en PoliticasAcceso (RF-NEG-06).</summary>
[ApiController]
[Route("api/ordenes-compra")]
public class OrdenesCompraController : ControllerBase
{
    private readonly ServicioOrdenesCompra _ordenes;

    public OrdenesCompraController(ServicioOrdenesCompra ordenes)
    {
        _ordenes = ordenes;
    }

    [HttpPost]
    [RequiereOperacion(Operaciones.CrearOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> Crear(CrearOrdenRequest req)
    {
        var o = await _ordenes.CrearAsync(User.UsuarioId(), req.ProveedorId);
        return CreatedAtAction(nameof(Obtener), new { id = o.Id }, OrdenCompraResponse.Desde(o));
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.ListarOrdenes)]
    public async Task<ActionResult<IEnumerable<OrdenCompraResponse>>> Listar() =>
        Ok((await _ordenes.ListarAsync(User.UsuarioId(), User.Rol())).Select(OrdenCompraResponse.Desde));

    [HttpGet("{id:int}")]
    [RequiereOperacion(Operaciones.VerOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> Obtener(int id) =>
        Ok(OrdenCompraResponse.Desde(await _ordenes.ObtenerAsync(id, User.UsuarioId(), User.Rol())));

    [HttpPost("{id:int}/detalles")]
    [RequiereOperacion(Operaciones.AgregarDetalleOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> AgregarDetalle(int id, AgregarDetalleRequest req) =>
        Ok(OrdenCompraResponse.Desde(await _ordenes.AgregarDetalleAsync(
            id, User.UsuarioId(), User.Rol(), req.ProductoId, req.Cantidad, req.CostoUnitario)));

    [HttpPost("{id:int}/emitir")]
    [RequiereOperacion(Operaciones.EmitirOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> Emitir(int id) =>
        Ok(OrdenCompraResponse.Desde(await _ordenes.EmitirAsync(id)));

    [HttpPost("{id:int}/recibir")]
    [RequiereOperacion(Operaciones.RecibirOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> Recibir(int id) =>
        Ok(OrdenCompraResponse.Desde(await _ordenes.RecibirAsync(id, User.UsuarioId(), User.Rol())));

    [HttpPost("{id:int}/cancelar")]
    [RequiereOperacion(Operaciones.CancelarOrden)]
    public async Task<ActionResult<OrdenCompraResponse>> Cancelar(int id, CancelarOrdenRequest req) =>
        Ok(OrdenCompraResponse.Desde(await _ordenes.CancelarAsync(id, req.Motivo)));

    /// <summary>Cotiza el total de la orden (DOP) en otra moneda con la tasa del día de ExchangeRate-API.</summary>
    [HttpGet("{id:int}/cotizacion")]
    [RequiereOperacion(Operaciones.CotizarOrden)]
    public async Task<ActionResult<CotizacionOrdenResponse>> Cotizar(int id, [FromQuery] string? moneda,
        [FromServices] ServicioCotizacionOrdenes cotizaciones) =>
        Ok(CotizacionOrdenResponse.Desde(await cotizaciones.CotizarAsync(id, User.UsuarioId(), User.Rol(), moneda ?? "USD")));
}
