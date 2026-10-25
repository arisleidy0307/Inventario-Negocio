using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Permisos;
using Inventario.Core.Seguridad;
using Xunit;

namespace Inventario.Core.Tests.GestionPermisos;

/// <summary>
/// Pruebas de Gestión de permisos (RF-GP-01 a 07) sin levantar la API ni la base de datos (RD-12).
/// Cada prueba arma su propio repositorio en memoria y su propio reloj: son independientes y
/// pasan en cualquier orden. Fases: // Preparar, // Actuar, // Verificar.
/// </summary>
public class ServicioPermisosTests
{
    private const int EstandarId = 10;
    private const int AdminId = 1;
    private const int OtroEstandarId = 20;
    private static readonly DateTime Ahora = new(2026, 10, 20, 15, 30, 0, DateTimeKind.Utc);
    private const string OperacionOtorgable = Operaciones.ListarUsuarios;

    private static (ServicioPermisos Servicio, RepositorioPermisosEnMemoria Repo, RelojFijo Reloj) Crear()
    {
        var repo = new RepositorioPermisosEnMemoria();
        var reloj = new RelojFijo(Ahora);
        return (new ServicioPermisos(repo, reloj), repo, reloj);
    }

    // ---------- RF-GP-01 ----------

    [Trait("Requisito", "RF-GP-01")]
    [Fact]
    public async Task CrearSolicitud_UsuarioEstandar_NaceEnEstadoPendiente()
    {
        // Preparar
        var (servicio, _, _) = Crear();

        // Actuar
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, "Necesito ver el listado");

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    [Trait("Requisito", "RF-GP-01")]
    [Fact]
    public async Task CrearSolicitud_OperacionQueNoSePuedeSolicitar_SeRechazaConError400()
    {
        // Preparar
        var (servicio, repo, _) = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.CrearSolicitudAsync(EstandarId, Operaciones.CambiarRol, null));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Empty(repo.Solicitudes);
    }

    [Trait("Requisito", "RF-GP-01")]
    [Fact]
    public async Task CrearSolicitud_YaHayUnaPendienteParaLaMismaOperacion_SeRechazaConError409()
    {
        // Preparar
        var (servicio, repo, _) = Crear();
        await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Single(repo.Solicitudes);
    }

    // ---------- RF-GP-02 ----------

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public async Task ObtenerSolicitud_EstandarPideLaDeOtroUsuario_SeRechazaConError403()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var ajena = await servicio.CrearSolicitudAsync(OtroEstandarId, OperacionOtorgable, null);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.ObtenerAsync(ajena.Id, EstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
    }

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public async Task Pendientes_HaySolicitudesResueltas_SoloDevuelveLasPendientes()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var pendiente = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);
        var aprobada = await servicio.CrearSolicitudAsync(OtroEstandarId, OperacionOtorgable, null);
        await servicio.AprobarAsync(aprobada.Id, AdminId);

        // Actuar
        var resultado = await servicio.PendientesAsync();

        // Verificar
        Assert.Equal(new[] { pendiente.Id }, resultado.Select(s => s.Id).ToArray());
    }

    // ---------- RF-GP-03 / RF-GP-05: transición válida ----------

    [Trait("Requisito", "RF-GP-03")]
    [Fact]
    public async Task Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        var resultado = await servicio.AprobarAsync(solicitud.Id, AdminId);

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, resultado.Estado);
        Assert.Equal(AdminId, resultado.AprobadaPorId);
        Assert.Equal(Ahora, resultado.FechaAprobacion);
    }

    [Trait("Requisito", "RF-GP-05")]
    [Fact]
    public async Task Aprobar_SolicitudPendiente_ElSolicitantePuedeEjecutarLaOperacionSinPasoManual()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        await servicio.AprobarAsync(solicitud.Id, AdminId);

        // Verificar
        var permisosDespues = await servicio.PermisosDeUsuarioAsync(EstandarId);
        Assert.True(PoliticasAcceso.Permite(OperacionOtorgable, true, Rol.Estandar, permisosDespues));
    }

    // ---------- RF-GP-04: transición válida con motivo ----------

    [Trait("Requisito", "RF-GP-04")]
    [Fact]
    public async Task Rechazar_ConMotivo_QuedaRechazadaYRegistraMotivoQuienYCuando()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        var resultado = await servicio.RechazarAsync(solicitud.Id, AdminId, "  No corresponde a tu puesto  ");

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, resultado.Estado);
        Assert.Equal("No corresponde a tu puesto", resultado.MotivoRechazo);
        Assert.Equal(AdminId, resultado.RechazadaPorId);
        Assert.Equal(Ahora, resultado.FechaRechazo);
    }

    [Trait("Requisito", "RF-GP-04")]
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSiguePendiente(string? motivo)
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.RechazarAsync(solicitud.Id, AdminId, motivo));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.RechazadaPorId);
    }

    // ---------- RF-GP-06: transiciones prohibidas ----------

    [Trait("Requisito", "RF-GP-06")]
    [Fact]
    public async Task Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia()
    {
        // Preparar
        var (servicio, repo, reloj) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);
        await servicio.AprobarAsync(solicitud.Id, AdminId);
        reloj.UtcNow = Ahora.AddHours(1);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => servicio.AprobarAsync(solicitud.Id, AdminId));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(Ahora, solicitud.FechaAprobacion);
        Assert.Single(repo.Permisos);
    }

    [Trait("Requisito", "RF-GP-06")]
    [Fact]
    public async Task Aprobar_SolicitudRechazada_SeRechazaYSigueRechazada()
    {
        // Preparar
        var (servicio, repo, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);
        await servicio.RechazarAsync(solicitud.Id, AdminId, "No aplica");

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => servicio.AprobarAsync(solicitud.Id, AdminId));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Null(solicitud.AprobadaPorId);
        Assert.Empty(repo.Permisos);
    }

    [Trait("Requisito", "RF-GP-06")]
    [Fact]
    public async Task Rechazar_SolicitudYaAplicada_SeRechazaYSigueAplicada()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);
        await servicio.AprobarAsync(solicitud.Id, AdminId);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.RechazarAsync(solicitud.Id, AdminId, "Me arrepentí"));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
    }

    // ---------- RF-GP-07 ----------

    [Trait("Requisito", "RF-GP-07")]
    [Fact]
    public async Task Aprobar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente()
    {
        // Preparar
        var (servicio, repo, _) = Crear();
        var propia = await servicio.CrearSolicitudAsync(AdminId, OperacionOtorgable, null);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() => servicio.AprobarAsync(propia.Id, AdminId));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, propia.Estado);
        Assert.Empty(repo.Permisos);
    }

    [Trait("Requisito", "RF-GP-07")]
    [Fact]
    public async Task Rechazar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var propia = await servicio.CrearSolicitudAsync(AdminId, OperacionOtorgable, null);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(
            () => servicio.RechazarAsync(propia.Id, AdminId, "Motivo cualquiera"));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, propia.Estado);
    }

    [Trait("Requisito", "RF-GP-05")]
    [Fact]
    public async Task Aprobar_SolicitudPendiente_LlegaAAplicadaConFechaDeAplicacion()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var solicitud = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        await servicio.AprobarAsync(solicitud.Id, AdminId);

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(Ahora, solicitud.FechaAplicacion);
    }

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public async Task SolicitudesDeUsuario_DevuelveSoloLasDeEseUsuario()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var propia = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);
        await servicio.CrearSolicitudAsync(OtroEstandarId, OperacionOtorgable, null);

        // Actuar
        var resultado = await servicio.DeUsuarioAsync(EstandarId);

        // Verificar
        Assert.Equal(new[] { propia.Id }, resultado.Select(s => s.Id).ToArray());
    }

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public async Task ObtenerSolicitud_EstandarPideLaSuya_LaDevuelve()
    {
        // Preparar
        var (servicio, _, _) = Crear();
        var propia = await servicio.CrearSolicitudAsync(EstandarId, OperacionOtorgable, null);

        // Actuar
        var resultado = await servicio.ObtenerAsync(propia.Id, EstandarId, Rol.Estandar);

        // Verificar
        Assert.Equal(propia.Id, resultado.Id);
    }
}
