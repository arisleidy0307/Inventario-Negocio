using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Notificaciones;
using Inventario.Core.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Bandeja de notificaciones del usuario con sesión (RF-NOT-04, 05, 07). Siempre las propias.</summary>
[ApiController]
[Route("api/notificaciones")]
public class NotificacionesController : ControllerBase
{
    private readonly ServicioNotificaciones _notificaciones;

    public NotificacionesController(ServicioNotificaciones notificaciones)
    {
        _notificaciones = notificaciones;
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.MisNotificaciones)]
    public async Task<ActionResult<BandejaResponse>> Listar([FromQuery] bool soloNoLeidas = false)
    {
        var bandeja = await _notificaciones.BandejaAsync(User.UsuarioId(), soloNoLeidas);
        return Ok(new BandejaResponse(bandeja.NoLeidas, bandeja.Notificaciones.Select(NotificacionResponse.Desde)));
    }

    [HttpPost("{id:int}/leer")]
    [RequiereOperacion(Operaciones.MarcarNotificacionLeida)]
    public async Task<ActionResult<NotificacionResponse>> MarcarLeida(int id) =>
        Ok(NotificacionResponse.Desde(await _notificaciones.MarcarLeidaAsync(id, User.UsuarioId())));

    [HttpGet("preferencias")]
    [RequiereOperacion(Operaciones.PreferenciasNotificaciones)]
    public async Task<ActionResult<PreferenciasNotificacionesResponse>> Preferencias() =>
        Ok(new PreferenciasNotificacionesResponse(await _notificaciones.RecibeCorreoAsync(User.UsuarioId())));

    [HttpPut("preferencias")]
    [RequiereOperacion(Operaciones.PreferenciasNotificaciones)]
    public async Task<ActionResult<PreferenciasNotificacionesResponse>> CambiarPreferencias(PreferenciasNotificacionesRequest req) =>
        Ok(new PreferenciasNotificacionesResponse(await _notificaciones.CambiarCorreoAsync(User.UsuarioId(), req.Correo)));
}
