using Inventario.Core.Comun;
using Inventario.Negocio.Dominio;
using Inventario.Negocio.Estados;
using Xunit;

namespace Inventario.Core.Tests.Negocio;

/// <summary>
/// Una prueba por cada fila de las tablas de docs/maquina-de-estados.md (permitidas, condiciones y prohibidas)
/// y pruebas de los estados terminales. Ejercitan la entidad OrdenCompra sin levantar la aplicación (RD-12).
/// Cada prueba crea su propia orden: son independientes. Fases: // Preparar, // Actuar, // Verificar.
/// </summary>
public class MaquinaEstadosOrdenCompraTests
{
    private static readonly DateTime Ahora = new(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc);
    private const int AdminId = 1;

    private static OrdenCompra OrdenEnBorradorConDetalle()
    {
        var orden = new OrdenCompra { Id = 1, ProveedorId = 1, CreadaPorUsuarioId = 10, FechaCreacion = Ahora };
        orden.AgregarDetalle(productoId: 1, cantidad: 5, costoUnitario: 100m);
        return orden;
    }

    private static OrdenCompra OrdenEmitida()
    {
        var orden = OrdenEnBorradorConDetalle();
        orden.Emitir(Ahora);
        return orden;
    }

    private static OrdenCompra OrdenRecibida()
    {
        var orden = OrdenEmitida();
        orden.Recibir(AdminId, Ahora);
        return orden;
    }

    private static OrdenCompra OrdenCancelada()
    {
        var orden = OrdenEnBorradorConDetalle();
        orden.Cancelar(null, Ahora);
        return orden;
    }

    // ---------- Tabla de transiciones permitidas ----------

    [Trait("Fila", "Permitida: Borrador → Emitida")]
    [Fact]
    public void Emitir_OrdenEnBorradorConDetalle_QuedaEmitidaConFechaDeEmision()
    {
        // Preparar
        var orden = OrdenEnBorradorConDetalle();

        // Actuar
        orden.Emitir(Ahora);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Emitida, orden.Estado);
        Assert.Equal(Ahora, orden.FechaEmision);
    }

    [Trait("Fila", "Permitida: Borrador → Cancelada")]
    [Fact]
    public void Cancelar_OrdenEnBorrador_QuedaCancelada()
    {
        // Preparar
        var orden = OrdenEnBorradorConDetalle();

        // Actuar
        orden.Cancelar(null, Ahora);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
        Assert.Equal(Ahora, orden.FechaCancelacion);
    }

    [Trait("Fila", "Permitida: Emitida → Recibida")]
    [Fact]
    public void Recibir_OrdenEmitida_QuedaRecibidaYRegistraQuienYCuando()
    {
        // Preparar
        var orden = OrdenEmitida();

        // Actuar
        orden.Recibir(AdminId, Ahora);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
        Assert.Equal(AdminId, orden.RecibidaPorUsuarioId);
        Assert.Equal(Ahora, orden.FechaRecepcion);
    }

    [Trait("Fila", "Permitida: Emitida → Cancelada")]
    [Fact]
    public void Cancelar_OrdenEmitidaConMotivo_QuedaCanceladaYGuardaElMotivo()
    {
        // Preparar
        var orden = OrdenEmitida();

        // Actuar
        orden.Cancelar("  El proveedor no tiene existencia  ", Ahora);

        // Verificar
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
        Assert.Equal("El proveedor no tiene existencia", orden.MotivoCancelacion);
    }

    // ---------- Condiciones de las transiciones permitidas ----------

    [Trait("Fila", "Condición: Borrador → Emitida exige al menos un detalle")]
    [Fact]
    public void Emitir_OrdenSinDetalles_SeRechazaYSigueEnBorrador()
    {
        // Preparar
        var orden = new OrdenCompra { Id = 1, ProveedorId = 1, CreadaPorUsuarioId = 10, FechaCreacion = Ahora };

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Emitir(Ahora));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Borrador, orden.Estado);
        Assert.Null(orden.FechaEmision);
    }

    [Trait("Fila", "Condición: Emitida → Cancelada exige motivo")]
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancelar_OrdenEmitidaSinMotivo_SeRechazaYSigueEmitida(string? motivo)
    {
        // Preparar
        var orden = OrdenEmitida();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Cancelar(motivo, Ahora));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Emitida, orden.Estado);
    }

    // ---------- Tabla de transiciones prohibidas (RF-NEG-04) ----------

    [Trait("Fila", "Prohibida: Recibida → Cancelada")]
    [Fact]
    public void Cancelar_OrdenRecibida_SeRechazaYSigueRecibida()
    {
        // Preparar
        var orden = OrdenRecibida();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Cancelar("Ya no la queremos", Ahora));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
        Assert.Null(orden.MotivoCancelacion);
    }

    [Trait("Fila", "Prohibida: Borrador → Recibida")]
    [Fact]
    public void Recibir_OrdenEnBorrador_SeRechazaYSigueEnBorrador()
    {
        // Preparar
        var orden = OrdenEnBorradorConDetalle();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Recibir(AdminId, Ahora));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Borrador, orden.Estado);
        Assert.Null(orden.FechaRecepcion);
    }

    [Trait("Fila", "Prohibida: Recibida → Emitida")]
    [Fact]
    public void Emitir_OrdenRecibida_SeRechazaYSigueRecibida()
    {
        // Preparar
        var orden = OrdenRecibida();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Emitir(Ahora.AddHours(1)));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Recibida, orden.Estado);
        Assert.Equal(Ahora, orden.FechaEmision);
    }

    [Trait("Fila", "Prohibida: Cancelada → Emitida")]
    [Fact]
    public void Emitir_OrdenCancelada_SeRechazaYSigueCancelada()
    {
        // Preparar
        var orden = OrdenCancelada();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Emitir(Ahora));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoOrdenCompra.Cancelada, orden.Estado);
        Assert.Null(orden.FechaEmision);
    }

    // ---------- Estados terminales (RF-NEG-05) ----------

    [Trait("Fila", "Terminal: Cancelada")]
    [Fact]
    public void Cancelada_EsTerminal_NingunaTransicionParteDeElla()
    {
        // Preparar
        var estado = EstadoOrdenCompra.Cancelada;

        // Actuar
        var destinos = TransicionesOrdenCompra.DestinosDesde(estado);

        // Verificar
        Assert.Empty(destinos);
    }

    [Trait("Fila", "Terminal: Recibida")]
    [Fact]
    public void Recibida_EsTerminal_NingunaTransicionParteDeElla()
    {
        // Preparar
        var estado = EstadoOrdenCompra.Recibida;

        // Actuar
        var destinos = TransicionesOrdenCompra.DestinosDesde(estado);

        // Verificar
        Assert.Empty(destinos);
    }

    [Trait("Fila", "Terminal: Cancelada")]
    [Fact]
    public void Cancelar_OrdenYaCancelada_SeRechazaYConservaLaFechaOriginal()
    {
        // Preparar
        var orden = OrdenCancelada();

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => orden.Cancelar("Otra vez", Ahora.AddHours(1)));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(Ahora, orden.FechaCancelacion);
    }

    // ---------- La tabla completa: los 16 pares de estados (RD-04) ----------

    [Trait("Fila", "Tabla completa")]
    [Theory]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Borrador, false)]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Emitida, true)]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Recibida, false)]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Cancelada, true)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Borrador, false)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Emitida, false)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Recibida, true)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Cancelada, true)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Borrador, false)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Emitida, false)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Recibida, false)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada, false)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Borrador, false)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Emitida, false)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Recibida, false)]
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Cancelada, false)]
    public void Puede_SoloPermiteLasTransicionesDeLaTabla(EstadoOrdenCompra desde, EstadoOrdenCompra hacia, bool esperado)
    {
        // Preparar: los estados vienen de InlineData

        // Actuar
        var puede = TransicionesOrdenCompra.Puede(desde, hacia);

        // Verificar
        Assert.Equal(esperado, puede);
    }

    [Trait("Fila", "Tabla de prohibidas")]
    [Fact]
    public void ProhibidasExplicitas_NingunaEstaPermitida()
    {
        // Preparar
        var prohibidas = TransicionesOrdenCompra.ProhibidasExplicitas;

        // Actuar
        var permitidas = prohibidas.Where(t => TransicionesOrdenCompra.Puede(t.Desde, t.Hacia)).ToList();

        // Verificar
        Assert.NotEmpty(prohibidas);
        Assert.Empty(permitidas);
    }
}
