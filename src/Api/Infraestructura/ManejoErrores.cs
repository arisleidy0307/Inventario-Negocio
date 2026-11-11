using Inventario.Core.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Infraestructura;

/// <summary>
/// RD-08: ninguna respuesta expone trazas, rutas ni consultas.
/// Los errores previstos (ExcepcionControlada) salen con su código y mensaje;
/// cualquier otro error sale como un 500 genérico.
/// </summary>
public static class ManejoErrores
{
    public static void UsarManejoErrores(this WebApplication app)
    {
        app.UseExceptionHandler(builder => builder.Run(async contexto =>
        {
            var error = contexto.Features.Get<IExceptionHandlerFeature>()?.Error;
            ProblemDetails problema;

            if (error is ExcepcionControlada controlada)
            {
                problema = new ProblemDetails { Status = controlada.CodigoEstado, Title = controlada.Message };
            }
            else if (error is BadHttpRequestException peticionInvalida)
            {
                // Por ejemplo, un archivo más grande de lo que el servidor acepta leer (RD-07).
                problema = new ProblemDetails
                {
                    Status = peticionInvalida.StatusCode,
                    Title = peticionInvalida.StatusCode == StatusCodes.Status413PayloadTooLarge
                        ? "La petición es demasiado grande."
                        : "La petición no es válida."
                };
            }
            else
            {
                var logger = contexto.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ErrorNoControlado");
                logger.LogError(error, "Error no controlado");
                problema = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Ocurrió un error inesperado. Intenta de nuevo más tarde."
                };
            }

            contexto.Response.StatusCode = problema.Status!.Value;
            await contexto.Response.WriteAsJsonAsync(problema, (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json");
        }));
    }
}
