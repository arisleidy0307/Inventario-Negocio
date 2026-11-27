using Inventario.Core.Notificaciones;

namespace Inventario.Api.Contratos;

public record NotificacionResponse(int Id, string Tipo, string Titulo, string Mensaje, DateTime Fecha, bool Leida,
    DateTime? FechaLectura)
{
    public static NotificacionResponse Desde(Notificacion n) =>
        new(n.Id, n.Tipo.ToString(), n.Titulo, n.Mensaje, n.FechaCreacion, n.Leida, n.FechaLectura);
}

public record BandejaResponse(int NoLeidas, IEnumerable<NotificacionResponse> Notificaciones);

/// <summary>RF-NOT-07: <c>correo</c> en false apaga el canal de correo; la bandeja interna sigue.</summary>
public record PreferenciasNotificacionesRequest(bool Correo);

public record PreferenciasNotificacionesResponse(bool Correo, bool Bandeja = true);
