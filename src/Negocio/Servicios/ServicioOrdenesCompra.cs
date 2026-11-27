using Inventario.Core.Comun;
using System.Globalization;
using Inventario.Core.Dominio;
using Inventario.Core.Notificaciones;
using Inventario.Core.Permisos;
using Inventario.Core.Seguridad;
using Inventario.Negocio.Dominio;

namespace Inventario.Negocio.Servicios;

/// <summary>
/// Órdenes de compra: creación, detalles y su ciclo de vida (Emitir, Recibir, Cancelar).
/// Las transiciones las resuelve TransicionesOrdenCompra a través de la entidad (RD-04).
/// </summary>
public class ServicioOrdenesCompra
{
    private readonly IRepositorioNegocio _repo;
    private readonly ServicioPermisos _permisos;
    private readonly IReloj _reloj;
    private readonly INotificador? _notificador;

    /// <param name="notificador">Motor de notificaciones (RF-NOT-03). Opcional: sin él no se notifica.</param>
    public ServicioOrdenesCompra(IRepositorioNegocio repo, ServicioPermisos permisos, IReloj reloj,
        INotificador? notificador = null)
    {
        _repo = repo;
        _permisos = permisos;
        _reloj = reloj;
        _notificador = notificador;
    }

    /// <summary>Crea una orden en Borrador para un proveedor existente.</summary>
    public async Task<OrdenCompra> CrearAsync(int actorId, int proveedorId)
    {
        var proveedor = await _repo.ObtenerProveedorAsync(proveedorId)
                        ?? throw ExcepcionControlada.Solicitud("No existe un proveedor con ese identificador.");

        var orden = new OrdenCompra
        {
            ProveedorId = proveedor.Id,
            Proveedor = proveedor,
            CreadaPorUsuarioId = actorId,
            FechaCreacion = _reloj.UtcNow
        };
        _repo.AgregarOrden(orden);
        await _repo.GuardarAsync();
        return orden;
    }

    /// <summary>Agrega un producto a una orden en Borrador. Solo quien la creó o un Administrador.</summary>
    public async Task<OrdenCompra> AgregarDetalleAsync(int ordenId, int actorId, Rol rolActor,
        int productoId, int cantidad, decimal costoUnitario)
    {
        var orden = await BuscarAsync(ordenId);
        if (rolActor != Rol.Administrador && orden.CreadaPorUsuarioId != actorId)
            throw ExcepcionControlada.Prohibido("Solo quien creó la orden o un Administrador puede modificarla.");

        var producto = await _repo.ObtenerProductoAsync(productoId);
        if (producto is null || !producto.Activo)
            throw ExcepcionControlada.Solicitud("No existe un producto activo con ese identificador.");

        orden.AgregarDetalle(producto.Id, cantidad, costoUnitario);
        await _repo.GuardarAsync();
        return orden;
    }

    /// <summary>Un Estándar solo ve sus propias órdenes; el Administrador ve todas.</summary>
    public async Task<OrdenCompra> ObtenerAsync(int ordenId, int actorId, Rol rolActor)
    {
        var orden = await BuscarAsync(ordenId);
        if (rolActor != Rol.Administrador && orden.CreadaPorUsuarioId != actorId)
            throw ExcepcionControlada.Prohibido("No puedes ver órdenes de otros usuarios.");
        return orden;
    }

    public Task<IReadOnlyList<OrdenCompra>> ListarAsync(int actorId, Rol rolActor) =>
        _repo.OrdenesAsync(rolActor == Rol.Administrador ? null : actorId);

    /// <summary>Borrador → Emitida (Administrador, exigido en PoliticasAcceso).</summary>
    public async Task<OrdenCompra> EmitirAsync(int ordenId, int? actorId = null)
    {
        var orden = await BuscarAsync(ordenId);
        orden.Emitir(_reloj.UtcNow);
        await _repo.GuardarAsync();

        // RF-NOT-03 (evento del negocio): avisa a quien creó la orden.
        await NotificarCreadorAsync(orden, actorId, TipoNotificacion.OrdenEmitida,
            $"Tu orden de compra #{orden.Id} fue emitida",
            $"La orden de compra #{orden.Id} a {NombreProveedor(orden)} fue emitida por un Administrador. " +
            $"Total: {Monto(orden.Total)} DOP.");
        return orden;
    }

    /// <summary>
    /// Emitida → Recibida. La puede ejecutar un Administrador o un usuario con el permiso
    /// Ordenes.Recibir aprobado en Gestión de permisos. Suma el stock y registra los movimientos de entrada.
    /// </summary>
    public async Task<OrdenCompra> RecibirAsync(int ordenId, int actorId, Rol rolActor)
    {
        var permisos = await _permisos.PermisosDeUsuarioAsync(actorId);
        if (!PoliticasAcceso.Permite(Operaciones.RecibirOrden, autenticado: true, rolActor, permisos))
            throw ExcepcionControlada.Prohibido("Necesitas el permiso Ordenes.Recibir aprobado por un Administrador.");

        var orden = await BuscarAsync(ordenId);
        var ahora = _reloj.UtcNow;
        orden.Recibir(actorId, ahora);

        foreach (var detalle in orden.Detalles)
        {
            var producto = await _repo.ObtenerProductoAsync(detalle.ProductoId)
                           ?? throw ExcepcionControlada.Conflicto("Un producto de la orden ya no existe.");
            producto.Stock += detalle.Cantidad;
            _repo.AgregarMovimiento(new MovimientoStock
            {
                ProductoId = producto.Id,
                Tipo = TipoMovimiento.Entrada,
                Cantidad = detalle.Cantidad,
                Fecha = ahora,
                OrdenCompraId = orden.Id
            });
        }

        await _repo.GuardarAsync();

        // RF-NOT-03 (evento del negocio): avisa a quien creó la orden que la mercancía llegó.
        await NotificarCreadorAsync(orden, actorId, TipoNotificacion.OrdenRecibida,
            $"Tu orden de compra #{orden.Id} fue recibida",
            $"La orden de compra #{orden.Id} a {NombreProveedor(orden)} fue recibida y el stock ya se actualizó " +
            $"({orden.Detalles.Sum(d => d.Cantidad)} unidades).");
        return orden;
    }

    /// <summary>Borrador → Cancelada, o Emitida → Cancelada con motivo (Administrador).</summary>
    public async Task<OrdenCompra> CancelarAsync(int ordenId, string? motivo)
    {
        var orden = await BuscarAsync(ordenId);
        orden.Cancelar(motivo, _reloj.UtcNow);
        await _repo.GuardarAsync();
        return orden;
    }

    /// <summary>No se le notifica a nadie su propia acción.</summary>
    private async Task NotificarCreadorAsync(OrdenCompra orden, int? actorId, TipoNotificacion tipo, string titulo, string mensaje)
    {
        if (_notificador is null || orden.CreadaPorUsuarioId == actorId)
            return;
        await _notificador.NotificarAsync(orden.CreadaPorUsuarioId, tipo, titulo, mensaje);
    }

    private static string NombreProveedor(OrdenCompra orden) => orden.Proveedor?.Nombre ?? $"el proveedor #{orden.ProveedorId}";

    private static string Monto(decimal valor) => valor.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private async Task<OrdenCompra> BuscarAsync(int id) =>
        await _repo.ObtenerOrdenAsync(id)
        ?? throw ExcepcionControlada.NoEncontrado("No existe una orden de compra con ese identificador.");
}
