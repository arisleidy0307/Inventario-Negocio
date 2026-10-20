using System.Security.Claims;
using System.Text.Encodings.Web;
using Inventario.Core.Permisos;
using Inventario.Core.Servicios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inventario.Api.Infraestructura;

/// <summary>
/// Esquema "Bearer" con credencial opaca guardada en BD. Cada petición consulta la sesión y el usuario,
/// así que el rol sale de la base de datos y no de lo que envíe el cliente.
/// </summary>
public class AutenticacionSesion : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "Bearer";
    public const string ClaimSesion = "sesion_id";
    public const string ClaimPermiso = "permiso";

    private readonly ServicioSesion _sesiones;
    private readonly IRepositorioPermisos _permisos;

    public AutenticacionSesion(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, ServicioSesion sesiones, IRepositorioPermisos permisos) : base(options, logger, encoder)
    {
        _sesiones = sesiones;
        _permisos = permisos;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var encabezado = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(encabezado) || !encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = encabezado["Bearer ".Length..].Trim();
        var validada = await _sesiones.ValidarAsync(token);
        if (validada is null)
            return AuthenticateResult.Fail("Sesión no válida.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validada.Usuario.Id.ToString()),
            new(ClaimTypes.Email, validada.Usuario.Correo),
            new(ClaimTypes.Role, validada.Usuario.Rol.ToString()),
            new(ClaimSesion, validada.Sesion.Id.ToString())
        };
        // Permisos otorgados por solicitudes aprobadas, leídos de la BD en cada petición (RF-GP-05).
        foreach (var operacion in await _permisos.PermisosDeUsuarioAsync(validada.Usuario.Id))
            claims.Add(new Claim(ClaimPermiso, operacion));
        var identidad = new ClaimsIdentity(claims, Esquema);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 401,
            Title = "Se requiere una sesión válida. Inicia sesión y envía la credencial en el encabezado Authorization: Bearer <token>."
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 403,
            Title = "No tienes permiso para realizar esta operación."
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

public static class ExtensionesUsuario
{
    public static int UsuarioId(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static int SesionId(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue(AutenticacionSesion.ClaimSesion)!);

    public static Inventario.Core.Dominio.Rol Rol(this ClaimsPrincipal u) =>
        Enum.Parse<Inventario.Core.Dominio.Rol>(u.FindFirstValue(ClaimTypes.Role)!);
}
