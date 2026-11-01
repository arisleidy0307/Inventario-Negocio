using System.ComponentModel.DataAnnotations;
using Inventario.Negocio.Dominio;

namespace Inventario.Api.Contratos;

public record CrearProductoRequest(
    [Required(ErrorMessage = "El código es obligatorio.")] [StringLength(30)] string Codigo,
    [Required(ErrorMessage = "El nombre es obligatorio.")] [StringLength(150)] string Nombre,
    [Range(0, 999999999, ErrorMessage = "El precio de costo no puede ser negativo.")] decimal PrecioCosto);

public record CrearProveedorRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")] [StringLength(150)] string Nombre,
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")] [StringLength(254)] string? Correo,
    [StringLength(30)] string? Telefono);

public record CrearOrdenRequest(
    [Range(1, int.MaxValue, ErrorMessage = "El proveedor es obligatorio.")] int ProveedorId);

public record AgregarDetalleRequest(
    [Range(1, int.MaxValue, ErrorMessage = "El producto es obligatorio.")] int ProductoId,
    [Range(1, 1000000, ErrorMessage = "La cantidad debe ser mayor que cero.")] int Cantidad,
    [Range(0, 999999999, ErrorMessage = "El costo unitario no puede ser negativo.")] decimal CostoUnitario);

public record CancelarOrdenRequest([StringLength(OrdenCompra.LongitudMaximaMotivo)] string? Motivo);

public record ProductoResponse(int Id, string Codigo, string Nombre, decimal PrecioCosto, int Stock, bool Activo)
{
    public static ProductoResponse Desde(Producto p) => new(p.Id, p.Codigo, p.Nombre, p.PrecioCosto, p.Stock, p.Activo);
}

public record ProveedorResponse(int Id, string Nombre, string? Correo, string? Telefono)
{
    public static ProveedorResponse Desde(Proveedor p) => new(p.Id, p.Nombre, p.Correo, p.Telefono);
}

public record MovimientoResponse(int Id, int ProductoId, string Tipo, int Cantidad, DateTime Fecha, int? OrdenCompraId)
{
    public static MovimientoResponse Desde(MovimientoStock m) =>
        new(m.Id, m.ProductoId, m.Tipo.ToString(), m.Cantidad, m.Fecha, m.OrdenCompraId);
}

public record DetalleResponse(int ProductoId, string? Producto, int Cantidad, decimal CostoUnitario);

public record OrdenCompraResponse(
    int Id, int ProveedorId, string? Proveedor, string Estado, int CreadaPorUsuarioId, DateTime FechaCreacion,
    DateTime? FechaEmision, DateTime? FechaRecepcion, int? RecibidaPorUsuarioId, DateTime? FechaCancelacion,
    string? MotivoCancelacion, decimal Total, IReadOnlyList<DetalleResponse> Detalles)
{
    public static OrdenCompraResponse Desde(OrdenCompra o) => new(
        o.Id, o.ProveedorId, o.Proveedor?.Nombre, o.Estado.ToString(), o.CreadaPorUsuarioId, o.FechaCreacion,
        o.FechaEmision, o.FechaRecepcion, o.RecibidaPorUsuarioId, o.FechaCancelacion, o.MotivoCancelacion, o.Total,
        o.Detalles.Select(d => new DetalleResponse(d.ProductoId, d.Producto?.Nombre, d.Cantidad, d.CostoUnitario)).ToList());
}
