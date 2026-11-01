using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Seguridad;
using Inventario.Negocio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/productos")]
public class ProductosController : ControllerBase
{
    private readonly ServicioCatalogo _catalogo;

    public ProductosController(ServicioCatalogo catalogo)
    {
        _catalogo = catalogo;
    }

    [HttpPost]
    [RequiereOperacion(Operaciones.CrearProducto)]
    public async Task<ActionResult<ProductoResponse>> Crear(CrearProductoRequest req)
    {
        var p = await _catalogo.CrearProductoAsync(req.Codigo, req.Nombre, req.PrecioCosto);
        return StatusCode(StatusCodes.Status201Created, ProductoResponse.Desde(p));
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.ListarProductos)]
    public async Task<ActionResult<IEnumerable<ProductoResponse>>> Listar() =>
        Ok((await _catalogo.ProductosAsync()).Select(ProductoResponse.Desde));

    [HttpGet("{id:int}/movimientos")]
    [RequiereOperacion(Operaciones.MovimientosProducto)]
    public async Task<ActionResult<IEnumerable<MovimientoResponse>>> Movimientos(int id) =>
        Ok((await _catalogo.MovimientosAsync(id)).Select(MovimientoResponse.Desde));
}
