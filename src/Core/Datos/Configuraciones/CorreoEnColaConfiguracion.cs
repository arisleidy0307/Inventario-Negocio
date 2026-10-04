using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class CorreoEnColaConfiguracion : IEntityTypeConfiguration<CorreoEnCola>
{
    public void Configure(EntityTypeBuilder<CorreoEnCola> b)
    {
        b.ToTable("CorreosEnCola");
        b.Property(c => c.Destinatario).HasMaxLength(254).IsRequired();
        b.Property(c => c.Asunto).HasMaxLength(200).IsRequired();
        b.Property(c => c.Cuerpo).IsRequired();
        b.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.UltimoError).HasMaxLength(1000);
        b.HasIndex(c => c.Estado);
    }
}
