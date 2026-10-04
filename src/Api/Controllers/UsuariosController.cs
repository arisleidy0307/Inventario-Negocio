using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Seguridad;
using Inventario.Core.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Administración de usuarios. La exigencia de rol de cada acción está en PoliticasAcceso.</summary>
[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly ServicioUsuarios _usuarios;
    private readonly ServicioContrasenas _contrasenas;

    public UsuariosController(ServicioUsuarios usuarios, ServicioContrasenas contrasenas)
    {
        _usuarios = usuarios;
        _contrasenas = contrasenas;
    }

    /// <summary>RF-CA-21: listado sin datos sensibles.</summary>
    [HttpGet]
    [RequiereOperacion(Operaciones.ListarUsuarios)]
    public async Task<ActionResult<IReadOnlyList<UsuarioResumen>>> Listar() => Ok(await _usuarios.ListarAsync());

    /// <summary>RF-CA-08: cambio de rol.</summary>
    [HttpPut("{id:int}/rol")]
    [RequiereOperacion(Operaciones.CambiarRol)]
    public async Task<IActionResult> CambiarRol(int id, CambiarRolRequest req)
    {
        await _usuarios.CambiarRolAsync(User.UsuarioId(), id, req.Rol);
        return Ok(new MensajeResponse("Rol actualizado."));
    }

    /// <summary>RF-CA-20: desactiva al usuario e invalida sus sesiones.</summary>
    [HttpPost("{id:int}/desactivar")]
    [RequiereOperacion(Operaciones.Desactivar)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await _usuarios.DesactivarAsync(User.UsuarioId(), id);
        return Ok(new MensajeResponse("Usuario desactivado. Sus sesiones abiertas dejaron de ser válidas."));
    }

    /// <summary>RF-CA-20: reactiva al usuario.</summary>
    [HttpPost("{id:int}/reactivar")]
    [RequiereOperacion(Operaciones.Reactivar)]
    public async Task<IActionResult> Reactivar(int id)
    {
        await _usuarios.ReactivarAsync(id);
        return Ok(new MensajeResponse("Usuario reactivado."));
    }

    /// <summary>RF-CA-13: fuerza el restablecimiento y encola el correo con el código.</summary>
    [HttpPost("{id:int}/forzar-restablecimiento")]
    [RequiereOperacion(Operaciones.ForzarRestablecimiento)]
    public async Task<IActionResult> ForzarRestablecimiento(int id)
    {
        await _contrasenas.ForzarRestablecimientoAsync(id);
        return Ok(new MensajeResponse("Contraseña restablecida. El usuario recibirá un código por correo para definir una nueva."));
    }
}
