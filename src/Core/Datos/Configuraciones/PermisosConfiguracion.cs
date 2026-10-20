using Inventario.Core.Permisos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Core.Datos.Configuraciones;

public class SolicitudPermisoConfiguracion : IEntityTypeConfiguration<SolicitudPermiso>
{
    public void Configure(EntityTypeBuilder<SolicitudPermiso> b)
    {
        b.ToTable("SolicitudesPermiso");
        b.Property(s => s.Operacion).HasMaxLength(100).IsRequired();
        b.Property(s => s.Justificacion).HasMaxLength(SolicitudPermiso.LongitudMaximaTexto);
        b.Property(s => s.MotivoRechazo).HasMaxLength(SolicitudPermiso.LongitudMaximaTexto);
        b.Property(s => s.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.HasIndex(s => s.Estado);
        b.HasOne(s => s.Solicitante).WithMany().HasForeignKey(s => s.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Dominio.Usuario>().WithMany().HasForeignKey(s => s.AprobadaPorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Dominio.Usuario>().WithMany().HasForeignKey(s => s.RechazadaPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PermisoUsuarioConfiguracion : IEntityTypeConfiguration<PermisoUsuario>
{
    public void Configure(EntityTypeBuilder<PermisoUsuario> b)
    {
        b.ToTable("PermisosUsuario");
        b.Property(p => p.Operacion).HasMaxLength(100).IsRequired();
        b.HasIndex(p => new { p.UsuarioId, p.Operacion }).IsUnique();
        b.HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<SolicitudPermiso>().WithMany().HasForeignKey(p => p.SolicitudPermisoId).OnDelete(DeleteBehavior.Restrict);
    }
}
