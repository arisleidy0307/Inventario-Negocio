using Inventario.Core.Comun;
using Inventario.Core.Dominio;

namespace Inventario.Core.Permisos;

/// <summary>
/// Solicitud de un usuario para que se le otorgue permiso de ejecutar una operación.
/// El estado solo cambia mediante los métodos de abajo, y todos consultan
/// <see cref="TransicionesSolicitudPermiso"/> ANTES de modificar algo: si la transición
/// está prohibida se lanza el rechazo y la solicitud queda exactamente igual (RF-GP-06).
/// </summary>
public class SolicitudPermiso
{
    public const int LongitudMaximaTexto = 500;

    public int Id { get; set; }
    public int SolicitanteId { get; set; }
    public Usuario? Solicitante { get; set; }

    /// <summary>Operación de <c>PoliticasAcceso</c> que el solicitante quiere poder ejecutar.</summary>
    public string Operacion { get; set; } = string.Empty;
    public string? Justificacion { get; set; }

    public EstadoSolicitudPermiso Estado { get; private set; } = EstadoSolicitudPermiso.Pendiente;
    public DateTime FechaSolicitud { get; set; }

    /// <summary>RF-GP-03: quién aprobó y cuándo.</summary>
    public int? AprobadaPorId { get; private set; }
    public DateTime? FechaAprobacion { get; private set; }

    /// <summary>RF-GP-04: quién rechazó, cuándo y por qué.</summary>
    public int? RechazadaPorId { get; private set; }
    public DateTime? FechaRechazo { get; private set; }
    public string? MotivoRechazo { get; private set; }

    /// <summary>RF-GP-05: cuándo se aplicó el permiso.</summary>
    public DateTime? FechaAplicacion { get; private set; }

    /// <summary>Pendiente → Aprobada. Registra quién aprobó y cuándo (RF-GP-03).</summary>
    public void Aprobar(int administradorId, DateTime ahora)
    {
        TransicionesSolicitudPermiso.Validar(Estado, EstadoSolicitudPermiso.Aprobada);
        Estado = EstadoSolicitudPermiso.Aprobada;
        AprobadaPorId = administradorId;
        FechaAprobacion = ahora;
    }

    /// <summary>Pendiente → Rechazada. Exige un motivo escrito (RF-GP-04).</summary>
    public void Rechazar(int administradorId, string? motivo, DateTime ahora)
    {
        var motivoLimpio = (motivo ?? string.Empty).Trim();
        if (motivoLimpio.Length == 0)
            throw ExcepcionControlada.Solicitud("Para rechazar una solicitud debes escribir el motivo.");
        if (motivoLimpio.Length > LongitudMaximaTexto)
            throw ExcepcionControlada.Solicitud($"El motivo admite hasta {LongitudMaximaTexto} caracteres.");

        TransicionesSolicitudPermiso.Validar(Estado, EstadoSolicitudPermiso.Rechazada);
        Estado = EstadoSolicitudPermiso.Rechazada;
        RechazadaPorId = administradorId;
        FechaRechazo = ahora;
        MotivoRechazo = motivoLimpio;
    }

    /// <summary>Aprobada → Aplicada. La ejecuta el sistema justo después de aprobar (RF-GP-05).</summary>
    public void Aplicar(DateTime ahora)
    {
        TransicionesSolicitudPermiso.Validar(Estado, EstadoSolicitudPermiso.Aplicada);
        Estado = EstadoSolicitudPermiso.Aplicada;
        FechaAplicacion = ahora;
    }
}
