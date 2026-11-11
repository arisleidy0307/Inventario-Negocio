namespace Inventario.Core.Documentos;

/// <summary>
/// Registro de un documento subido (RF-DOC-01). El archivo vive en el almacén de archivos
/// (un volumen fuera de la imagen, RF-DOC-05); aquí solo se guardan sus datos.
/// </summary>
public class Documento
{
    public const int LongitudMaximaNombre = 255;
    public const int LongitudMaximaTipo = 150;

    public int Id { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaSubida { get; set; }
    public int SubidoPorUsuarioId { get; set; }

    /// <summary>Nombre con el que se guardó el archivo en el almacén (no depende del nombre que envió el usuario).</summary>
    public string NombreAlmacenado { get; set; } = string.Empty;

    /// <summary>RF-DOC-04: la eliminación es lógica. El registro y el archivo siguen existiendo.</summary>
    public bool Eliminado { get; private set; }
    public DateTime? FechaEliminacion { get; private set; }
    public int? EliminadoPorUsuarioId { get; private set; }

    public void MarcarEliminado(int usuarioId, DateTime ahora)
    {
        Eliminado = true;
        FechaEliminacion = ahora;
        EliminadoPorUsuarioId = usuarioId;
    }
}
