using Inventario.Core.Datos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Datos;

/// <summary>Contexto de la aplicación: entidades del Core + entidades de Negocio.</summary>
public class AppDbContext : CoreDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
