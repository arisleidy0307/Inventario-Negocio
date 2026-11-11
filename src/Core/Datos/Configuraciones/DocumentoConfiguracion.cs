using Inventario.Core.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class DocumentoConfiguracion : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> b)
    {
        b.ToTable("Documentos");
        b.Property(d => d.NombreOriginal).HasMaxLength(Documento.LongitudMaximaNombre).IsRequired();
        b.Property(d => d.TipoContenido).HasMaxLength(Documento.LongitudMaximaTipo).IsRequired();
        b.Property(d => d.NombreAlmacenado).HasMaxLength(64).IsRequired();
        b.HasIndex(d => d.NombreAlmacenado).IsUnique();
        b.HasIndex(d => new { d.SubidoPorUsuarioId, d.Eliminado });
        b.HasOne<Dominio.Usuario>().WithMany().HasForeignKey(d => d.SubidoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Dominio.Usuario>().WithMany().HasForeignKey(d => d.EliminadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
