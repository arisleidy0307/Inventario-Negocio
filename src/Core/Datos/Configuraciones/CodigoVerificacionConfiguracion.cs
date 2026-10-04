using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class CodigoVerificacionConfiguracion : IEntityTypeConfiguration<CodigoVerificacion>
{
    public void Configure(EntityTypeBuilder<CodigoVerificacion> b)
    {
        b.ToTable("CodigosVerificacion");
        b.Property(c => c.Proposito).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.CodigoHash).HasMaxLength(64).IsRequired();
        b.HasIndex(c => c.CodigoHash).IsUnique();
        b.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
