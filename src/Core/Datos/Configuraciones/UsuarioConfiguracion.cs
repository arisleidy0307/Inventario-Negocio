using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("Usuarios");
        b.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
        b.Property(u => u.Correo).HasMaxLength(254).IsRequired();
        b.HasIndex(u => u.Correo).IsUnique(); // RF-CA-01
        b.Property(u => u.ContrasenaHash).HasMaxLength(200).IsRequired();
        b.Property(u => u.ContrasenaSal).HasMaxLength(100).IsRequired();
        b.Property(u => u.Rol).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
