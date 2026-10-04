using Inventario.Api.Contratos;
using Inventario.Core.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Controlador delgado (RD-02): recibe, llama al servicio y devuelve.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ServicioRegistro _registro;

    public AuthController(ServicioRegistro registro)
    {
        _registro = registro;
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

    private ContentResult Pagina(int estado, string titulo, string mensaje) => new()
    {
        StatusCode = estado,
        ContentType = "text/html; charset=utf-8",
        Content = $"<!doctype html><html lang=\"es\"><meta charset=\"utf-8\"><title>{titulo}</title>" +
                  $"<body style=\"font-family:sans-serif;max-width:560px;margin:60px auto\"><h1>{titulo}</h1>" +
                  $"<p>{System.Net.WebUtility.HtmlEncode(mensaje)}</p></body></html>"
    };
}
