using System.Security.Claims;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Inventario.Api.Infraestructura;

/// <summary>
/// Aplica en el servidor la exigencia declarada en <see cref="PoliticasAcceso"/> (RF-CA-05, RF-CA-06, RD-06).
/// El rol se toma de la sesión validada contra la base de datos, nunca de datos enviados por el cliente.
/// Sin sesión → 401. Con sesión pero sin el rol → 403 explícito.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiereOperacionAttribute : Attribute, IAuthorizationFilter
{
    public string Operacion { get; }

    public RequiereOperacionAttribute(string operacion)
    {
        Operacion = operacion;
    }

    public void OnAuthorization(AuthorizationFilterContext contexto)
    {
        var exigencia = PoliticasAcceso.Obtener(Operacion);
        var usuario = contexto.HttpContext.User;
        var autenticado = usuario.Identity?.IsAuthenticated == true;
        Rol? rol = Enum.TryParse<Rol>(usuario.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;

        if (exigencia.Permite(autenticado, rol))
            return;

        contexto.Result = autenticado ? new ForbidResult() : new ChallengeResult();
    }
}
