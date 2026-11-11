using System.Text;
using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Documentos;
using Inventario.Core.Dominio;
using Inventario.Datos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Inventario.Core.Tests.Documentos;

/// <summary>
/// El Manejador de documentos con sus piezas reales: archivos en una carpeta del disco y registros con
/// RepositorioDocumentosEf sobre SQLite en memoria. Comprueba que la eliminación es lógica en la base.
/// </summary>
public sealed class AlmacenYRepositorioDocumentosTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "stockcore-pruebas-" + Guid.NewGuid().ToString("N"));
    private readonly SqliteConnection _conexion;
    private readonly AppDbContext _db;
    private readonly ServicioDocumentos _servicio;
    private readonly Usuario _dueno;

    public AlmacenYRepositorioDocumentosTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexion).Options);
        _db.Database.EnsureCreated();
        _dueno = new Usuario
        {
            Nombre = "Almacén", Correo = "almacen@stockcore.test", ContrasenaHash = "x", ContrasenaSal = "x",
            Rol = Rol.Estandar, Activo = true, FechaCreacion = DateTime.UtcNow
        };
        _db.Usuarios.Add(_dueno);
        _db.SaveChanges();
        _servicio = new ServicioDocumentos(new RepositorioDocumentosEf(_db), new AlmacenArchivosEnDisco(_carpeta),
            new RelojSistema(), 1024 * 1024);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }

    [Trait("Requisito", "RF-DOC-05")]
    [Fact]
    public async Task Subir_GuardaElArchivoEnLaCarpetaDelAlmacenYSeDescargaIgual()
    {
        // Preparar
        var bytes = Encoding.UTF8.GetBytes("Orden de compra 15 - Distribuidora Caribe");

        // Actuar
        var documento = await _servicio.SubirAsync(_dueno.Id, "orden-15.txt", "text/plain", bytes.Length, new MemoryStream(bytes));
        var (_, contenido) = await _servicio.DescargarAsync(documento.Id, _dueno.Id, Rol.Estandar);

        // Verificar
        Assert.True(File.Exists(Path.Combine(_carpeta, documento.NombreAlmacenado)));
        using var lector = new StreamReader(contenido);
        Assert.Equal("Orden de compra 15 - Distribuidora Caribe", await lector.ReadToEndAsync());
    }

    [Trait("Requisito", "RF-DOC-04")]
    [Fact]
    public async Task Eliminar_ElRegistroSigueEnLaBaseMarcadoComoEliminadoYElArchivoSigueEnDisco()
    {
        // Preparar
        var bytes = Encoding.UTF8.GetBytes("factura");
        var documento = await _servicio.SubirAsync(_dueno.Id, "factura.txt", "text/plain", bytes.Length, new MemoryStream(bytes));

        // Actuar
        await _servicio.EliminarAsync(documento.Id, _dueno.Id, Rol.Estandar);

        // Verificar
        _db.ChangeTracker.Clear();
        var registro = await _db.Documentos.SingleAsync(d => d.Id == documento.Id);
        Assert.True(registro.Eliminado);
        Assert.NotNull(registro.FechaEliminacion);
        Assert.Empty(await _servicio.ListarAsync(_dueno.Id, Rol.Estandar));
        Assert.True(File.Exists(Path.Combine(_carpeta, registro.NombreAlmacenado)));
    }
}
