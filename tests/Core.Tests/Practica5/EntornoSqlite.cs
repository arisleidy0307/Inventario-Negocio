using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Inventario.Core.Notificaciones;
using Inventario.Core.Permisos;
using Inventario.Core.Reportes;
using Inventario.Core.Servicios;
using Inventario.Core.Tests.GestionPermisos;
using Inventario.Datos;
using Inventario.Negocio.Dominio;
using Inventario.Negocio.Reportes;
using Inventario.Negocio.Servicios;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Tests.Practica5;

/// <summary>
/// Los servicios REALES de la Práctica 5 (notificaciones, cola de correos, reportes) con sus repositorios EF Core
/// sobre una base SQLite en memoria. Cada prueba crea el suyo, así no comparten datos. Reloj fijo y movible.
/// </summary>
public sealed class EntornoSqlite : IDisposable
{
    public static readonly DateTime Inicio = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _conexion;

    public AppDbContext Db { get; }
    public RelojFijo Reloj { get; } = new(Inicio);
    public ServicioNotificaciones Notificaciones { get; }
    public ServicioPermisos Permisos { get; }
    public ServicioCatalogo Catalogo { get; }
    public ServicioOrdenesCompra Ordenes { get; }
    public ServicioConsultaCorreos Correos { get; }
    public ServicioReportesCore ReportesCore { get; }
    public ServicioReportesNegocio ReportesNegocio { get; }

    public EntornoSqlite()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        Db = NuevoContexto();
        Db.Database.EnsureCreated();

        var opciones = new OpcionesCore { AppBaseUrl = "http://localhost:5001" };
        Notificaciones = new ServicioNotificaciones(new RepositorioNotificacionesEf(Db, new ColaCorreos(Db, Reloj)), Reloj, opciones);
        Permisos = new ServicioPermisos(new RepositorioPermisosEf(Db), Reloj, Notificaciones);
        var repoNegocio = new RepositorioNegocioEf(Db);
        Catalogo = new ServicioCatalogo(repoNegocio);
        Ordenes = new ServicioOrdenesCompra(repoNegocio, Permisos, Reloj, Notificaciones);
        Correos = new ServicioConsultaCorreos(Db);
        ReportesCore = new ServicioReportesCore(Db);
        ReportesNegocio = new ServicioReportesNegocio(new RepositorioReportesNegocioEf(Db));
    }

    public AppDbContext NuevoContexto() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexion).Options);

    public async Task<Usuario> UsuarioAsync(string nombre, Rol rol, bool activo = true, bool deshabilitado = false)
    {
        var usuario = new Usuario
        {
            Nombre = nombre, Correo = $"{nombre.ToLowerInvariant()}@stockcore.test", ContrasenaHash = "x",
            ContrasenaSal = "x", Rol = rol, Activo = activo, Deshabilitado = deshabilitado, FechaCreacion = Reloj.UtcNow
        };
        Db.Usuarios.Add(usuario);
        await Db.SaveChangesAsync();
        return usuario;
    }

    /// <summary>Notificaciones de un usuario leídas directamente de la base.</summary>
    public async Task<List<Notificacion>> NotificacionesDeAsync(int usuarioId)
    {
        await using var otro = NuevoContexto();
        return await otro.Notificaciones.AsNoTracking().Where(n => n.DestinatarioId == usuarioId).ToListAsync();
    }

    public async Task<List<CorreoEnCola>> CorreosParaAsync(string correo)
    {
        await using var otro = NuevoContexto();
        return await otro.CorreosEnCola.AsNoTracking().Where(c => c.Destinatario == correo).ToListAsync();
    }

    public async Task<(Proveedor Proveedor, Producto Producto)> CatalogoAsync(string proveedor = "Distribuidora Caribe",
        string codigo = "P-001")
    {
        var prov = await Catalogo.CrearProveedorAsync(proveedor, null, null);
        var prod = await Catalogo.CrearProductoAsync(codigo, "Arroz 25 lb", 900m);
        return (prov, prod);
    }

    /// <summary>Crea una orden con un detalle, la emite y, si se pide, la recibe (como Administrador).</summary>
    public async Task<OrdenCompra> OrdenAsync(int creadorId, int adminId, int proveedorId, int productoId, int cantidad,
        decimal costo, bool recibir)
    {
        var orden = await Ordenes.CrearAsync(creadorId, proveedorId);
        await Ordenes.AgregarDetalleAsync(orden.Id, adminId, Rol.Administrador, productoId, cantidad, costo);
        await Ordenes.EmitirAsync(orden.Id, adminId);
        if (recibir)
            await Ordenes.RecibirAsync(orden.Id, adminId, Rol.Administrador);
        return orden;
    }

    public void Dispose()
    {
        Db.Dispose();
        _conexion.Dispose();
    }
}
