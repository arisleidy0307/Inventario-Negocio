using Inventario.Core.Servicios;

namespace Inventario.Api.Contratos;

public record CorreoEnColaResponse(int Id, string Destinatario, string Asunto, string Estado, int Intentos,
    DateTime FechaCreacion, DateTime? FechaUltimoIntento, DateTime? FechaEnvio, string? UltimoError)
{
    public static CorreoEnColaResponse Desde(EstadoCorreoEnCola c) => new(c.Id, c.Destinatario, c.Asunto,
        c.Estado.ToString(), c.Intentos, c.FechaCreacion, c.FechaUltimoIntento, c.FechaEnvio, c.UltimoError);
}
