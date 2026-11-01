using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Seguridad;
using Inventario.Negocio.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
public class ProveedoresController : ControllerBase
{
    private readonly ServicioCatalogo _catalogo;

    public ProveedoresController(ServicioCatalogo catalogo)
    {
        _catalogo = catalogo;
    }

    [HttpPost]
    [RequiereOperacion(Operaciones.CrearProveedor)]
    public async Task<ActionResult<ProveedorResponse>> Crear(CrearProveedorRequest req)
    {
        var p = await _catalogo.CrearProveedorAsync(req.Nombre, req.Correo, req.Telefono);
        return StatusCode(StatusCodes.Status201Created, ProveedorResponse.Desde(p));
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.ListarProveedores)]
    public async Task<ActionResult<IEnumerable<ProveedorResponse>>> Listar() =>
        Ok((await _catalogo.ProveedoresAsync()).Select(ProveedorResponse.Desde));
}
