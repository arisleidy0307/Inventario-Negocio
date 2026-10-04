using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Datos;

/// <summary>
/// Contexto con las entidades del Core. No conoce el módulo de negocio (RD-03):
/// el proyecto Datos lo extiende con las entidades de Negocio.
/// </summary>
public abstract class CoreDbContext : DbContext
{
    protected CoreDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);
    }
}
