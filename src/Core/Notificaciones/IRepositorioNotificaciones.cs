using Inventario.Core.Dominio;

namespace Inventario.Core.Notificaciones;

public interface IRepositorioNotificaciones
{
    Task<Usuario?> UsuarioAsync(int id);

    /// <summary>Administradores con la cuenta activada y no desactivada.</summary>
    Task<IReadOnlyList<Usuario>> AdministradoresActivosAsync();

    Task<Notificacion?> ObtenerAsync(int id);

    /// <summary>Notificaciones de un usuario, de la más nueva a la más vieja.</summary>
    Task<IReadOnlyList<Notificacion>> DeUsuarioAsync(int usuarioId, bool soloNoLeidas);

    Task<int> NoLeidasAsync(int usuarioId);

    void Agregar(Notificacion notificacion);

    /// <summary>Encola el correo del canal externo (RF-NOT-08: nunca se envía aquí).</summary>
    void EncolarCorreo(string destinatario, string asunto, string cuerpo);

    Task GuardarAsync();
}
