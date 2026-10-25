using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Xunit;

namespace Inventario.Core.Tests.GestionPermisos;

/// <summary>
/// Quién puede ejecutar cada operación de Gestión de permisos, según el punto único de exigencias
/// (PoliticasAcceso). La API aplica esta misma decisión en el servidor con [RequiereOperacion] (RD-06).
/// </summary>
public class AutorizacionPermisosTests
{
    private static readonly string[] SinPermisosOtorgados = Array.Empty<string>();

    [Trait("Requisito", "RF-GP-01")]
    [Fact]
    public void SolicitarPermiso_UsuarioEstandar_EstaPermitido()
    {
        // Preparar
        var operacion = Operaciones.SolicitarPermiso;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, SinPermisosOtorgados);

        // Verificar
        Assert.True(permitido);
    }

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public void ListarPendientes_UsuarioEstandar_SeRechaza()
    {
        // Preparar
        var operacion = Operaciones.ListarSolicitudesPendientes;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, SinPermisosOtorgados);

        // Verificar
        Assert.False(permitido);
    }

    [Trait("Requisito", "RF-GP-02")]
    [Fact]
    public void ListarPendientes_Administrador_EstaPermitido()
    {
        // Preparar
        var operacion = Operaciones.ListarSolicitudesPendientes;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Administrador, SinPermisosOtorgados);

        // Verificar
        Assert.True(permitido);
    }

    [Trait("Requisito", "RF-GP-03")]
    [Fact]
    public void AprobarSolicitud_UsuarioEstandar_SeRechaza()
    {
        // Preparar
        var operacion = Operaciones.AprobarSolicitud;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, SinPermisosOtorgados);

        // Verificar
        Assert.False(permitido);
    }

    [Trait("Requisito", "RF-GP-03")]
    [Fact]
    public void AprobarSolicitud_Administrador_EstaPermitido()
    {
        // Preparar
        var operacion = Operaciones.AprobarSolicitud;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Administrador, SinPermisosOtorgados);

        // Verificar
        Assert.True(permitido);
    }

    [Trait("Requisito", "RF-GP-04")]
    [Fact]
    public void RechazarSolicitud_UsuarioEstandar_SeRechaza()
    {
        // Preparar
        var operacion = Operaciones.RechazarSolicitud;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, SinPermisosOtorgados);

        // Verificar
        Assert.False(permitido);
    }

    [Trait("Requisito", "RF-GP-03")]
    [Fact]
    public void AprobarSolicitud_SinSesion_SeRechaza()
    {
        // Preparar
        var operacion = Operaciones.AprobarSolicitud;

        // Actuar
        var permitido = PoliticasAcceso.Permite(operacion, autenticado: false, null, SinPermisosOtorgados);

        // Verificar
        Assert.False(permitido);
    }
}
