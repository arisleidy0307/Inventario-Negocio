using Inventario.Core.Comun;
using Inventario.Core.Permisos;
using Xunit;

namespace Inventario.Core.Tests.GestionPermisos;

/// <summary>Reglas de la entidad y de la tabla única de transiciones (RD-04, RF-GP-06).</summary>
public class SolicitudPermisoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 15, 30, 0, DateTimeKind.Utc);

    [Trait("Requisito", "RF-GP-03")]
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

    [Trait("Requisito", "RF-GP-06")]
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

    [Trait("Requisito", "RF-GP-06")]
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

    // Los 16 pares posibles de estados: solo 3 están en la tabla (RD-04, RF-GP-06).
    [Trait("Requisito", "RF-GP-06")]
    [Theory]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Pendiente, false)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada, true)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada, true)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Pendiente, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada, true)]
    [InlineData(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Rechazada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Pendiente, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Aplicada, EstadoSolicitudPermiso.Rechazada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Pendiente, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aprobada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Aplicada, false)]
    [InlineData(EstadoSolicitudPermiso.Rechazada, EstadoSolicitudPermiso.Rechazada, false)]
    public void Puede_SoloPermiteLasTransicionesDeLaTabla(
        EstadoSolicitudPermiso desde, EstadoSolicitudPermiso hacia, bool esperado)
    {
        // Preparar: los estados vienen de InlineData

        // Actuar
        var puede = TransicionesSolicitudPermiso.Puede(desde, hacia);

        // Verificar
        Assert.Equal(esperado, puede);
    }

    [Trait("Requisito", "RF-GP-06")]
    [Fact]
    public void Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada()
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };
        solicitud.Aprobar(administradorId: 1, Ahora);

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(
            () => solicitud.Rechazar(administradorId: 1, "Cambio de opinión", Ahora.AddMinutes(1)));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Aprobada, solicitud.Estado);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Trait("Requisito", "RF-GP-06")]
    [Fact]
    public void Rechazar_SolicitudYaRechazada_SeRechazaYConservaElPrimerMotivo()
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };
        solicitud.Rechazar(administradorId: 1, "Primer motivo", Ahora);

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(
            () => solicitud.Rechazar(administradorId: 1, "Segundo motivo", Ahora.AddMinutes(1)));

        // Verificar
        Assert.Equal(409, error.CodigoEstado);
        Assert.Equal("Primer motivo", solicitud.MotivoRechazo);
        Assert.Equal(Ahora, solicitud.FechaRechazo);
    }

    [Trait("Requisito", "RF-GP-04")]
    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rechazar_MotivoAusenteOVacio_SeRechazaYSiguePendiente(string? motivo)
    {
        // Preparar
        var solicitud = new SolicitudPermiso { SolicitanteId = 10, Operacion = "Usuarios.Listar" };

        // Actuar
        var error = Assert.Throws<ExcepcionControlada>(() => solicitud.Rechazar(administradorId: 1, motivo, Ahora));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
    }
}
