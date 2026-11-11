using Inventario.Core.Documentos;

namespace Inventario.Api.Contratos;

/// <summary>Formulario multipart para subir un documento. El campo se llama «archivo».</summary>
public class SubirDocumentoRequest
{
    public IFormFile? Archivo { get; set; }
}

public record DocumentoResponse(int Id, string Nombre, string Tipo, long TamanoBytes, DateTime FechaSubida, int SubidoPorUsuarioId)
{
    public static DocumentoResponse Desde(Documento d) =>
        new(d.Id, d.NombreOriginal, d.TipoContenido, d.TamanoBytes, d.FechaSubida, d.SubidoPorUsuarioId);
}
