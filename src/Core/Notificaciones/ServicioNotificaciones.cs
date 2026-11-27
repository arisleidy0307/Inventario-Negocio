using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Servicios;

namespace Inventario.Core.Notificaciones;

/// <summary>Bandeja de un usuario: sus notificaciones y cuántas no ha leído (RF-NOT-04).</summary>
public record BandejaNotificaciones(int NoLeidas, IReadOnlyList<Notificacion> Notificaciones);

/// <summary>
/// Motor de notificaciones (pieza 4). Cada notificación se entrega por dos canales (RF-NOT-06):
/// la bandeja interna y un correo que solo se ENCOLA; lo envía el EnviadorCorreos (RF-NOT-08).
/// El destinatario puede apagar el correo sin perder la bandeja (RF-NOT-07).
/// </summary>
public class ServicioNotificaciones : INotificador
{
    private readonly IRepositorioNotificaciones _repo;
    private readonly IReloj _reloj;
    private readonly OpcionesCore _opciones;

    public ServicioNotificaciones(IRepositorioNotificaciones repo, IReloj reloj, OpcionesCore opciones)
    {
        _repo = repo;
        _reloj = reloj;
        _opciones = opciones;
    }

    public async Task NotificarAsync(int destinatarioId, TipoNotificacion tipo, string titulo, string mensaje)
    {
        var destinatario = await _repo.UsuarioAsync(destinatarioId);
        if (destinatario is null)
            return;
        Entregar(destinatario, tipo, titulo, mensaje);
        await _repo.GuardarAsync();
    }

    public async Task NotificarAdministradoresAsync(TipoNotificacion tipo, string titulo, string mensaje)
    {
        foreach (var administrador in await _repo.AdministradoresActivosAsync())
            Entregar(administrador, tipo, titulo, mensaje);
        await _repo.GuardarAsync();
    }

    /// <summary>RF-NOT-04: solo las del propio usuario.</summary>
    public async Task<BandejaNotificaciones> BandejaAsync(int usuarioId, bool soloNoLeidas) =>
        new(await _repo.NoLeidasAsync(usuarioId), await _repo.DeUsuarioAsync(usuarioId, soloNoLeidas));

    /// <summary>RF-NOT-05: solo el destinatario puede marcarla; a cualquier otro se le rechaza.</summary>
    public async Task<Notificacion> MarcarLeidaAsync(int notificacionId, int actorId)
    {
        var notificacion = await _repo.ObtenerAsync(notificacionId)
                           ?? throw ExcepcionControlada.NoEncontrado("No existe una notificación con ese identificador.");
        if (notificacion.DestinatarioId != actorId)
            throw ExcepcionControlada.Prohibido("Solo puedes marcar tus propias notificaciones.");

        notificacion.MarcarLeida(_reloj.UtcNow);
        await _repo.GuardarAsync();
        return notificacion;
    }

    /// <summary>RF-NOT-07: si el usuario recibe las notificaciones también por correo.</summary>
    public async Task<bool> RecibeCorreoAsync(int usuarioId) =>
        (await BuscarUsuarioAsync(usuarioId)).NotificacionesPorCorreo;

    public async Task<bool> CambiarCorreoAsync(int usuarioId, bool recibirCorreo)
    {
        var usuario = await BuscarUsuarioAsync(usuarioId);
        usuario.NotificacionesPorCorreo = recibirCorreo;
        await _repo.GuardarAsync();
        return usuario.NotificacionesPorCorreo;
    }

    private void Entregar(Usuario destinatario, TipoNotificacion tipo, string titulo, string mensaje)
    {
        titulo = Recortar(titulo, Notificacion.LongitudMaximaTitulo);
        mensaje = Recortar(mensaje, Notificacion.LongitudMaximaMensaje);

        // Canal interno: siempre.
        _repo.Agregar(new Notificacion
        {
            DestinatarioId = destinatario.Id,
            Tipo = tipo,
            Titulo = titulo,
            Mensaje = mensaje,
            FechaCreacion = _reloj.UtcNow
        });

        // Canal de correo: solo si el destinatario no lo desactivó.
        if (destinatario.NotificacionesPorCorreo)
        {
            var cuerpo = $"Hola {destinatario.Nombre}:\n\n{mensaje}\n\n" +
                         $"Puedes ver todas tus notificaciones en {_opciones.AppBaseUrl.TrimEnd('/')}/api/notificaciones.\n" +
                         "Si no quieres recibirlas por correo, desactívalo en PUT /api/notificaciones/preferencias.";
            _repo.EncolarCorreo(destinatario.Correo, titulo, cuerpo);
        }
    }

    private async Task<Usuario> BuscarUsuarioAsync(int usuarioId) =>
        await _repo.UsuarioAsync(usuarioId) ?? throw ExcepcionControlada.NoEncontrado("No existe el usuario.");

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
