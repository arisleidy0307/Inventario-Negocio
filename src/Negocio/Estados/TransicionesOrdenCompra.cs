namespace Inventario.Negocio.Estados;

/// <summary>
/// RD-04: ÚNICO lugar donde se declaran las transiciones permitidas de la orden de compra.
/// Todo lo que no aparece aquí está prohibido (RF-NEG-04); por ejemplo Recibida → Cancelada
/// o Borrador → Recibida. Recibida y Cancelada no tienen salidas: son estados terminales (RF-NEG-05).
/// La tabla completa (quién ejecuta y con qué condición) está en docs/maquina-de-estados.md.
/// </summary>
public static class TransicionesOrdenCompra
{
    private static readonly IReadOnlyDictionary<EstadoOrdenCompra, EstadoOrdenCompra[]> Permitidas =
        new Dictionary<EstadoOrdenCompra, EstadoOrdenCompra[]>
        {
            [EstadoOrdenCompra.Borrador]  = new[] { EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Cancelada },
            [EstadoOrdenCompra.Emitida]   = new[] { EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada },
            [EstadoOrdenCompra.Recibida]  = Array.Empty<EstadoOrdenCompra>(), // terminal
            [EstadoOrdenCompra.Cancelada] = Array.Empty<EstadoOrdenCompra>(), // terminal
        };

    /// <summary>Transiciones prohibidas declaradas explícitamente (RF-NEG-04).</summary>
    public static readonly IReadOnlyList<(EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia)> ProhibidasExplicitas = new[]
    {
        (EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada),
        (EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Recibida),
    };

    public static bool Puede(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Permitidas.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    public static bool EsTerminal(EstadoOrdenCompra estado) =>
        Permitidas.TryGetValue(estado, out var destinos) && destinos.Length == 0;

    public static IReadOnlyCollection<EstadoOrdenCompra> DestinosDesde(EstadoOrdenCompra estado) =>
        Permitidas.TryGetValue(estado, out var destinos) ? destinos : Array.Empty<EstadoOrdenCompra>();
}
