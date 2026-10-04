using Inventario.Negocio.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Datos.Configuraciones;

public class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos");
        b.Property(p => p.Codigo).HasMaxLength(30).IsRequired();
        b.HasIndex(p => p.Codigo).IsUnique();
        b.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        b.Property(p => p.PrecioCosto).HasPrecision(18, 2);
    }
}

public class ProveedorConfiguracion : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b)
    {
        b.ToTable("Proveedores");
        b.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        b.Property(p => p.Correo).HasMaxLength(254);
        b.Property(p => p.Telefono).HasMaxLength(30);
    }
}

public class OrdenCompraConfiguracion : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> b)
    {
        b.ToTable("OrdenesCompra");
        b.Property(o => o.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(o => o.MotivoCancelacion).HasMaxLength(500);
        b.HasOne(o => o.Proveedor).WithMany().HasForeignKey(o => o.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(o => o.Detalles).WithOne(d => d.OrdenCompra!).HasForeignKey(d => d.OrdenCompraId);
    }
}

public class DetalleOrdenCompraConfiguracion : IEntityTypeConfiguration<DetalleOrdenCompra>
{
    public void Configure(EntityTypeBuilder<DetalleOrdenCompra> b)
    {
        b.ToTable("DetallesOrdenCompra");
        b.Property(d => d.CostoUnitario).HasPrecision(18, 2);
        b.HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MovimientoStockConfiguracion : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> b)
    {
        b.ToTable("MovimientosStock");
        b.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        b.HasOne(m => m.Producto).WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.OrdenCompra).WithMany().HasForeignKey(m => m.OrdenCompraId).OnDelete(DeleteBehavior.Restrict);
    }
}
