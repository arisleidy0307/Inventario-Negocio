using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Notificaciones;
using Inventario.Core.Seguridad;
using Xunit;

namespace Inventario.Core.Tests.Practica5;

/// <summary>Motor de notificaciones (RF-NOT-01 a 07), criterios CA-NOT-01 a 07 de docs/criterios-notificaciones-reportes.md.</summary>
public sealed class NotificacionesTests : IDisposable
{
    private readonly EntornoSqlite _e = new();

    public void Dispose() => _e.Dispose();

    // ---------- CA-NOT-01 (RF-NOT-01) ----------

    [Trait("Requisito", "RF-NOT-01")]
    [Fact]
    public async Task CrearSolicitud_NotificaACadaAdministradorActivoYANadieMas()
    {
        // Preparar
        var admin1 = await _e.UsuarioAsync("Admin1", Rol.Administrador);
        var admin2 = await _e.UsuarioAsync("Admin2", Rol.Administrador);
        var adminDesactivado = await _e.UsuarioAsync("AdminDesactivado", Rol.Administrador, deshabilitado: true);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);

        // Actuar
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.RecibirOrden, "Recibo la mercancía");

        // Verificar
        foreach (var admin in new[] { admin1, admin2 })
        {
            var n = Assert.Single(await _e.NotificacionesDeAsync(admin.Id));
            Assert.Equal(TipoNotificacion.SolicitudCreada, n.Tipo);
            Assert.False(n.Leida);
            Assert.Contains(Operaciones.RecibirOrden, n.Mensaje);
            Assert.Contains("Almacen", n.Mensaje);
            Assert.Contains($"#{solicitud.Id}", n.Titulo);
        }
        Assert.Empty(await _e.NotificacionesDeAsync(adminDesactivado.Id));
        Assert.Empty(await _e.NotificacionesDeAsync(a.Id));
        Assert.Empty(await _e.NotificacionesDeAsync(b.Id));
    }

    // ---------- CA-NOT-02 (RF-NOT-02) ----------

    [Trait("Requisito", "RF-NOT-02")]
    [Fact]
    public async Task AprobarSolicitud_NotificaAlSolicitanteQueFueAprobadaYANadieMas()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.RecibirOrden, null);
        var delAdminAntes = (await _e.NotificacionesDeAsync(admin.Id)).Count;

        // Actuar
        await _e.Permisos.AprobarAsync(solicitud.Id, admin.Id);

        // Verificar
        var n = Assert.Single(await _e.NotificacionesDeAsync(a.Id));
        Assert.Equal(TipoNotificacion.SolicitudAprobada, n.Tipo);
        Assert.Contains("aprobada", n.Titulo);
        Assert.Contains(Operaciones.RecibirOrden, n.Mensaje);
        Assert.Equal(delAdminAntes, (await _e.NotificacionesDeAsync(admin.Id)).Count);
        Assert.Empty(await _e.NotificacionesDeAsync(b.Id));
    }

    [Trait("Requisito", "RF-NOT-02")]
    [Fact]
    public async Task RechazarSolicitud_NotificaAlSolicitanteQueFueRechazadaConElMotivo()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.ListarUsuarios, null);

        // Actuar
        await _e.Permisos.RechazarAsync(solicitud.Id, admin.Id, "No corresponde a tu puesto");

        // Verificar
        var n = Assert.Single(await _e.NotificacionesDeAsync(a.Id));
        Assert.Equal(TipoNotificacion.SolicitudRechazada, n.Tipo);
        Assert.Contains("rechazada", n.Titulo);
        Assert.Contains("No corresponde a tu puesto", n.Mensaje);
    }

    [Trait("Requisito", "RF-NOT-02")]
    [Fact]
    public async Task AprobarSolicitudYaRechazada_SeRechazaYNoGeneraNotificacion()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.ListarUsuarios, null);
        await _e.Permisos.RechazarAsync(solicitud.Id, admin.Id, "No corresponde");

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => _e.Permisos.AprobarAsync(solicitud.Id, admin.Id));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Single(await _e.NotificacionesDeAsync(a.Id)); // solo la del rechazo
    }

    // ---------- CA-NOT-03 (RF-NOT-03) ----------

    [Trait("Requisito", "RF-NOT-03")]
    [Fact]
    public async Task EmitirYRecibirOrden_NotificanAQuienLaCreoConNumeroYTotal()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var (prov, prod) = await _e.CatalogoAsync();
        var orden = await _e.Ordenes.CrearAsync(a.Id, prov.Id);
        await _e.Ordenes.AgregarDetalleAsync(orden.Id, a.Id, Rol.Estandar, prod.Id, 5, 850m);

        // Actuar
        await _e.Ordenes.EmitirAsync(orden.Id, admin.Id);
        await _e.Ordenes.RecibirAsync(orden.Id, admin.Id, Rol.Administrador);

        // Verificar
        var notificaciones = (await _e.NotificacionesDeAsync(a.Id)).OrderBy(n => n.Id).ToList();
        Assert.Equal(new[] { TipoNotificacion.OrdenEmitida, TipoNotificacion.OrdenRecibida }, notificaciones.Select(n => n.Tipo));
        Assert.Contains($"#{orden.Id}", notificaciones[0].Titulo);
        Assert.Contains("4,250.00 DOP", notificaciones[0].Mensaje);
        Assert.Contains($"#{orden.Id}", notificaciones[1].Titulo);
        Assert.Empty(await _e.NotificacionesDeAsync(admin.Id));
    }

    [Trait("Requisito", "RF-NOT-03")]
    [Fact]
    public async Task EmitirOrdenPropia_NoSeLeNotificaAQuienHizoLaAccion()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var (prov, prod) = await _e.CatalogoAsync();
        var orden = await _e.Ordenes.CrearAsync(admin.Id, prov.Id);
        await _e.Ordenes.AgregarDetalleAsync(orden.Id, admin.Id, Rol.Administrador, prod.Id, 1, 10m);

        // Actuar
        await _e.Ordenes.EmitirAsync(orden.Id, admin.Id);

        // Verificar
        Assert.Empty(await _e.NotificacionesDeAsync(admin.Id));
    }

    // ---------- CA-NOT-04 (RF-NOT-04) ----------

    [Trait("Requisito", "RF-NOT-04")]
    [Fact]
    public async Task Bandeja_SoloTraeLasPropiasYCuentaLasNoLeidas()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "Para A 1", "x");
        _e.Reloj.UtcNow = _e.Reloj.UtcNow.AddMinutes(1);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenRecibida, "Para A 2", "x");
        await _e.Notificaciones.NotificarAsync(b.Id, TipoNotificacion.OrdenEmitida, "Para B", "x");

        // Actuar
        var deA = await _e.Notificaciones.BandejaAsync(a.Id, soloNoLeidas: false);
        var deB = await _e.Notificaciones.BandejaAsync(b.Id, soloNoLeidas: false);

        // Verificar
        Assert.Equal(new[] { "Para A 2", "Para A 1" }, deA.Notificaciones.Select(n => n.Titulo)); // la más nueva primero
        Assert.Equal(2, deA.NoLeidas);
        Assert.Equal("Para B", Assert.Single(deB.Notificaciones).Titulo);
        Assert.Empty(deA.Notificaciones.Select(n => n.Id).Intersect(deB.Notificaciones.Select(n => n.Id)));
    }

    [Trait("Requisito", "RF-NOT-04")]
    [Fact]
    public async Task Bandeja_SoloNoLeidas_DejaFueraLasLeidas()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "Leída", "x");
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenRecibida, "Sin leer", "x");
        var leida = (await _e.NotificacionesDeAsync(a.Id)).Single(n => n.Titulo == "Leída");
        await _e.Notificaciones.MarcarLeidaAsync(leida.Id, a.Id);

        // Actuar
        var bandeja = await _e.Notificaciones.BandejaAsync(a.Id, soloNoLeidas: true);

        // Verificar
        Assert.Equal("Sin leer", Assert.Single(bandeja.Notificaciones).Titulo);
        Assert.Equal(1, bandeja.NoLeidas);
    }

    // ---------- CA-NOT-05 (RF-NOT-05) ----------

    [Trait("Requisito", "RF-NOT-05")]
    [Fact]
    public async Task MarcarLeida_Propia_QuedaLeidaConFechaYBajaElContador()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "Hola", "x");
        var id = Assert.Single(await _e.NotificacionesDeAsync(a.Id)).Id;
        _e.Reloj.UtcNow = _e.Reloj.UtcNow.AddMinutes(5);

        // Actuar
        var marcada = await _e.Notificaciones.MarcarLeidaAsync(id, a.Id);
        var otraVez = await _e.Notificaciones.MarcarLeidaAsync(id, a.Id);

        // Verificar
        Assert.True(marcada.Leida);
        Assert.Equal(_e.Reloj.UtcNow, otraVez.FechaLectura);
        Assert.Equal(0, (await _e.Notificaciones.BandejaAsync(a.Id, false)).NoLeidas);
    }

    [Trait("Requisito", "RF-NOT-05")]
    [Fact]
    public async Task MarcarLeida_DeOtroUsuario_SeRechazaYSigueSinLeer()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var b = await _e.UsuarioAsync("Compras", Rol.Estandar);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "De A", "x");
        var id = Assert.Single(await _e.NotificacionesDeAsync(a.Id)).Id;

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => _e.Notificaciones.MarcarLeidaAsync(id, b.Id));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.False(Assert.Single(await _e.NotificacionesDeAsync(a.Id)).Leida);
    }

    [Trait("Requisito", "RF-NOT-05")]
    [Fact]
    public async Task MarcarLeida_Inexistente_SeRechazaConNoEncontrado()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => _e.Notificaciones.MarcarLeidaAsync(999, a.Id));

        // Verificar
        Assert.Equal(404, error.CodigoEstado);
    }

    // ---------- CA-NOT-06 (RF-NOT-06) ----------

    [Trait("Requisito", "RF-NOT-06")]
    [Fact]
    public async Task Notificacion_LlegaALaBandejaYEncolaUnCorreoConElTitulo()
    {
        // Preparar
        var admin = await _e.UsuarioAsync("Admin", Rol.Administrador);
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        var solicitud = await _e.Permisos.CrearSolicitudAsync(a.Id, Operaciones.ListarUsuarios, null);

        // Actuar
        await _e.Permisos.RechazarAsync(solicitud.Id, admin.Id, "No corresponde");

        // Verificar
        var n = Assert.Single(await _e.NotificacionesDeAsync(a.Id));
        var correo = Assert.Single(await _e.CorreosParaAsync(a.Correo));
        Assert.Equal(n.Titulo, correo.Asunto);
        Assert.Equal(EstadoCorreo.Pendiente, correo.Estado);
        Assert.Contains("No corresponde", correo.Cuerpo);
        Assert.Single(await _e.CorreosParaAsync(admin.Correo)); // el aviso de la solicitud nueva
    }

    // ---------- CA-NOT-07 (RF-NOT-07) ----------

    [Trait("Requisito", "RF-NOT-07")]
    [Fact]
    public async Task CorreoDesactivado_LaNotificacionLlegaALaBandejaYNoSeEncolaCorreo()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        await _e.Notificaciones.CambiarCorreoAsync(a.Id, recibirCorreo: false);

        // Actuar
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "Orden emitida", "x");

        // Verificar
        Assert.False(await _e.Notificaciones.RecibeCorreoAsync(a.Id));
        Assert.Single(await _e.NotificacionesDeAsync(a.Id));
        Assert.Empty(await _e.CorreosParaAsync(a.Correo));
    }

    [Trait("Requisito", "RF-NOT-07")]
    [Fact]
    public async Task CorreoReactivado_LaSiguienteNotificacionVuelveAEncolarCorreo()
    {
        // Preparar
        var a = await _e.UsuarioAsync("Almacen", Rol.Estandar);
        await _e.Notificaciones.CambiarCorreoAsync(a.Id, false);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenEmitida, "Sin correo", "x");

        // Actuar
        await _e.Notificaciones.CambiarCorreoAsync(a.Id, true);
        await _e.Notificaciones.NotificarAsync(a.Id, TipoNotificacion.OrdenRecibida, "Con correo", "x");

        // Verificar
        Assert.Equal(2, (await _e.NotificacionesDeAsync(a.Id)).Count);
        Assert.Equal("Con correo", Assert.Single(await _e.CorreosParaAsync(a.Correo)).Asunto);
    }

    // ---------- RD-06: sesión obligatoria ----------

    [Trait("Requisito", "RD-06")]
    [Theory]
    [InlineData(Operaciones.MisNotificaciones)]
    [InlineData(Operaciones.MarcarNotificacionLeida)]
    [InlineData(Operaciones.PreferenciasNotificaciones)]
    public void OperacionDeNotificaciones_SinSesion_SeRechaza(string operacion)
    {
        // Preparar
        var exigencia = PoliticasAcceso.Obtener(operacion);

        // Actuar
        var sinSesion = PoliticasAcceso.Permite(operacion, autenticado: false, rol: null, permisosOtorgados: null);

        // Verificar
        Assert.True(exigencia.RequiereSesion);
        Assert.False(sinSesion);
    }
}
