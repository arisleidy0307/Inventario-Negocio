using System.Text.RegularExpressions;
using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Negocio.Estados;

namespace Inventario.Negocio.Servicios;

/// <summary>Resultado de cotizar el total de una orden (en pesos dominicanos) en otra moneda.</summary>
public record CotizacionOrden(
    int OrdenId,
    EstadoOrdenCompra Estado,
    string MonedaOrden,
    decimal TotalOrden,
    string Moneda,
    decimal Tasa,
    decimal TotalConvertido,
    DateTime? FechaTasaUtc,
    string Fuente);

/// <summary>
/// Cotiza una orden de compra en otra moneda con la tasa del día que da la API externa.
/// Usa los datos de la orden (sus detalles y su total) y los de la API; no reenvía la respuesta de la API.
/// </summary>
public class ServicioCotizacionOrdenes
{
    /// <summary>Los totales de las órdenes están en pesos dominicanos.</summary>
    public const string MonedaOrdenes = "DOP";

    private static readonly Regex CodigoMoneda = new("^[A-Z]{3}$", RegexOptions.Compiled);

    private readonly ServicioOrdenesCompra _ordenes;
    private readonly IProveedorTasaCambio _tasas;

    public ServicioCotizacionOrdenes(ServicioOrdenesCompra ordenes, IProveedorTasaCambio tasas)
    {
        _ordenes = ordenes;
        _tasas = tasas;
    }

    public async Task<CotizacionOrden> CotizarAsync(int ordenId, int actorId, Rol rolActor, string? moneda)
    {
        var destino = (moneda ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodigoMoneda.IsMatch(destino))
            throw ExcepcionControlada.Solicitud("La moneda debe ser un código ISO de 3 letras, por ejemplo USD o EUR.");
        if (destino == MonedaOrdenes)
            throw ExcepcionControlada.Solicitud("La orden ya está en DOP; indica otra moneda, por ejemplo USD.");

        // Misma regla que ver la orden: un Estándar solo cotiza las suyas (403 si es ajena, 404 si no existe).
        var orden = await _ordenes.ObtenerAsync(ordenId, actorId, rolActor);
        if (orden.Detalles.Count == 0)
            throw ExcepcionControlada.Solicitud("La orden no tiene detalles: no hay un total que cotizar.");

        var tasa = await _tasas.ObtenerAsync(MonedaOrdenes, destino);
        var total = orden.Total;
        return new CotizacionOrden(orden.Id, orden.Estado, MonedaOrdenes, total, tasa.Destino, tasa.Tasa,
            Math.Round(total * tasa.Tasa, 2, MidpointRounding.AwayFromZero), tasa.FechaActualizacionUtc, tasa.Fuente);
    }
}
