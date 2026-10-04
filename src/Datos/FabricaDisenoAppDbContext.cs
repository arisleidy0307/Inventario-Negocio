using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventario.Datos;

/// <summary>
/// Usada solo por las herramientas de EF (dotnet ef migrations add ...).
/// Lee la cadena de conexión de la variable de entorno; si no existe usa LocalDB.
/// </summary>
public class FabricaDisenoAppDbContext : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                     ?? "Server=(localdb)\\MSSQLLocalDB;Database=InventarioP3;Trusted_Connection=True;TrustServerCertificate=True";
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cadena).Options;
        return new AppDbContext(opciones);
    }
}
