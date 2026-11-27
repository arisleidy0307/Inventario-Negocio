using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Inventario.EnviadorCorreos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventario.Core.Tests.Practica5;

/// <summary>Cola de correos (RF-NOT-08 a 12), criterios CA-COL-01 a 05. El ProcesadorCola real sobre SQLite.</summary>
public sealed class ColaCorreosTests : IDisposable
{
    private readonly EntornoSqlite _e = new();

    public void Dispose() => _e.Dispose();

    /// <summary>Envío falso: anota lo que «envía» o falla siempre, como un servidor SMTP apagado.</summary>
    private sealed class EnvioFalso : IEnvioCorreo
    {
        private readonly bool _falla;
        public List<int> Enviados { get; } = new();
        public EnvioFalso(bool falla) => _falla = falla;

        public Task EnviarAsync(CorreoEnCola correo)
        {
            if (_falla)
                throw new InvalidOperationException("Failure sending mail.");
            Enviados.Add(correo.Id);
            return Task.CompletedTask;
        }
    }

    private async Task<int> EncolarAsync(string destinatario = "almacen@stockcore.test")
    {
        var correo = new CorreoEnCola
        {
            Destinatario = destinatario, Asunto = "Activa tu cuenta - Inventario", Cuerpo = "enlace",
            Estado = EstadoCorreo.Pendiente, FechaCreacion = _e.Reloj.UtcNow
        };
        _e.Db.CorreosEnCola.Add(correo);
        await _e.Db.SaveChangesAsync();
        return correo.Id;
    }

    private async Task<ResultadoProcesamiento> ProcesarAsync(IEnvioCorreo envio, int maxIntentos = 3)
    {
        await using var db = _e.NuevoContexto();
        return await new ProcesadorCola(db, envio, _e.Reloj, maxIntentos).ProcesarAsync();
    }

    private async Task<CorreoEnCola> LeerAsync(int id)
    {
        await using var db = _e.NuevoContexto();
        return await db.CorreosEnCola.AsNoTracking().SingleAsync(c => c.Id == id);
    }

    // ---------- CA-COL-01 (RF-NOT-08) ----------

    [Trait("Requisito", "RF-NOT-08")]
    [Fact]
    public async Task CrearSolicitud_SinEnviadorCorriendo_TerminaBienYElCorreoQuedaPendiente()
    {
        // Preparar: nadie procesa la cola
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);

        // Actuar
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.ListarUsuarios, null);

        // Verificar
        Assert.True(solicitud.Id > 0);
        var correo = Assert.Single(await _e.CorreosParaAsync(admin.Correo));
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Equal(0, correo.Intentos);
    }

    // ---------- CA-COL-02 (RF-NOT-09) ----------

    [Trait("Requisito", "RF-NOT-09")]
    [Fact]
    public async Task Procesar_CorreoPendiente_QuedaEnviadoConFechaEIntentoUno()
    {
        // Preparar
        var id = await EncolarAsync();
        var envio = new EnvioFalso(falla: false);

        // Actuar
        var resultado = await ProcesarAsync(envio);

        // Verificar
        var correo = await LeerAsync(id);
        Assert.Equal(1, resultado.Enviados);
        Assert.Equal(EstadoCorreo.Enviado, correo.Estado);
        Assert.Equal(1, correo.Intentos);
        Assert.Equal(_e.Reloj.UtcNow, correo.FechaEnvio);
        Assert.Equal(_e.Reloj.UtcNow, correo.FechaUltimoIntento);
        Assert.Null(correo.UltimoError);
    }

    // ---------- CA-COL-03 (RF-NOT-10) ----------

    [Trait("Requisito", "RF-NOT-10")]
    [Fact]
    public async Task Procesar_EnvioQueFalla_SeReintentaHastaElMaximoYQuedaFallidoConSuError()
    {
        // Preparar
        var id = await EncolarAsync();
        var smtpApagado = new EnvioFalso(falla: true);

        // Actuar y verificar cada pasada
        var primera = await ProcesarAsync(smtpApagado);
        var trasPrimera = await LeerAsync(id);
        await ProcesarAsync(smtpApagado);
        var tercera = await ProcesarAsync(smtpApagado);
        var final = await LeerAsync(id);

        Assert.Equal(1, primera.Reintentaran);
        Assert.Equal((EstadoCorreo.Pendiente, 1), (trasPrimera.Estado, trasPrimera.Intentos));
        Assert.Equal("Failure sending mail.", trasPrimera.UltimoError);
        Assert.Equal(1, tercera.Fallidos);
        Assert.Equal(EstadoCorreo.Fallido, final.Estado);
        Assert.Equal(3, final.Intentos);
        Assert.Equal("Failure sending mail.", final.UltimoError);
        Assert.NotNull(final.FechaUltimoIntento);
        Assert.Null(final.FechaEnvio);
    }

    [Trait("Requisito", "RF-NOT-10")]
    [Fact]
    public async Task Procesar_CorreoFallido_YaNoSeVuelveAIntentarNiSeBorra()
    {
        // Preparar: agota sus 2 intentos
        var id = await EncolarAsync();
        var smtpApagado = new EnvioFalso(falla: true);
        await ProcesarAsync(smtpApagado, maxIntentos: 2);
        await ProcesarAsync(smtpApagado, maxIntentos: 2);

        // Actuar: el servidor vuelve, pero el correo ya está Fallido
        var envio = new EnvioFalso(falla: false);
        var resultado = await ProcesarAsync(envio, maxIntentos: 2);

        // Verificar
        var correo = await LeerAsync(id);
        Assert.Equal(0, resultado.Enviados);
        Assert.Empty(envio.Enviados);
        Assert.Equal((EstadoCorreo.Fallido, 2), (correo.Estado, correo.Intentos));
    }

    [Trait("Requisito", "RF-NOT-10")]
    [Fact]
    public async Task Procesar_FallaUnaVezYLuegoSale_QuedaEnviadoSinError()
    {
        // Preparar
        var id = await EncolarAsync();
        await ProcesarAsync(new EnvioFalso(falla: true));

        // Actuar
        await ProcesarAsync(new EnvioFalso(falla: false));

        // Verificar
        var correo = await LeerAsync(id);
        Assert.Equal((EstadoCorreo.Enviado, 2), (correo.Estado, correo.Intentos));
        Assert.Null(correo.UltimoError);
    }

    // ---------- CA-COL-05 (RF-NOT-12) ----------

    [Trait("Requisito", "RF-NOT-12")]
    [Fact]
    public async Task Procesar_DosVecesSeguidas_NoDuplicaElEnvio()
    {
        // Preparar
        var id = await EncolarAsync();
        var envio = new EnvioFalso(falla: false);

        // Actuar
        var primera = await ProcesarAsync(envio);
        var segunda = await ProcesarAsync(envio);

        // Verificar
        Assert.Equal(1, primera.Enviados);
        Assert.Equal(0, segunda.Enviados);
        Assert.Equal(new[] { id }, envio.Enviados);
        Assert.Equal(1, (await LeerAsync(id)).Intentos);
    }

    // ---------- CA-COL-04 (RF-NOT-11) ----------

    [Trait("Requisito", "RF-NOT-11")]
    [Fact]
    public async Task ConsultarCola_MuestraEstadoIntentosFechasYErrorSinElCuerpo_YFiltraPorEstado()
    {
        // Preparar: uno enviado y uno fallido
        var enviado = await EncolarAsync("a@stockcore.test");
        await ProcesarAsync(new EnvioFalso(falla: false));
        var fallido = await EncolarAsync("b@stockcore.test");
        await ProcesarAsync(new EnvioFalso(falla: true), maxIntentos: 1);

        // Actuar
        var todos = await _e.Correos.ListarAsync(null);
        var soloFallidos = await _e.Correos.ListarAsync("fallido");

        // Verificar
        Assert.Equal(new[] { fallido, enviado }, todos.Select(c => c.Id)); // el más nuevo primero
        var f = Assert.Single(soloFallidos);
        Assert.Equal((fallido, EstadoCorreo.Fallido, 1, "Failure sending mail."), (f.Id, f.Estado, f.Intentos, f.UltimoError));
        Assert.NotNull(f.FechaUltimoIntento);
    }

    [Trait("Requisito", "RF-NOT-11")]
    [Fact]
    public void ConsultarCola_LaRespuestaNoTieneElCuerpoDelCorreo()
    {
        // Preparar
        var propiedades = typeof(Inventario.Core.Servicios.EstadoCorreoEnCola).GetProperties().Select(p => p.Name);

        // Actuar
        var tieneCuerpo = propiedades.Contains("Cuerpo");

        // Verificar
        Assert.False(tieneCuerpo);
    }

    [Trait("Requisito", "RF-NOT-11")]
    [Fact]
    public async Task ConsultarCola_EstadoInvalido_SeRechazaConSolicitudInvalida()
    {
        // Preparar
        var estado = "Perdido";

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => _e.Correos.ListarAsync(estado));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
    }

    [Trait("Requisito", "RF-NOT-11")]
    [Fact]
    public void ConsultarCola_SoloElAdministrador()
    {
        // Preparar
        var operacion = Operaciones.ListarCorreos;

        // Actuar
        var admin = PoliticasAcceso.Permite(operacion, true, Rol.Administrador, null);
        var estandar = PoliticasAcceso.Permite(operacion, true, Rol.Estandar, null);
        var sinSesion = PoliticasAcceso.Permite(operacion, false, null, null);

        // Verificar
        Assert.True(admin);
        Assert.False(estandar);
        Assert.False(sinSesion);
    }
}
