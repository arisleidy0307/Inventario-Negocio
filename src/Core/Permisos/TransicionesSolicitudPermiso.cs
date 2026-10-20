using Inventario.Core.Comun;

namespace Inventario.Core.Permisos;

/// <summary>
/// RD-04: ÚNICO punto del código donde se resuelven las transiciones de una solicitud de permiso.
/// Solo existen Pendiente → Aprobada → Aplicada y Pendiente → Rechazada (RF-GP-06).
/// Cualquier otra (aprobar dos veces, Rechazada → Aplicada, etc.) está prohibida.
/// Esta máquina es independiente de la de OrdenCompra del módulo de negocio (RF-NEG-09).
/// </summary>
public static class TransicionesSolicitudPermiso
{
    private static readonly IReadOnlyDictionary<EstadoSolicitudPermiso, EstadoSolicitudPermiso[]> Permitidas =
        new Dictionary<EstadoSolicitudPermiso, EstadoSolicitudPermiso[]>
        {
            [EstadoSolicitudPermiso.Pendiente] = new[] { EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada },
            [EstadoSolicitudPermiso.Aprobada]  = new[] { EstadoSolicitudPermiso.Aplicada },
            [EstadoSolicitudPermiso.Aplicada]  = Array.Empty<EstadoSolicitudPermiso>(), // terminal
            [EstadoSolicitudPermiso.Rechazada] = Array.Empty<EstadoSolicitudPermiso>(), // terminal
        };

    public static bool Puede(EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia) =>
        Permitidas.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Lanza un rechazo controlado (409) si la transición no está en la tabla.</summary>
    public static void Validar(EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia)
    {
        if (!Puede(desde, hacia))
            throw ExcepcionControlada.Conflicto(
                $"Transición no permitida: la solicitud está {desde} y no puede pasar a {hacia}.");
    }
}
