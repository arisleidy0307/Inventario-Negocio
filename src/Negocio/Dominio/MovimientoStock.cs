namespace Inventario.Negocio.Dominio;

public enum TipoMovimiento
{
    Entrada = 1,
    Salida = 2
}

/// <summary>Movimiento de inventario. Recibir una orden genera movimientos de entrada.</summary>
public class MovimientoStock
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public Producto? Producto { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public DateTime Fecha { get; set; }
    public int? OrdenCompraId { get; set; }
    public OrdenCompra? OrdenCompra { get; set; }
}
