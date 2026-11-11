using System.Net;
using System.Text;
using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Permisos;
using Inventario.Core.Tests.GestionPermisos;
using Inventario.Core.Tests.Negocio;
using Inventario.Datos.Externos;
using Inventario.Negocio.Servicios;
using Xunit;

namespace Inventario.Core.Tests.ApiExterna;

/// <summary>
/// La operación que usa la API externa: cotizar una orden en otra moneda. La API se reemplaza por un
/// manejador HTTP falso, así las pruebas no dependen de la red ni de una clave real (RD-12).
/// </summary>
public class CotizacionOrdenesTests
{
    private const int EstandarId = 10;
    private const int OtroEstandarId = 20;
    private static readonly DateTime Ahora = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    private const string RespuestaExito = """
        {"result":"success","time_last_update_utc":"Tue, 10 Nov 2026 00:00:01 +0000",
         "base_code":"DOP","target_code":"USD","conversion_rate":0.01580}
        """;

    /// <summary>Responde siempre lo mismo y anota las URL pedidas; o lanza la excepción indicada.</summary>
    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly string? _cuerpo;
        private readonly HttpStatusCode _codigo;
        private readonly Exception? _error;
        public List<string> Urls { get; } = new();

        public ManejadorFalso(string? cuerpo = null, HttpStatusCode codigo = HttpStatusCode.OK, Exception? error = null)
        {
            _cuerpo = cuerpo;
            _codigo = codigo;
            _error = error;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (_error is not null)
                throw _error;
            return Task.FromResult(new HttpResponseMessage(_codigo)
            {
                Content = new StringContent(_cuerpo ?? "", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record Contexto(ServicioCotizacionOrdenes Cotizaciones, ServicioOrdenesCompra Ordenes, ServicioCatalogo Catalogo);

    private static Contexto Crear(IProveedorTasaCambio tasas)
    {
        var repo = new RepositorioNegocioEnMemoria();
        var permisos = new ServicioPermisos(new RepositorioPermisosEnMemoria(), new RelojFijo(Ahora));
        var ordenes = new ServicioOrdenesCompra(repo, permisos, new RelojFijo(Ahora));
        return new Contexto(new ServicioCotizacionOrdenes(ordenes, tasas), ordenes, new ServicioCatalogo(repo));
    }

    private static async Task<int> OrdenConTotal4250(Contexto c)
    {
        var proveedor = await c.Catalogo.CrearProveedorAsync("Distribuidora Caribe", null, null);
        var producto = await c.Catalogo.CrearProductoAsync("P-001", "Arroz 25 lb", 900m);
        var orden = await c.Ordenes.CrearAsync(EstandarId, proveedor.Id);
        await c.Ordenes.AgregarDetalleAsync(orden.Id, EstandarId, Rol.Estandar, producto.Id, 5, 850m);
        return orden.Id;
    }

    private static ClienteExchangeRateApi Cliente(ManejadorFalso manejador, string? clave = "clave-de-prueba") =>
        new(new HttpClient(manejador), clave);

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_ConTasaDeLaApi_CalculaElTotalDeLaOrdenEnLaOtraMoneda()
    {
        // Preparar
        var manejador = new ManejadorFalso(RespuestaExito);
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var cotizacion = await c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "usd");

        // Verificar
        Assert.Equal(4250m, cotizacion.TotalOrden);
        Assert.Equal("DOP", cotizacion.MonedaOrden);
        Assert.Equal("USD", cotizacion.Moneda);
        Assert.Equal(0.01580m, cotizacion.Tasa);
        Assert.Equal(67.15m, cotizacion.TotalConvertido);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 1, DateTimeKind.Utc), cotizacion.FechaTasaUtc);
        Assert.Equal("ExchangeRate-API", cotizacion.Fuente);
        Assert.EndsWith("/clave-de-prueba/pair/DOP/USD", Assert.Single(manejador.Urls));
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_SinClaveConfigurada_RechazoControladoSinLlamarALaApi()
    {
        // Preparar
        var manejador = new ManejadorFalso(RespuestaExito);
        var c = Crear(Cliente(manejador, clave: "  "));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(503, error.CodigoEstado);
        Assert.Contains("EXCHANGE_API_KEY", error.Message);
        Assert.Empty(manejador.Urls);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_ClaveRechazadaPorLaApi_RechazoControladoSinMostrarLaClave()
    {
        // Preparar
        var manejador = new ManejadorFalso("""{"result":"error","error-type":"invalid-key"}""", HttpStatusCode.Forbidden);
        var c = Crear(Cliente(manejador, clave: "tu-clave-de-exchangerate-api"));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(503, error.CodigoEstado);
        Assert.Contains("rechazó la clave", error.Message);
        Assert.DoesNotContain("tu-clave-de-exchangerate-api", error.Message);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_ApiNoResponde_RechazoControlado()
    {
        // Preparar
        var manejador = new ManejadorFalso(error: new HttpRequestException("No such host is known."));
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(503, error.CodigoEstado);
        Assert.Contains("no responde", error.Message);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_ApiTardaDemasiado_RechazoControlado()
    {
        // Preparar
        var manejador = new ManejadorFalso(error: new TaskCanceledException("timeout"));
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(503, error.CodigoEstado);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_RespuestaQueNoEsJson_RechazoControlado()
    {
        // Preparar
        var manejador = new ManejadorFalso("<html>502 Bad Gateway</html>", HttpStatusCode.BadGateway);
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(503, error.CodigoEstado);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_MonedaQueLaApiNoSoporta_SeRechazaConSolicitudInvalida()
    {
        // Preparar
        var manejador = new ManejadorFalso("""{"result":"error","error-type":"unsupported-code"}""");
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, "XYZ"));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Contains("XYZ", error.Message);
    }

    [Trait("Requisito", "API externa")]
    [Theory]
    [InlineData("")]
    [InlineData("dolares")]
    [InlineData("US")]
    [InlineData("DOP")]
    public async Task Cotizar_MonedaInvalida_SeRechazaSinLlamarALaApi(string moneda)
    {
        // Preparar
        var manejador = new ManejadorFalso(RespuestaExito);
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, EstandarId, Rol.Estandar, moneda));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Empty(manejador.Urls);
    }

    [Trait("Requisito", "API externa")]
    [Fact]
    public async Task Cotizar_OrdenDeOtroEstandar_SeRechazaSinLlamarALaApi()
    {
        // Preparar
        var manejador = new ManejadorFalso(RespuestaExito);
        var c = Crear(Cliente(manejador));
        var ordenId = await OrdenConTotal4250(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Cotizaciones.CotizarAsync(ordenId, OtroEstandarId, Rol.Estandar, "USD"));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.Empty(manejador.Urls);
    }
}
