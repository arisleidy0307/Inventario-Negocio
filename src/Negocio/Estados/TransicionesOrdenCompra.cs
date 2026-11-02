using Inventario.Core.Comun;

namespace Inventario.Negocio.Estados;

/// <summary>
/// RD-04: ÚNICO lugar donde se declaran las transiciones permitidas de la orden de compra.
/// Todo lo que no aparece aquí está prohibido (RF-NEG-04); por ejemplo Borrador → Recibida
/// o Recibida → Cancelada. Recibida y Cancelada no tienen salidas: son estados terminales (RF-NEG-05).
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
            [EstadoOrdenCompra.Cancelada] = new[] { EstadoOrdenCompra.Borrador },
        };

    /// <summary>
    /// Transiciones prohibidas declaradas explícitamente (RF-NEG-04), en el mismo orden que la tabla
    /// de prohibidas de docs/maquina-de-estados.md.
    /// </summary>
    public static readonly IReadOnlyList<(EstadoOrdenCompra Desde, EstadoOrdenCompra Hacia)> ProhibidasExplicitas = new[]
    {
        (EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Recibida),
        (EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada),
        (EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Emitida),
        (EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Emitida),
    };

    public static bool Puede(EstadoOrdenCompra desde, EstadoOrdenCompra hacia) =>
        Permitidas.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    public static bool EsTerminal(EstadoOrdenCompra estado) =>
        Permitidas.TryGetValue(estado, out var destinos) && destinos.Length == 0;

    public static IReadOnlyCollection<EstadoOrdenCompra> DestinosDesde(EstadoOrdenCompra estado) =>
        Permitidas.TryGetValue(estado, out var destinos) ? destinos : Array.Empty<EstadoOrdenCompra>();

    /// <summary>Rechazo controlado (409) si la transición no está en la tabla; el estado no se toca.</summary>
    public static void Validar(EstadoOrdenCompra desde, EstadoOrdenCompra hacia)
    {
        if (!Puede(desde, hacia))
            throw ExcepcionControlada.Conflicto(
                $"Transición no permitida: la orden está {desde} y no puede pasar a {hacia}.");
    }
}
