namespace Inventario.Negocio.Estados;

/// <summary>
/// RF-NEG-03: ÚNICO lugar donde se declaran los estados de la orden de compra.
/// Recibida y Cancelada son terminales (RF-NEG-05).
/// </summary>
public enum EstadoOrdenCompra
{
    Borrador = 1,
    Emitida = 2,
    Recibida = 3,
    Cancelada = 4
}
