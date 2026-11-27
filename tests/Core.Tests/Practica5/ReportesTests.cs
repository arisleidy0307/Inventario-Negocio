using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Reportes;
using Inventario.Core.Seguridad;
using Xunit;

namespace Inventario.Core.Tests.Practica5;

/// <summary>
/// Reportes con agregación (RF-REP-01 a 05), criterios CA-REP-01 a 05. Las consultas reales (GROUP BY en la base)
/// sobre SQLite en memoria, con fechas controladas por el reloj.
/// </summary>
public sealed class ReportesTests : IDisposable
{
    private readonly EntornoSqlite _e = new();

    public void Dispose() => _e.Dispose();

    private static RangoFechas Rango(string? desde, string? hasta) => RangoFechas.Crear(desde, hasta);

    /// <summary>6 solicitudes en noviembre: 3 Aplicada, 2 Rechazada y 1 Pendiente, repartidas entre A y B.</summary>
    private async Task<(Usuario Admin, Usuario A, Usuario B)> SolicitudesDeNoviembreAsync()
    {
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);

        async Task Resolver(Usuario quien, string operacion, bool? aprobar)
        {
            var s = await _e.Permisos.CrearSolicitudAsync(quien.Id, operacion, null);
            if (aprobar == true) await _e.Permisos.AprobarAsync(s.Id, admin.Id);
            if (aprobar == false) await _e.Permisos.RechazarAsync(s.Id, admin.Id, "No corresponde");
        }

        await Resolver(a, Operaciones.RecibirOrden, true);
        await Resolver(a, Operaciones.ListarUsuarios, true);
        await Resolver(b, Operaciones.RecibirOrden, true);
        await Resolver(b, Operaciones.ListarUsuarios, false);
        _e.Reloj.UtcNow = _e.Reloj.UtcNow.AddDays(1);
        await Resolver(b, Operaciones.ListarUsuarios, false);
        await Resolver(b, Operaciones.ListarUsuarios, null);
        return (admin, a, b);
    }

    // ---------- CA-REP-01 (RF-REP-01, RF-REP-03) ----------

    [Trait("Requisito", "RF-REP-01")]
    [Fact]
    public async Task ReporteSolicitudes_Administrador_AgrupaPorMesYEstadoConMenosFilasQueSolicitudes()
    {
        // Preparar
        var (admin, _, _) = await SolicitudesDeNoviembreAsync();

        // Actuar
        var r = await _e.ReportesCore.SolicitudesPermisoAsync(admin.Id, Rol.Administrador, RangoFechas.Todo);

        // Verificar
        Assert.Equal(6, r.Total);
        Assert.Equal(3, r.Filas.Count);
        Assert.All(r.Filas, f => Assert.Equal("2026-11", f.Mes));
        Assert.Equal(3, r.Filas.Single(f => f.Estado == "Aplicada").Cantidad);
        Assert.Equal(2, r.Filas.Single(f => f.Estado == "Rechazada").Cantidad);
        Assert.Equal(1, r.Filas.Single(f => f.Estado == "Pendiente").Cantidad);
        Assert.True(r.Filas.Count < r.Total);
    }

    // ---------- CA-REP-03 (RF-REP-04) ----------

    [Trait("Requisito", "RF-REP-04")]
    [Fact]
    public async Task ReporteSolicitudes_Estandar_SoloCuentaLasSuyasYLasDeAmbosSumanLasDelAdministrador()
    {
        // Preparar
        var (admin, a, b) = await SolicitudesDeNoviembreAsync();

        // Actuar
        var deA = await _e.ReportesCore.SolicitudesPermisoAsync(a.Id, Rol.Estandar, RangoFechas.Todo);
        var deB = await _e.ReportesCore.SolicitudesPermisoAsync(b.Id, Rol.Estandar, RangoFechas.Todo);
        var todos = await _e.ReportesCore.SolicitudesPermisoAsync(admin.Id, Rol.Administrador, RangoFechas.Todo);

        // Verificar
        Assert.Equal(2, deA.Total);
        Assert.Equal("Aplicada", Assert.Single(deA.Filas).Estado);
        Assert.Equal(4, deB.Total);
        Assert.Equal(todos.Total, deA.Total + deB.Total);
        Assert.StartsWith("Solo tus datos", deA.Alcance);
    }

    // ---------- CA-REP-04 (RF-REP-05) ----------

    [Trait("Requisito", "RF-REP-05")]
    [Fact]
    public async Task ReporteSolicitudes_DosRangosDistintos_DanResultadosDistintosSobreLosMismosDatos()
    {
        // Preparar: 4 solicitudes el 20 de noviembre y 2 el 21 (hora de RD)
        var (admin, _, _) = await SolicitudesDeNoviembreAsync();

        // Actuar
        var dia20 = await _e.ReportesCore.SolicitudesPermisoAsync(admin.Id, Rol.Administrador, Rango("2026-11-20", "2026-11-20"));
        var dia21 = await _e.ReportesCore.SolicitudesPermisoAsync(admin.Id, Rol.Administrador, Rango("2026-11-21", "2026-11-21"));
        var anioPasado = await _e.ReportesCore.SolicitudesPermisoAsync(admin.Id, Rol.Administrador, Rango("2025-01-01", "2025-12-31"));

        // Verificar
        Assert.Equal(4, dia20.Total);
        Assert.Equal(2, dia21.Total);
        Assert.Equal(0, anioPasado.Total);
        Assert.Empty(anioPasado.Filas);
    }

    [Trait("Requisito", "RF-REP-05")]
    [Fact]
    public async Task ReporteSolicitudes_HastaEsInclusivoEnHoraDeRepublicaDominicana()
    {
        // Preparar: una solicitud a las 23:30 del 20 de noviembre en RD, que en UTC ya es el 21
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        _e.Reloj.UtcNow = new DateTime(2026, 11, 21, 3, 30, 0, DateTimeKind.Utc);
        await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.RecibirOrden, null);

        // Actuar
        var hasta20 = await _e.ReportesCore.SolicitudesPermisoAsync(a.Id, Rol.Estandar, Rango(null, "2026-11-20"));
        var desde21 = await _e.ReportesCore.SolicitudesPermisoAsync(a.Id, Rol.Estandar, Rango("2026-11-21", null));

        // Verificar
        Assert.Equal(1, hasta20.Total);
        Assert.Equal(0, desde21.Total);
    }

    [Trait("Requisito", "RF-REP-05")]
    [Theory]
    [InlineData("2026-11-30", "2026-11-01")]
    [InlineData("30/11/2026", null)]
    [InlineData(null, "ayer")]
    public void RangoFechas_FormatoInvalidoODesdePosteriorAHasta_SeRechazaConSolicitudInvalida(string? desde, string? hasta)
    {
        // Preparar
        var crear = () => RangoFechas.Crear(desde, hasta);

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(crear);

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
    }

    // ---------- CA-REP-02 (RF-REP-02, RF-REP-03) ----------

    /// <summary>
    /// Proveedor X: 3 órdenes recibidas de A (4 250, 2 400 y 1 700 DOP). Proveedor Y: 1 recibida de B (1 000 DOP).
    /// Además una orden de X solo emitida y otra cancelada, que no deben sumar.
    /// </summary>
    private async Task<(Usuario Admin, Usuario A, Usuario B)> ComprasAsync()
    {
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);
        var (x, arroz) = await _e.CatalogoAsync("Distribuidora Caribe", "P-001");
        var (y, aceite) = await _e.CatalogoAsync("Importadora del Este", "P-002");

        await _e.OrdenAsync(a.Id, admin.Id, x.Id, arroz.Id, 5, 850m, recibir: true);   // 4 250
        await _e.OrdenAsync(a.Id, admin.Id, x.Id, arroz.Id, 3, 800m, recibir: true);   // 2 400
        _e.Reloj.UtcNow = _e.Reloj.UtcNow.AddDays(1);
        await _e.OrdenAsync(a.Id, admin.Id, x.Id, arroz.Id, 2, 850m, recibir: true);   // 1 700
        await _e.OrdenAsync(b.Id, admin.Id, y.Id, aceite.Id, 4, 250m, recibir: true);  // 1 000
        await _e.OrdenAsync(a.Id, admin.Id, x.Id, arroz.Id, 9, 100m, recibir: false);  // emitida: no cuenta
        var cancelada = await _e.OrdenAsync(a.Id, admin.Id, x.Id, arroz.Id, 7, 100m, recibir: false);
        await _e.Ordenes.CancelarAsync(cancelada.Id, "Sin existencia");                // cancelada: no cuenta
        return (admin, a, b);
    }

    [Trait("Requisito", "RF-REP-02")]
    [Fact]
    public async Task ReporteCompras_Administrador_UnaFilaPorProveedorConOrdenesUnidadesMontoYPromedio()
    {
        // Preparar
        var (admin, _, _) = await ComprasAsync();

        // Actuar
        var r = await _e.ReportesNegocio.ComprasPorProveedorAsync(admin.Id, Rol.Administrador, RangoFechas.Todo);

        // Verificar
        Assert.Equal(new[] { "Distribuidora Caribe", "Importadora del Este" }, r.Filas.Select(f => f.Proveedor)); // mayor monto primero
        var x = r.Filas[0];
        Assert.Equal((3, 10, 8350m, 2783.33m), (x.OrdenesRecibidas, x.Unidades, x.MontoTotal, x.MontoPromedioPorOrden));
        var y = r.Filas[1];
        Assert.Equal((1, 4, 1000m), (y.OrdenesRecibidas, y.Unidades, y.MontoTotal));
        Assert.Equal((4, 14, 9350m), (r.TotalOrdenes, r.TotalUnidades, r.MontoTotal));
        Assert.Equal("DOP", r.Moneda);
        Assert.True(r.Filas.Count < r.TotalOrdenes);
    }

    [Trait("Requisito", "RF-REP-04")]
    [Fact]
    public async Task ReporteCompras_Estandar_SoloCuentaLasOrdenesQueEl()
    {
        // Preparar
        var (admin, a, b) = await ComprasAsync();

        // Actuar
        var deA = await _e.ReportesNegocio.ComprasPorProveedorAsync(a.Id, Rol.Estandar, RangoFechas.Todo);
        var deB = await _e.ReportesNegocio.ComprasPorProveedorAsync(b.Id, Rol.Estandar, RangoFechas.Todo);
        var todos = await _e.ReportesNegocio.ComprasPorProveedorAsync(admin.Id, Rol.Administrador, RangoFechas.Todo);

        // Verificar
        Assert.Equal("Distribuidora Caribe", Assert.Single(deA.Filas).Proveedor);
        Assert.Equal(8350m, deA.MontoTotal);
        Assert.Equal("Importadora del Este", Assert.Single(deB.Filas).Proveedor);
        Assert.Equal(todos.MontoTotal, deA.MontoTotal + deB.MontoTotal);
    }

    [Trait("Requisito", "RF-REP-05")]
    [Fact]
    public async Task ReporteCompras_DosRangosDistintos_DanResultadosDistintosPorLaFechaDeRecepcion()
    {
        // Preparar: 2 órdenes de X recibidas el 20 de noviembre; la tercera de X y la de Y, el 21
        var (admin, _, _) = await ComprasAsync();

        // Actuar
        var dia20 = await _e.ReportesNegocio.ComprasPorProveedorAsync(admin.Id, Rol.Administrador, Rango("2026-11-20", "2026-11-20"));
        var dia21 = await _e.ReportesNegocio.ComprasPorProveedorAsync(admin.Id, Rol.Administrador, Rango("2026-11-21", "2026-11-21"));
        var anioPasado = await _e.ReportesNegocio.ComprasPorProveedorAsync(admin.Id, Rol.Administrador, Rango("2025-01-01", "2025-12-31"));

        // Verificar
        var x20 = Assert.Single(dia20.Filas);
        Assert.Equal((2, 6650m), (x20.OrdenesRecibidas, x20.MontoTotal));
        Assert.Equal(2, dia21.Filas.Count);
        Assert.Equal(2700m, dia21.MontoTotal);
        Assert.Empty(anioPasado.Filas);
        Assert.Equal(0m, anioPasado.MontoTotal);
    }

    // ---------- CA-REP-05 (RD-06) ----------

    [Trait("Requisito", "RD-06")]
    [Theory]
    [InlineData(Operaciones.ReporteSolicitudesPermiso)]
    [InlineData(Operaciones.ReporteComprasPorProveedor)]
    public void Reportes_SinSesion_SeRechazan(string operacion)
    {
        // Preparar
        var exigencia = PoliticasAcceso.Obtener(operacion);

        // Actuar
        var sinSesion = PoliticasAcceso.Permite(operacion, false, null, null);

        // Verificar
        Assert.True(exigencia.RequiereSesion);
        Assert.False(sinSesion);
    }
}
