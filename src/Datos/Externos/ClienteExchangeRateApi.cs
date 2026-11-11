using System.Globalization;
using System.Text.Json;
using Inventario.Core.Comun;
using Inventario.Negocio.Servicios;

namespace Inventario.Datos.Externos;

/// <summary>
/// Cliente de ExchangeRate-API (https://www.exchangerate-api.com), endpoint «pair».
/// La clave llega por la variable de entorno EXCHANGE_API_KEY (RD-10) y nunca aparece en mensajes ni registros.
/// Sin clave, con la clave rechazada o con la API caída, lanza un 503 controlado (RD-07, RD-08).
/// </summary>
public class ClienteExchangeRateApi : IProveedorTasaCambio
{
    public const string Fuente = "ExchangeRate-API";
    public const string UrlPorDefecto = "https://v6.exchangerate-api.com/v6/";

    private readonly HttpClient _http;
    private readonly string? _clave;
    private readonly string _urlBase;

    public ClienteExchangeRateApi(HttpClient http, string? clave, string? urlBase = null)
    {
        _http = http;
        _clave = string.IsNullOrWhiteSpace(clave) ? null : clave.Trim();
        _urlBase = string.IsNullOrWhiteSpace(urlBase) ? UrlPorDefecto : urlBase.TrimEnd('/') + "/";
    }

    public async Task<TasaCambio> ObtenerAsync(string monedaOrigen, string monedaDestino, CancellationToken ct = default)
    {
        if (_clave is null)
            throw ExcepcionControlada.NoDisponible(
                "La cotización no está disponible: falta configurar la variable EXCHANGE_API_KEY en el archivo .env.");

        var url = $"{_urlBase}{Uri.EscapeDataString(_clave)}/pair/{monedaOrigen}/{monedaDestino}";
        JsonDocument json;
        try
        {
            using var respuesta = await _http.GetAsync(url, ct);
            await using var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
            json = await JsonDocument.ParseAsync(cuerpo, cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw ExcepcionControlada.NoDisponible(
                "El servicio de tasas de cambio (ExchangeRate-API) no responde. Intenta más tarde; el resto del sistema sigue funcionando.");
        }

        using (json)
        {
            var raiz = json.RootElement;
            var resultado = Texto(raiz, "result");
            if (resultado == "success" && raiz.TryGetProperty("conversion_rate", out var tasa) && tasa.TryGetDecimal(out var valor))
            {
                DateTime? fecha = DateTime.TryParse(Texto(raiz, "time_last_update_utc"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var f) ? f : null;
                return new TasaCambio(Texto(raiz, "base_code") ?? monedaOrigen, Texto(raiz, "target_code") ?? monedaDestino,
                    valor, fecha, Fuente);
            }

            throw Texto(raiz, "error-type") switch
            {
                "unsupported-code" => ExcepcionControlada.Solicitud(
                    $"La moneda {monedaDestino} no está soportada por ExchangeRate-API."),
                "invalid-key" or "inactive-account" => ExcepcionControlada.NoDisponible(
                    "ExchangeRate-API rechazó la clave de EXCHANGE_API_KEY. Revisa el valor en tu archivo .env; el resto del sistema sigue funcionando."),
                "quota-reached" => ExcepcionControlada.NoDisponible(
                    "Se agotó la cuota mensual de consultas de ExchangeRate-API. Intenta más tarde."),
                _ => ExcepcionControlada.NoDisponible(
                    "El servicio de tasas de cambio (ExchangeRate-API) no devolvió una tasa válida. Intenta más tarde.")
            };
        }
    }

    private static string? Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
