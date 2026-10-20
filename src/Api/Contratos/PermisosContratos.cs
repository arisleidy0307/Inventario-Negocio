using System.ComponentModel.DataAnnotations;
using Inventario.Core.Permisos;

namespace Inventario.Api.Contratos;

public record CrearSolicitudPermisoRequest(
    [Required(ErrorMessage = "La operación es obligatoria.")]
    [StringLength(100, ErrorMessage = "La operación admite hasta 100 caracteres.")]
    string Operacion,
    [StringLength(SolicitudPermiso.LongitudMaximaTexto, ErrorMessage = "La justificación admite hasta 500 caracteres.")]
    string? Justificacion);

/// <summary>El motivo se valida en el dominio para que "sin motivo" y "solo espacios" den el mismo rechazo (RF-GP-04).</summary>
public record RechazarSolicitudRequest(
    [StringLength(SolicitudPermiso.LongitudMaximaTexto, ErrorMessage = "El motivo admite hasta 500 caracteres.")]
    string? Motivo);

public record SolicitudPermisoResponse(
    int Id, int SolicitanteId, string? SolicitanteCorreo, string Operacion, string? Justificacion, string Estado,
    DateTime FechaSolicitud, int? AprobadaPorId, DateTime? FechaAprobacion, int? RechazadaPorId,
    DateTime? FechaRechazo, string? MotivoRechazo, DateTime? FechaAplicacion)
{
    public static SolicitudPermisoResponse Desde(SolicitudPermiso s) => new(
        s.Id, s.SolicitanteId, s.Solicitante?.Correo, s.Operacion, s.Justificacion, s.Estado.ToString(),
        s.FechaSolicitud, s.AprobadaPorId, s.FechaAprobacion, s.RechazadaPorId, s.FechaRechazo,
        s.MotivoRechazo, s.FechaAplicacion);
}
