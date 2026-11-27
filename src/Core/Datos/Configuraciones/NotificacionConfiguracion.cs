using Inventario.Core.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class NotificacionConfiguracion : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable("Notificaciones");
        b.Property(n => n.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(n => n.Titulo).HasMaxLength(Notificacion.LongitudMaximaTitulo).IsRequired();
        b.Property(n => n.Mensaje).HasMaxLength(Notificacion.LongitudMaximaMensaje).IsRequired();
        b.HasIndex(n => new { n.DestinatarioId, n.Leida });
        b.HasOne<Dominio.Usuario>().WithMany().HasForeignKey(n => n.DestinatarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
