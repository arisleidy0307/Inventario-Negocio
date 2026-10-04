using Inventario.Api.Contratos;
using Inventario.Core.Servicios;
using Inventario.Api.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Controlador delgado (RD-02): recibe, llama al servicio y devuelve.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ServicioRegistro _registro;
    private readonly ServicioSesion _sesion;

    public AuthController(ServicioRegistro registro, ServicioSesion sesion)
    {
        _registro = registro;
        _sesion = sesion;
    }

    /// <summary>RF-CA-01, 02, 14, 15: registra un usuario inactivo y encola el correo de activación.</summary>
    [HttpPost("registro")]
    public async Task<IActionResult> Registrar(RegistroRequest req)
    {
        await _registro.RegistrarAsync(req.Nombre, req.Correo, req.Contrasena);
        return StatusCode(StatusCodes.Status201Created,
            new MensajeResponse("Cuenta creada. Revisa tu correo para activarla antes de iniciar sesión."));
    }

    /// <summary>RF-CA-16: enlace que llega por correo. Responde HTML porque se abre en el navegador.</summary>
    [HttpGet("activar")]
    public async Task<IActionResult> Activar([FromQuery] string? token)
    {
        try
        {
            await _registro.ActivarAsync(token);
            return Pagina(200, "Cuenta activada", "Tu cuenta quedó activa. Ya puedes iniciar sesión.");
        }
        catch (Inventario.Core.Comun.ExcepcionControlada ex)
        {
            return Pagina(ex.CodigoEstado, "No se pudo activar la cuenta", ex.Message);
        }
    }

    /// <summary>RF-CA-17: misma respuesta exista o no el correo.</summary>
    [HttpPost("reenviar-activacion")]
    public async Task<IActionResult> ReenviarActivacion(CorreoRequest req)
    {
        await _registro.ReenviarActivacionAsync(req.Correo);
        return Ok(new MensajeResponse(ServicioRegistro.MensajeReenvio));
    }

    /// <summary>RF-CA-03 / RF-CA-19: entrega la credencial de sesión.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var s = await _sesion.IniciarSesionAsync(req.Correo, req.Contrasena);
        return Ok(new LoginResponse(s.Token, "Bearer", s.Vence,
            new UsuarioActualResponse(s.Usuario.Id, s.Usuario.Nombre, s.Usuario.Correo, s.Usuario.Rol.ToString())));
    }

    /// <summary>RF-CA-07: usuario autenticado y su rol.</summary>
    [HttpGet("yo")]
    [Authorize]
    public async Task<ActionResult<UsuarioActualResponse>> Yo()
    {
        var u = await _sesion.ObtenerUsuarioAsync(User.UsuarioId());
        return Ok(new UsuarioActualResponse(u.Id, u.Nombre, u.Correo, u.Rol.ToString()));
    }

    /// <summary>RF-CA-18: invalida la credencial usada en la petición.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _sesion.CerrarSesionAsync(User.SesionId());
        return Ok(new MensajeResponse("Sesión cerrada. La credencial ya no es válida."));
    }

    private ContentResult Pagina(int estado, string titulo, string mensaje) => new()
    {
        StatusCode = estado,
        ContentType = "text/html; charset=utf-8",
        Content = $"<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>{titulo}</title>" +
                  $"<body style=\"font-family:sans-serif;max-width:560px;margin:60px auto\"><h1>{titulo}</h1>" +
                  $"<p>{System.Net.WebUtility.HtmlEncode(mensaje)}</p></body></html>"
    };
}
