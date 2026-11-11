using Inventario.Core.Comun;
using Inventario.Core.Dominio;

namespace Inventario.Core.Documentos;

/// <summary>
/// Manejador de documentos (Pieza 3 del Core, RF-DOC-01 a 05). Todas sus operaciones las invoca un
/// usuario con sesión válida (lo exige PoliticasAcceso); aquí se aplican las reglas de dueño y Administrador.
/// </summary>
public class ServicioDocumentos
{
    private readonly IRepositorioDocumentos _repo;
    private readonly IAlmacenArchivos _almacen;
    private readonly IReloj _reloj;
    private readonly long _tamanoMaximo;

    public ServicioDocumentos(IRepositorioDocumentos repo, IAlmacenArchivos almacen, IReloj reloj, long tamanoMaximoBytes)
    {
        _repo = repo;
        _almacen = almacen;
        _reloj = reloj;
        _tamanoMaximo = tamanoMaximoBytes;
    }

    /// <summary>RF-DOC-01: guarda el archivo y registra nombre, tipo, tamaño, fecha y quién lo subió.</summary>
    public async Task<Documento> SubirAsync(int actorId, string? nombre, string? tipoContenido, long tamano, Stream? contenido)
    {
        // RD-07: archivo ausente o vacío → rechazo controlado.
        if (contenido is null || string.IsNullOrWhiteSpace(nombre))
            throw ExcepcionControlada.Solicitud("Debes adjuntar un archivo en el campo «archivo».");
        if (tamano <= 0)
            throw ExcepcionControlada.Solicitud("El archivo está vacío.");
        if (tamano > _tamanoMaximo)
            throw ExcepcionControlada.Solicitud(
                $"El archivo supera el tamaño máximo permitido ({_tamanoMaximo / (1024 * 1024)} MB).");

        var nombreLimpio = Path.GetFileName(nombre.Trim());
        if (nombreLimpio.Length > Documento.LongitudMaximaNombre)
            throw ExcepcionControlada.Solicitud($"El nombre del archivo no puede pasar de {Documento.LongitudMaximaNombre} caracteres.");

        var tipo = string.IsNullOrWhiteSpace(tipoContenido) ? "application/octet-stream" : tipoContenido.Trim();
        if (tipo.Length > Documento.LongitudMaximaTipo)
            tipo = "application/octet-stream";

        var documento = new Documento
        {
            NombreOriginal = nombreLimpio,
            TipoContenido = tipo,
            TamanoBytes = tamano,
            FechaSubida = _reloj.UtcNow,
            SubidoPorUsuarioId = actorId,
            NombreAlmacenado = Guid.NewGuid().ToString("N")
        };

        await _almacen.GuardarAsync(documento.NombreAlmacenado, contenido);
        _repo.Agregar(documento);
        await _repo.GuardarAsync();
        return documento;
    }

    /// <summary>RF-DOC-02: sin eliminados; un Estándar solo ve los suyos y el Administrador ve todos.</summary>
    public Task<IReadOnlyList<Documento>> ListarAsync(int actorId, Rol rolActor) =>
        _repo.ListarVigentesAsync(rolActor == Rol.Administrador ? null : actorId);

    /// <summary>Datos de un documento: solo el dueño o un Administrador.</summary>
    public Task<Documento> ObtenerAsync(int id, int actorId, Rol rolActor) =>
        BuscarPermitidoAsync(id, actorId, rolActor, "ver");

    /// <summary>RF-DOC-03: solo el dueño o un Administrador lo descargan; a cualquier otro se le rechaza.</summary>
    public async Task<(Documento Documento, Stream Contenido)> DescargarAsync(int id, int actorId, Rol rolActor)
    {
        var documento = await BuscarPermitidoAsync(id, actorId, rolActor, "descargar");
        var contenido = _almacen.AbrirLectura(documento.NombreAlmacenado)
                        ?? throw ExcepcionControlada.NoEncontrado("El archivo de este documento ya no está en el almacén.");
        return (documento, contenido);
    }

    /// <summary>RF-DOC-04: eliminación lógica. Deja de listarse, pero el registro y el archivo se conservan.</summary>
    public async Task<Documento> EliminarAsync(int id, int actorId, Rol rolActor)
    {
        var documento = await BuscarPermitidoAsync(id, actorId, rolActor, "eliminar");
        documento.MarcarEliminado(actorId, _reloj.UtcNow);
        await _repo.GuardarAsync();
        return documento;
    }

    private async Task<Documento> BuscarPermitidoAsync(int id, int actorId, Rol rolActor, string accion)
    {
        var documento = await _repo.ObtenerAsync(id);
        if (documento is null || documento.Eliminado)
            throw ExcepcionControlada.NoEncontrado("No existe un documento con ese identificador.");
        if (rolActor != Rol.Administrador && documento.SubidoPorUsuarioId != actorId)
            throw ExcepcionControlada.Prohibido($"Solo quien subió el documento o un Administrador puede {accion}lo.");
        return documento;
    }
}
