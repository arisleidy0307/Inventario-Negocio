namespace Inventario.Core.Documentos;

public interface IRepositorioDocumentos
{
    /// <summary>Busca un documento por su Id, incluidos los eliminados.</summary>
    Task<Documento?> ObtenerAsync(int id);

    /// <summary>Documentos no eliminados. Con <paramref name="usuarioId"/> solo los de ese usuario; con null, todos.</summary>
    Task<IReadOnlyList<Documento>> ListarVigentesAsync(int? usuarioId);

    void Agregar(Documento documento);
    Task GuardarAsync();
}
