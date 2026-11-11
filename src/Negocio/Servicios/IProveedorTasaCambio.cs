namespace Inventario.Negocio.Servicios;

/// <summary>Tasa de cambio obtenida de un servicio externo.</summary>
public record TasaCambio(string Origen, string Destino, decimal Tasa, DateTime? FechaActualizacionUtc, string Fuente);

/// <summary>
/// Origen de las tasas de cambio. El negocio no sabe qué API se usa ni dónde está la clave:
/// la implementación (ExchangeRate-API) vive en Datos y lee la clave de una variable de entorno (RD-10).
/// Si no hay clave o la API no responde, lanza ExcepcionControlada (503) y el resto del sistema sigue funcionando.
/// </summary>
public interface IProveedorTasaCambio
{
    Task<TasaCambio> ObtenerAsync(string monedaOrigen, string monedaDestino, CancellationToken ct = default);
}
