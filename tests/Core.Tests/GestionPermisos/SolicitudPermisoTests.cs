using Inventario.Core.Comun;
using Inventario.Core.Permisos;
using Xunit;

namespace Inventario.Core.Tests.GestionPermisos;

/// <summary>Reglas de la entidad y de la tabla única de transiciones (RD-04, RF-GP-06).</summary>
public class SolicitudPermisoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Aprobar_SolicitudPendiente_PasaAAprobadaYRegistraQuienYCuando()
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };

        // Actuar
        solicitud.Aprobar(administradorId: 1, Ahora);

        // Verificar
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Equal(1, solicitud.AprobadaPorId);
        Assert.Equal(Ahora, solicitud.FechaAprobacion);
    }

    [Fact]
    public void Aplicar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada()
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };
        solicitud.Rechazar(administradorId: 1, "No corresponde", Ahora);

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => solicitud.Aplicar(Ahora.AddMinutes(5)));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Null(solicitud.FechaAplicacion);
    }

    [Fact]
    public void Aplicar_SolicitudPendienteSinAprobar_SeRechazaYSiguePendiente()
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => solicitud.Aplicar(Ahora));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }

    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada, true)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada, true)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada, true)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Rechazada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Pendiente, false)]
    public void Puede_SoloPermiteLasTransicionesDeLaTabla(
        EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia, bool esperado)
    {
        // Preparar: los estados vienen de InlineData

        // Actuar
        var puede = TransicionesSolicitudPermiso.Puede(desde, hacia);

        // Verificar
        Assert.Equal(esperado, puede);
    }
}
