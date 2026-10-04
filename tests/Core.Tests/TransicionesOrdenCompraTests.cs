using Inventario.Negocio.Estados;
using Xunit;

namespace Inventario.Core.Tests;

public class TransicionesOrdenCompraTests
{
    [Theory]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Emitida, true)]
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Cancelada, true)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Recibida, true)]
    [InlineData(EstadoOrdenCompra.Emitida, EstadoOrdenCompra.Cancelada, true)]
    [InlineData(EstadoOrdenCompra.Recibida, EstadoOrdenCompra.Cancelada, false)] // prohibida explícita
    [InlineData(EstadoOrdenCompra.Borrador, EstadoOrdenCompra.Recibida, false)]  // prohibida explícita
    [InlineData(EstadoOrdenCompra.Cancelada, EstadoOrdenCompra.Borrador, false)]
    public void Puede(EstadoOrdenCompra desde, EstadoOrdenCompra hacia, bool esperado)
    {
        Assert.Equal(esperado, TransicionesOrdenCompra.Puede(desde, hacia));
    }

    [Fact]
    public void RecibidaYCancelada_SonTerminales()
    {
        Assert.True(TransicionesOrdenCompra.EsTerminal(EstadoOrdenCompra.Recibida));
        Assert.True(TransicionesOrdenCompra.EsTerminal(EstadoOrdenCompra.Cancelada));
        Assert.False(TransicionesOrdenCompra.EsTerminal(EstadoOrdenCompra.Borrador));
    }

    [Fact]
    public void ProhibidasExplicitas_NoEstanPermitidas()
    {
        Assert.NotEmpty(TransicionesOrdenCompra.ProhibidasExplicitas);
        foreach (var (desde, hacia) in TransicionesOrdenCompra.ProhibidasExplicitas)
            Assert.False(TransicionesOrdenCompra.Puede(desde, hacia));
    }
}
