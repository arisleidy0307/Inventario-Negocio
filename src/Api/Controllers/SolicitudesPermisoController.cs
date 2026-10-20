using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Permisos;
using Inventario.Core.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Gestión de permisos (RF-GP-01 a 07). Controlador delgado: las reglas viven en ServicioPermisos.</summary>
[ApiController]
[Route("api/solicitudes-permiso")]
public class SolicitudesPermisoController : ControllerBase
{
    private readonly ServicioPermisos _permisos;

    public SolicitudesPermisoController(ServicioPermisos permisos)
    {
        _permisos = permisos;
    }

    /// <summary>Operaciones que se pueden solicitar.</summary>
    [HttpGet("operaciones")]
    [RequiereOperacion(Operaciones.CatalogoPermisos)]
    public ActionResult<IReadOnlyList<string>> Catalogo() => Ok(PoliticasAcceso.OperacionesOtorgables);

    /// <summary>RF-GP-01: crea una solicitud; nace en estado Pendiente.</summary>
    [HttpPost]
    [RequiereOperacion(Operaciones.SolicitarPermiso)]
    public async Task<ActionResult<SolicitudPermisoResponse>> Crear(CrearSolicitudPermisoRequest req)
    {
        var s = await _permisos.CrearSolicitudAsync(User.UsuarioId(), req.Operacion, req.Justificacion);
        return CreatedAtAction(nameof(Obtener), new { id = s.Id }, SolicitudPermisoResponse.Desde(s));
    }

    /// <summary>Solicitudes del usuario autenticado.</summary>
    [HttpGet("mias")]
    [RequiereOperacion(Operaciones.MisSolicitudes)]
    public async Task<ActionResult<IEnumerable<SolicitudPermisoResponse>>> Mias() =>
        Ok((await _permisos.DeUsuarioAsync(User.UsuarioId())).Select(SolicitudPermisoResponse.Desde));

    /// <summary>RF-GP-02: solicitudes pendientes, solo Administrador.</summary>
    [HttpGet("pendientes")]
    [RequiereOperacion(Operaciones.ListarSolicitudesPendientes)]
    public async Task<ActionResult<IEnumerable<SolicitudPermisoResponse>>> Pendientes() =>
        Ok((await _permisos.PendientesAsync()).Select(SolicitudPermisoResponse.Desde));

    /// <summary>RF-GP-02: un Estándar solo ve las suyas.</summary>
    [HttpGet("{id:int}")]
    [RequiereOperacion(Operaciones.VerSolicitud)]
    public async Task<ActionResult<SolicitudPermisoResponse>> Obtener(int id) =>
        Ok(SolicitudPermisoResponse.Desde(await _permisos.ObtenerAsync(id, User.UsuarioId(), User.Rol())));

    /// <summary>RF-GP-03 / RF-GP-05 / RF-GP-06 / RF-GP-07: aprueba y aplica el permiso.</summary>
    [HttpPost("{id:int}/aprobar")]
    [RequiereOperacion(Operaciones.AprobarSolicitud)]
    public async Task<ActionResult<SolicitudPermisoResponse>> Aprobar(int id) =>
        Ok(SolicitudPermisoResponse.Desde(await _permisos.AprobarAsync(id, User.UsuarioId())));

    /// <summary>RF-GP-04 / RF-GP-06 / RF-GP-07: rechaza con motivo obligatorio.</summary>
    [HttpPost("{id:int}/rechazar")]
    [RequiereOperacion(Operaciones.RechazarSolicitud)]
    public async Task<ActionResult<SolicitudPermisoResponse>> Rechazar(int id, RechazarSolicitudRequest req) =>
        Ok(SolicitudPermisoResponse.Desde(await _permisos.RechazarAsync(id, User.UsuarioId(), req.Motivo)));
}
