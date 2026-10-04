using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class SesionConfiguracion : IEntityTypeConfiguration<Sesion>
{
    public void Configure(EntityTypeBuilder<Sesion> b)
    {
        b.ToTable("Sesiones");
        b.Property(s => s.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(s => s.TokenHash).IsUnique();
        b.HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
