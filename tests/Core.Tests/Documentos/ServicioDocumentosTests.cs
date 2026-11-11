using System.Text;
using Inventario.Core.Comun;
using Inventario.Core.Documentos;
using Inventario.Core.Dominio;
using Inventario.Core.Seguridad;
using Inventario.Core.Tests.GestionPermisos;
using Xunit;

namespace Inventario.Core.Tests.Documentos;

/// <summary>Manejador de documentos (RF-DOC-01 a 04, RD-06, RD-07) sin base de datos ni API.</summary>
public class ServicioDocumentosTests
{
    private const int AdminId = 1;
    private const int DuenoId = 10;
    private const int OtroEstandarId = 20;
    private const long MaximoBytes = 1024;
    private static readonly DateTime Ahora = new(2026, 11, 10, 14, 30, 0, DateTimeKind.Utc);

    private sealed record Contexto(RepositorioDocumentosEnMemoria Repo, AlmacenArchivosEnMemoria Almacen, ServicioDocumentos Servicio);

    private static Contexto Crear()
    {
        var repo = new RepositorioDocumentosEnMemoria();
        var almacen = new AlmacenArchivosEnMemoria();
        return new Contexto(repo, almacen, new ServicioDocumentos(repo, almacen, new RelojFijo(Ahora), MaximoBytes));
    }

    private static MemoryStream Contenido(string texto) => new(Encoding.UTF8.GetBytes(texto));

    private static Task<Documento> SubirFactura(Contexto c, int usuarioId = DuenoId, string texto = "factura 001")
    {
        var bytes = Encoding.UTF8.GetBytes(texto);
        return c.Servicio.SubirAsync(usuarioId, "factura-001.pdf", "application/pdf", bytes.Length, new MemoryStream(bytes));
    }

    // ---------- RF-DOC-01: subir ----------

    [Trait("Requisito", "RF-DOC-01")]
    [Fact]
    public async Task Subir_ArchivoValido_RegistraNombreTipoTamanoFechaYQuienLoSubio()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var documento = await c.Servicio.SubirAsync(DuenoId, "factura-001.pdf", "application/pdf", 11, Contenido("factura 001"));

        // Verificar
        var guardado = Assert.Single(c.Repo.Todos);
        Assert.Same(documento, guardado);
        Assert.Equal("factura-001.pdf", guardado.NombreOriginal);
        Assert.Equal("application/pdf", guardado.TipoContenido);
        Assert.Equal(11, guardado.TamanoBytes);
        Assert.Equal(Ahora, guardado.FechaSubida);
        Assert.Equal(DuenoId, guardado.SubidoPorUsuarioId);
        Assert.False(guardado.Eliminado);
        Assert.Equal("factura 001", Encoding.UTF8.GetString(c.Almacen.Archivos[guardado.NombreAlmacenado]));
    }

    [Trait("Requisito", "RD-07")]
    [Fact]
    public async Task Subir_SinArchivo_SeRechazaConMensajeYNoRegistraNada()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.SubirAsync(DuenoId, null, null, 0, null));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Contains("archivo", error.Message);
        Assert.Empty(c.Repo.Todos);
        Assert.Empty(c.Almacen.Archivos);
    }

    [Trait("Requisito", "RD-07")]
    [Fact]
    public async Task Subir_ArchivoVacio_SeRechazaYNoRegistraNada()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.SubirAsync(DuenoId, "vacio.txt", "text/plain", 0, new MemoryStream()));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Equal("El archivo está vacío.", error.Message);
        Assert.Empty(c.Repo.Todos);
    }

    [Trait("Requisito", "RD-07")]
    [Fact]
    public async Task Subir_ArchivoMayorQueElMaximo_SeRechazaYNoRegistraNada()
    {
        // Preparar
        var c = Crear();
        var grande = new byte[MaximoBytes + 1];

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.SubirAsync(DuenoId, "grande.bin", "application/octet-stream", grande.Length, new MemoryStream(grande)));

        // Verificar
        Assert.Equal(400, error.CodigoEstado);
        Assert.Empty(c.Repo.Todos);
    }

    [Trait("Requisito", "RF-DOC-01")]
    [Fact]
    public async Task Subir_NombreConCarpetas_GuardaSoloElNombreDelArchivo()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var documento = await c.Servicio.SubirAsync(DuenoId, "../../otra/carpeta/cotizacion.xlsx", null, 4, Contenido("hola"));

        // Verificar
        Assert.Equal("cotizacion.xlsx", documento.NombreOriginal);
        Assert.Equal("application/octet-stream", documento.TipoContenido);
        Assert.DoesNotContain("/", documento.NombreAlmacenado);
    }

    // ---------- RF-DOC-02: listar ----------

    [Trait("Requisito", "RF-DOC-02")]
    [Fact]
    public async Task Listar_Estandar_SoloVeLosSuyosYNoLosEliminados()
    {
        // Preparar
        var c = Crear();
        var propio = await SubirFactura(c);
        var propioEliminado = await SubirFactura(c);
        await SubirFactura(c, OtroEstandarId);
        await c.Servicio.EliminarAsync(propioEliminado.Id, DuenoId, Rol.Estandar);

        // Actuar
        var lista = await c.Servicio.ListarAsync(DuenoId, Rol.Estandar);

        // Verificar
        var unico = Assert.Single(lista);
        Assert.Equal(propio.Id, unico.Id);
    }

    [Trait("Requisito", "RF-DOC-02")]
    [Fact]
    public async Task Listar_Administrador_VeLosDeTodosMenosLosEliminados()
    {
        // Preparar
        var c = Crear();
        var delDueno = await SubirFactura(c);
        var delOtro = await SubirFactura(c, OtroEstandarId);
        var eliminado = await SubirFactura(c, OtroEstandarId);
        await c.Servicio.EliminarAsync(eliminado.Id, AdminId, Rol.Administrador);

        // Actuar
        var lista = await c.Servicio.ListarAsync(AdminId, Rol.Administrador);

        // Verificar
        Assert.Equal(new[] { delDueno.Id, delOtro.Id }, lista.Select(d => d.Id).OrderBy(i => i));
    }

    // ---------- RF-DOC-03: descargar ----------

    [Trait("Requisito", "RF-DOC-03")]
    [Fact]
    public async Task Descargar_Dueno_ObtieneElMismoContenido()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c, texto: "contenido original");

        // Actuar
        var (descargado, contenido) = await c.Servicio.DescargarAsync(documento.Id, DuenoId, Rol.Estandar);

        // Verificar
        using var lector = new StreamReader(contenido);
        Assert.Equal("contenido original", await lector.ReadToEndAsync());
        Assert.Equal("factura-001.pdf", descargado.NombreOriginal);
    }

    [Trait("Requisito", "RF-DOC-03")]
    [Fact]
    public async Task Descargar_AdministradorDocumentoAjeno_LoObtiene()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        var (descargado, contenido) = await c.Servicio.DescargarAsync(documento.Id, AdminId, Rol.Administrador);

        // Verificar
        Assert.Equal(documento.Id, descargado.Id);
        Assert.True(contenido.Length > 0);
    }

    [Trait("Requisito", "RF-DOC-03")]
    [Fact]
    public async Task Descargar_OtroEstandar_SeRechazaConProhibido()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.DescargarAsync(documento.Id, OtroEstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
    }

    [Trait("Requisito", "RF-DOC-03")]
    [Fact]
    public async Task Ver_OtroEstandar_SeRechazaConProhibido()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.ObtenerAsync(documento.Id, OtroEstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
    }

    [Trait("Requisito", "RD-07")]
    [Fact]
    public async Task Descargar_ArchivoQueYaNoEstaEnElAlmacen_SeRechazaConNoEncontrado()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);
        c.Almacen.Archivos.Clear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.DescargarAsync(documento.Id, DuenoId, Rol.Estandar));

        // Verificar
        Assert.Equal(404, error.CodigoEstado);
    }

    [Trait("Requisito", "RF-DOC-03")]
    [Fact]
    public async Task Descargar_DocumentoInexistente_SeRechazaConNoEncontrado()
    {
        // Preparar
        var c = Crear();

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.DescargarAsync(99, AdminId, Rol.Administrador));

        // Verificar
        Assert.Equal(404, error.CodigoEstado);
    }

    // ---------- RF-DOC-04: eliminar (lógico) ----------

    [Trait("Requisito", "RF-DOC-04")]
    [Fact]
    public async Task Eliminar_Dueno_DejaDeListarsePeroElRegistroYElArchivoSiguen()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        await c.Servicio.EliminarAsync(documento.Id, DuenoId, Rol.Estandar);

        // Verificar
        Assert.Empty(await c.Servicio.ListarAsync(DuenoId, Rol.Estandar));
        var registro = Assert.Single(c.Repo.Todos);
        Assert.True(registro.Eliminado);
        Assert.Equal(Ahora, registro.FechaEliminacion);
        Assert.Equal(DuenoId, registro.EliminadoPorUsuarioId);
        Assert.True(c.Almacen.Archivos.ContainsKey(registro.NombreAlmacenado));
    }

    [Trait("Requisito", "RF-DOC-04")]
    [Fact]
    public async Task Eliminar_OtroEstandar_SeRechazaYSigueEnElListado()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        var error = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.EliminarAsync(documento.Id, OtroEstandarId, Rol.Estandar));

        // Verificar
        Assert.Equal(403, error.CodigoEstado);
        Assert.False(documento.Eliminado);
        Assert.Single(await c.Servicio.ListarAsync(DuenoId, Rol.Estandar));
    }

    [Trait("Requisito", "RF-DOC-04")]
    [Fact]
    public async Task Eliminar_AdministradorDocumentoAjeno_LoOcultaDelListado()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);

        // Actuar
        await c.Servicio.EliminarAsync(documento.Id, AdminId, Rol.Administrador);

        // Verificar
        Assert.True(documento.Eliminado);
        Assert.Equal(AdminId, documento.EliminadoPorUsuarioId);
        Assert.Empty(await c.Servicio.ListarAsync(AdminId, Rol.Administrador));
    }

    [Trait("Requisito", "RF-DOC-04")]
    [Fact]
    public async Task DescargarOEliminar_DocumentoYaEliminado_SeRechazaConNoEncontrado()
    {
        // Preparar
        var c = Crear();
        var documento = await SubirFactura(c);
        await c.Servicio.EliminarAsync(documento.Id, DuenoId, Rol.Estandar);

        // Actuar
        var descargar = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.DescargarAsync(documento.Id, DuenoId, Rol.Estandar));
        var eliminarOtraVez = await Assert.ThrowsAsync<ExcepcionControlada>(() =>
            c.Servicio.EliminarAsync(documento.Id, DuenoId, Rol.Estandar));

        // Verificar
        Assert.Equal(404, descargar.CodigoEstado);
        Assert.Equal(404, eliminarOtraVez.CodigoEstado);
    }

    // ---------- RD-06: todas las operaciones exigen sesión, en el servidor ----------

    [Trait("Requisito", "RD-06")]
    [Theory]
    [InlineData(Operaciones.SubirDocumento)]
    [InlineData(Operaciones.ListarDocumentos)]
    [InlineData(Operaciones.VerDocumento)]
    [InlineData(Operaciones.DescargarDocumento)]
    [InlineData(Operaciones.EliminarDocumento)]
    public void OperacionDeDocumentos_SinSesion_SeRechaza(string operacion)
    {
        // Preparar
        var exigencia = PoliticasAcceso.Obtener(operacion);

        // Actuar
        var sinSesion = PoliticasAcceso.Permite(operacion, autenticado: false, rol: null, permisosOtorgados: null);
        var conSesion = PoliticasAcceso.Permite(operacion, autenticado: true, Rol.Estandar, permisosOtorgados: null);

        // Verificar
        Assert.True(exigencia.RequiereSesion);
        Assert.False(sinSesion);
        Assert.True(conSesion);
    }
}
