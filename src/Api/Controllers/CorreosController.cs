using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Seguridad;
using Inventario.Core.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Estado de la cola de correos (RF-NOT-11). Solo Administrador.</summary>
[ApiController]
[Route("api/correos")]
public class CorreosController : ControllerBase
{
    private readonly ServicioConsultaCorreos _correos;

    public CorreosController(ServicioConsultaCorreos correos)
    {
        _correos = correos;
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.ListarCorreos)]
    public async Task<ActionResult<IEnumerable<CorreoEnColaResponse>>> Listar([FromQuery] string? estado) =>
        Ok((await _correos.ListarAsync(estado)).Select(CorreoEnColaResponse.Desde));
}
