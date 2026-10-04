using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Xunit;

namespace Inventario.Core.Tests;

/// <summary>Pruebas del Core sin levantar la API (RD-12).</summary>
public class SeguridadTests
{
    [Fact]
    public void Hash_MismaContrasena_ProduceValoresDistintos() // RF-CA-02, RD-05
    {
        var hasher = new HasherContrasena();
        var a = hasher.Hashear("Clave1234");
        var b = hasher.Hashear("Clave1234");

        Assert.NotEqual(a.Hash, b.Hash);
        Assert.NotEqual(a.Sal, b.Sal);
        Assert.NotEqual("Clave1234", a.Hash);
        Assert.True(hasher.Verificar("Clave1234", a.Hash, a.Sal));
        Assert.False(hasher.Verificar("Otra12345", a.Hash, a.Sal));
    }

    [Fact]
    public void Hash_Inutilizable_NuncaCoincide() // RF-CA-13
    {
        Assert.False(new HasherContrasena().Verificar("Clave1234", "!restablecimiento-forzado", "!"));
    }

    [Theory]
    [InlineData("abc12", false)]      // muy corta
    [InlineData("abcdefgh", false)]   // sin números
    [InlineData("12345678", false)]   // sin letras
    [InlineData("", false)]
    [InlineData("abcd1234", true)]
    public void PoliticaContrasena(string contrasena, bool esperado) // RF-CA-14
    {
        Assert.Equal(esperado, Seguridad.PoliticaContrasena.Cumple(contrasena));
    }

    [Fact]
    public void PoliticaContrasena_Incumplida_LanzaRechazoControlado()
    {
        var ex = Assert.Throws<ExcepcionControlada>(() => Seguridad.PoliticaContrasena.Validar("abc"));
        Assert.Equal(400, ex.CodigoEstado);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("correo-sin-arroba", false)]
    [InlineData("a@b", false)]
    [InlineData("usuario@dominio.com", true)]
    public void ValidacionCorreo(string correo, bool esperado) // RD-07
    {
        Assert.Equal(esperado, ValidadorCorreo.EsValido(correo));
    }

    [Fact]
    public void Tokens_SonAleatorios_YSoloSeGuardaSuHash()
    {
        var gen = new GeneradorTokens();
        var t1 = gen.Generar();
        var t2 = gen.Generar();

        Assert.NotEqual(t1.Token, t2.Token);
        Assert.NotEqual(t1.Token, t1.Hash);
        Assert.Equal(t1.Hash, gen.Hashear(t1.Token));
    }

    [Fact]
    public void Codigo_DeUnSoloUso_YConVencimiento() // RF-CA-10, RF-CA-16
    {
        var ahora = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        var codigo = new CodigoVerificacion { FechaVencimiento = ahora.AddMinutes(30) };

        Assert.True(codigo.EsValido(ahora));
        Assert.False(codigo.EsValido(ahora.AddMinutes(31)));  // vencido
        codigo.Usado = true;
        Assert.False(codigo.EsValido(ahora));                 // ya usado
    }

    [Fact]
    public void Politicas_EstandarNoPuedeEjecutarOperacionDeAdministrador() // RF-CA-05, RF-CA-06
    {
        var exigencia = PoliticasAcceso.Obtener(Operaciones.ListarUsuarios);

        Assert.False(exigencia.Permite(autenticado: true, Rol.Estandar));
        Assert.False(exigencia.Permite(autenticado: false, null));
        Assert.True(exigencia.Permite(autenticado: true, Rol.Administrador));
    }
}
