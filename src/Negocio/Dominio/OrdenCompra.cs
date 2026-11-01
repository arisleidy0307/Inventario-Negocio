using Inventario.Core.Comun;
using Inventario.Negocio.Estados;

namespace Inventario.Negocio.Dominio;

/// <summary>
/// Entidad central del módulo de negocio. El estado solo cambia con Emitir, Recibir y Cancelar,
/// y cada uno consulta TransicionesOrdenCompra ANTES de modificar algo: si la transición está
/// prohibida se lanza el rechazo y la orden queda exactamente igual (RF-NEG-04).
/// </summary>
public class OrdenCompra
{
    public const int LongitudMaximaMotivo = 500;

    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public EstadoOrdenCompra Estado { get; private set; } = EstadoOrdenCompra.Borrador;
    public DateTime FechaCreacion { get; set; }
    public int CreadaPorUsuarioId { get; set; }
    public DateTime? FechaEmision { get; private set; }
    public DateTime? FechaRecepcion { get; private set; }
    public int? RecibidaPorUsuarioId { get; private set; }
    public DateTime? FechaCancelacion { get; private set; }
    public string? MotivoCancelacion { get; private set; }
    public List<DetalleOrdenCompra> Detalles { get; set; } = new();

    public decimal Total => Detalles.Sum(d => d.Cantidad * d.CostoUnitario);

    /// <summary>Solo en Borrador se pueden agregar detalles.</summary>
    public DetalleOrdenCompra AgregarDetalle(int productoId, int cantidad, decimal costoUnitario)
    {
        if (Estado != EstadoOrdenCompra.Borrador)
            throw ExcepcionControlada.Conflicto("Solo se pueden agregar detalles a una orden en Borrador.");
        if (cantidad <= 0)
            throw ExcepcionControlada.Solicitud("La cantidad debe ser mayor que cero.");
        if (costoUnitario < 0)
            throw ExcepcionControlada.Solicitud("El costo unitario no puede ser negativo.");

        var detalle = new DetalleOrdenCompra { ProductoId = productoId, Cantidad = cantidad, CostoUnitario = costoUnitario };
        Detalles.Add(detalle);
        return detalle;
    }

    /// <summary>Borrador → Emitida. Condición: la orden tiene al menos un detalle.</summary>
    public void Emitir(DateTime ahora)
    {
        TransicionesOrdenCompra.Validar(Estado, EstadoOrdenCompra.Emitida);
        if (Detalles.Count == 0)
            throw ExcepcionControlada.Solicitud("No se puede emitir una orden sin detalles.");

        Estado = EstadoOrdenCompra.Emitida;
        FechaEmision = ahora;
    }

    /// <summary>Emitida → Recibida. El servicio registra los movimientos de stock en la misma operación.</summary>
    public void Recibir(int usuarioId, DateTime ahora)
    {
        TransicionesOrdenCompra.Validar(Estado, EstadoOrdenCompra.Recibida);
        Estado = EstadoOrdenCompra.Recibida;
        FechaRecepcion = ahora;
        RecibidaPorUsuarioId = usuarioId;
    }

    /// <summary>Borrador → Cancelada, o Emitida → Cancelada con motivo obligatorio.</summary>
    public void Cancelar(string? motivo, DateTime ahora)
    {
        TransicionesOrdenCompra.Validar(Estado, EstadoOrdenCompra.Cancelada);

        var motivoLimpio = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (Estado == EstadoOrdenCompra.Emitida && motivoLimpio is null)
            throw ExcepcionControlada.Solicitud("Para cancelar una orden emitida debes escribir el motivo.");
        if (motivoLimpio?.Length > LongitudMaximaMotivo)
            throw ExcepcionControlada.Solicitud($"El motivo admite hasta {LongitudMaximaMotivo} caracteres.");

        Estado = EstadoOrdenCompra.Cancelada;
        FechaCancelacion = ahora;
        MotivoCancelacion = motivoLimpio;
    }
}
