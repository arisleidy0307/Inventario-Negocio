namespace Inventario.Core.Documentos;

/// <summary>
/// Guarda los archivos en una carpeta del disco (DOCUMENTOS_RUTA). En Docker esa carpeta es
/// el volumen con nombre «documentos», así que los archivos sobreviven a destruir el contenedor.
/// </summary>
public class AlmacenArchivosEnDisco : IAlmacenArchivos
{
    private readonly string _carpeta;

    public AlmacenArchivosEnDisco(string carpeta)
    {
        _carpeta = Path.GetFullPath(carpeta);
        Directory.CreateDirectory(_carpeta);
    }

    public async Task GuardarAsync(string nombreAlmacenado, Stream contenido)
    {
        await using var destino = new FileStream(Ruta(nombreAlmacenado), FileMode.CreateNew, FileAccess.Write);
        await contenido.CopyToAsync(destino);
    }

    public Stream? AbrirLectura(string nombreAlmacenado)
    {
        var ruta = Ruta(nombreAlmacenado);
        return File.Exists(ruta) ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

    // El nombre almacenado lo genera el servicio (un Guid); aun así se descarta cualquier ruta.
    private string Ruta(string nombreAlmacenado) => Path.Combine(_carpeta, Path.GetFileName(nombreAlmacenado));
}
