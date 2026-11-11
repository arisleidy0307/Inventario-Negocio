namespace Inventario.Core.Documentos;

/// <summary>Dónde se guardan los bytes de los documentos. En el contenedor es un volumen con nombre (RF-DOC-05).</summary>
public interface IAlmacenArchivos
{
    Task GuardarAsync(string nombreAlmacenado, Stream contenido);

    /// <summary>Abre el archivo para leerlo, o devuelve null si no existe en el almacén.</summary>
    Stream? AbrirLectura(string nombreAlmacenado);
}
