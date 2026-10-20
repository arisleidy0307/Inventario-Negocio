namespace Inventario.Core.Permisos;

/// <summary>Estados de una solicitud de permiso (Requerimientos del Core, pieza 2).</summary>
public enum EstadoSolicitudPermiso
{
    Pendiente = 1,
    Aprobada = 2,
    Aplicada = 3,
    Rechazada = 4
}
