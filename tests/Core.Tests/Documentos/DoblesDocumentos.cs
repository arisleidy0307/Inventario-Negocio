using Inventario.Core.Documentos;

namespace Inventario.Core.Tests.Documentos;

/// <summary>Repositorio de documentos en memoria (RD-12). Guarda también los eliminados, como la base real.</summary>
public sealed class RepositorioDocumentosEnMemoria : IRepositorioDocumentos
{
    private int _siguienteId = 1;
    public List<Documento> Todos { get; } = new();

    public Task<Documento?> ObtenerAsync(int id) => Task.FromResult(Todos.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Documento>> ListarVigentesAsync(int? usuarioId) =>
        Task.FromResult<IReadOnlyList<Documento>>(Todos
            .Where(d => !d.Eliminado && (usuarioId == null || d.SubidoPorUsuarioId == usuarioId)).ToList());

    public void Agregar(Documento documento)
    {
        documento.Id = _siguienteId++;
        Todos.Add(documento);
    }

    public Task GuardarAsync() => Task.CompletedTask;
}

/// <summary>Almacén de archivos en memoria: guarda los bytes por nombre almacenado.</summary>
public sealed class AlmacenArchivosEnMemoria : IAlmacenArchivos
{
    public Dictionary<string, byte[]> Archivos { get; } = new();

    public async Task GuardarAsync(string nombreAlmacenado, Stream contenido)
    {
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria);
        Archivos[nombreAlmacenado] = memoria.ToArray();
    }

    public Stream? AbrirLectura(string nombreAlmacenado) =>
        Archivos.TryGetValue(nombreAlmacenado, out var bytes) ? new MemoryStream(bytes) : null;
}
