using Inventario.Core.Comun;
using Inventario.Core.Dominio;
using Inventario.Core.Notificaciones;
using Inventario.Core.Seguridad;

namespace Inventario.Core.Permisos;

/// <summary>Gestión de permisos: solicitud, consulta y resolución (RF-GP-01 a 07).</summary>
public class ServicioPermisos
{
    private readonly IRepositorioPermisos _repositorio;
    private readonly IReloj _reloj;
    private readonly INotificador? _notificador;

    /// <param name="notificador">Motor de notificaciones (RF-NOT-01, 02). Opcional: sin él no se notifica.</param>
    public ServicioPermisos(IRepositorioPermisos repositorio, IReloj reloj, INotificador? notificador = null)
    {
        _repositorio = repositorio;
        _reloj = reloj;
        _notificador = notificador;
    }

    /// <summary>RF-GP-01: la solicitud nace en estado Pendiente.</summary>
    public async Task<SolicitudPermiso> CrearSolicitudAsync(int solicitanteId, string? operacion, string? justificacion)
    {
        var op = (operacion ?? string.Empty).Trim();
        if (!PoliticasAcceso.EsOtorgable(op))
            throw ExcepcionControlada.Solicitud(
                "La operación no existe o no se puede solicitar. Operaciones disponibles: " +
                string.Join(", ", PoliticasAcceso.OperacionesOtorgables) + ".");

        var texto = string.IsNullOrWhiteSpace(justificacion) ? null : justificacion.Trim();
        if (texto?.Length > SolicitudPermiso.LongitudMaximaTexto)
            throw ExcepcionControlada.Solicitud(
                $"La justificación admite hasta {SolicitudPermiso.LongitudMaximaTexto} caracteres.");

        if (await _repositorio.TienePermisoAsync(solicitanteId, op))
            throw ExcepcionControlada.Conflicto("Ya tienes ese permiso.");
        if (await _repositorio.ExisteSolicitudPendienteAsync(solicitanteId, op))
            throw ExcepcionControlada.Conflicto("Ya tienes una solicitud pendiente para esa operación.");

        var solicitud = new SolicitudPermiso
        {
            SolicitanteId = solicitanteId,
            Operacion = op,
            Justificacion = texto,
            FechaSolicitud = _reloj.UtcNow
        };
        _repositorio.AgregarSolicitud(solicitud);
        await _repositorio.GuardarAsync();

        // RF-NOT-01: avisa a cada Administrador activo, y a nadie más.
        if (_notificador is not null)
        {
            var solicitante = (await _repositorio.ObtenerSolicitudAsync(solicitud.Id))?.Solicitante;
            var quien = solicitante is null ? $"El usuario #{solicitanteId}" : $"{solicitante.Nombre} ({solicitante.Correo})";
            await _notificador.NotificarAdministradoresAsync(TipoNotificacion.SolicitudCreada,
                $"Nueva solicitud de permiso #{solicitud.Id}",
                $"{quien} pidió el permiso {op}." +
                (texto is null ? string.Empty : $" Justificación: {texto.TrimEnd('.')}.") +
                " Revísala en GET /api/solicitudes-permiso/pendientes.");
        }
        return solicitud;
    }

    /// <summary>RF-GP-02: solo el Administrador las consulta (PoliticasAcceso).</summary>
    public Task<IReadOnlyList<SolicitudPermiso>> PendientesAsync() => _repositorio.SolicitudesPendientesAsync();

    public Task<IReadOnlyList<SolicitudPermiso>> DeUsuarioAsync(int usuarioId) =>
        _repositorio.SolicitudesDeUsuarioAsync(usuarioId);

    public Task<IReadOnlyList<string>> PermisosDeUsuarioAsync(int usuarioId) =>
        _repositorio.PermisosDeUsuarioAsync(usuarioId);

    /// <summary>RF-GP-02: un Estándar no puede ver las solicitudes de otros usuarios.</summary>
    public async Task<SolicitudPermiso> ObtenerAsync(int solicitudId, int actorId, Rol rolActor)
    {
        var solicitud = await BuscarAsync(solicitudId);
        if (rolActor != Rol.Administrador && solicitud.SolicitanteId != actorId)
            throw ExcepcionControlada.Prohibido("No puedes ver solicitudes de otros usuarios.");
        return solicitud;
    }

    /// <summary>
    /// RF-GP-03 + RF-GP-05: aprueba (registra quién y cuándo) y el sistema aplica el permiso en la misma
    /// operación, sin intervención manual. Termina en Aplicada.
    /// </summary>
    public async Task<SolicitudPermiso> AprobarAsync(int solicitudId, int administradorId)
    {
        var solicitud = await BuscarAsync(solicitudId);
        ImpedirResolverPropia(solicitud, administradorId);   // RF-GP-07

        var ahora = _reloj.UtcNow;
        solicitud.Aprobar(administradorId, ahora);           // RF-GP-06: Pendiente → Aprobada o rechazo

        if (!await _repositorio.TienePermisoAsync(solicitud.SolicitanteId, solicitud.Operacion))
        {
            _repositorio.AgregarPermiso(new PermisoUsuario
            {
                UsuarioId = solicitud.SolicitanteId,
                Operacion = solicitud.Operacion,
                FechaOtorgado = ahora,
                SolicitudPermisoId = solicitud.Id
            });
        }
        solicitud.Aplicar(ahora);                            // Aprobada → Aplicada

        await _repositorio.GuardarAsync();

        // RF-NOT-02: avisa al solicitante que fue aprobada.
        if (_notificador is not null)
            await _notificador.NotificarAsync(solicitud.SolicitanteId, TipoNotificacion.SolicitudAprobada,
                $"Tu solicitud de permiso #{solicitud.Id} fue aprobada",
                $"Tu solicitud del permiso {solicitud.Operacion} fue aprobada. Ya puedes usar esa operación.");
        return solicitud;
    }

    /// <summary>RF-GP-04: el rechazo exige motivo escrito.</summary>
    public async Task<SolicitudPermiso> RechazarAsync(int solicitudId, int administradorId, string? motivo)
    {
        var solicitud = await BuscarAsync(solicitudId);
        ImpedirResolverPropia(solicitud, administradorId);   // RF-GP-07

        solicitud.Rechazar(administradorId, motivo, _reloj.UtcNow);
        await _repositorio.GuardarAsync();

        // RF-NOT-02: avisa al solicitante que fue rechazada, con el motivo.
        if (_notificador is not null)
            await _notificador.NotificarAsync(solicitud.SolicitanteId, TipoNotificacion.SolicitudRechazada,
                $"Tu solicitud de permiso #{solicitud.Id} fue rechazada",
                $"Tu solicitud del permiso {solicitud.Operacion} fue rechazada. Motivo: {solicitud.MotivoRechazo}");
        return solicitud;
    }

    private static void ImpedirResolverPropia(SolicitudPermiso solicitud, int administradorId)
    {
        if (solicitud.SolicitanteId == administradorId)
            throw ExcepcionControlada.Prohibido("No puedes aprobar ni rechazar tu propia solicitud.");
    }

    private async Task<SolicitudPermiso> BuscarAsync(int id) =>
        await _repositorio.ObtenerSolicitudAsync(id)
        ?? throw ExcepcionControlada.NoEncontrado("No existe una solicitud con ese identificador.");
}
