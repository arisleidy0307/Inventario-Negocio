using Inventario.Negocio.Estados;

namespace Inventario.Negocio.Dominio;

/// <summary>Entidad central del módulo de negocio. Su ciclo de vida lo rige TransicionesOrdenCompra.</summary>
public class OrdenCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Borrador;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEmision { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string? MotivoCancelacion { get; set; }
    public int CreadaPorUsuarioId { get; set; }
    public List<DetalleOrdenCompra> Detalles { get; set; } = new();
}
