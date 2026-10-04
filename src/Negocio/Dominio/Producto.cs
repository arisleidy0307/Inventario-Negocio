namespace Inventario.Negocio.Dominio;

public class Producto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioCosto { get; set; }
    public int Stock { get; set; }
    public bool Activo { get; set; } = true;
}
