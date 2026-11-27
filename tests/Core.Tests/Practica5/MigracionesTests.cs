using Inventario.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Inventario.Core.Tests.Practica5;

/// <summary>
/// Las pruebas usan SQLite con EnsureCreated, que crea las tablas desde el modelo y no desde las migraciones.
/// Esta prueba evita que un cambio de modelo llegue a SQL Server sin su migración (RD-09).
/// </summary>
public class MigracionesTests
{
    [Trait("Requisito", "RD-09")]
    [Fact]
    public void Migraciones_EstanAlDiaConElModelo()
    {
        // Preparar: el modelo actual y el que dejan las migraciones (no se conecta a ninguna base)
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=SoloParaComparar;TrustServerCertificate=True").Options);
        var instantanea = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var modeloDeMigraciones = db.GetService<IModelRuntimeInitializer>().Initialize((IModel)instantanea, designTime: true);
        var modeloActual = db.GetService<IDesignTimeModel>().Model;

        // Actuar
        var hayDiferencias = db.GetService<IMigrationsModelDiffer>()
            .HasDifferences(modeloDeMigraciones.GetRelationalModel(), modeloActual.GetRelationalModel());

        // Verificar
        Assert.False(hayDiferencias, "Hay cambios en el modelo sin migración: agrega una con dotnet ef migrations add.");
    }
}
