using Inventario.Api.Contratos;
using Inventario.Api.Infraestructura;
using Inventario.Core.Documentos;
using Inventario.Core.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Manejador de documentos (RF-DOC-01 a 05). Controlador delgado: las reglas están en ServicioDocumentos.</summary>
[ApiController]
[Route("api/documentos")]
public class DocumentosController : ControllerBase
{
    private readonly ServicioDocumentos _documentos;

    public DocumentosController(ServicioDocumentos documentos)
    {
        _documentos = documentos;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequiereOperacion(Operaciones.SubirDocumento)]
    public async Task<ActionResult<DocumentoResponse>> Subir([FromForm] SubirDocumentoRequest req)
    {
        var archivo = req.Archivo;
        await using var contenido = archivo?.OpenReadStream();
        var d = await _documentos.SubirAsync(User.UsuarioId(), archivo?.FileName, archivo?.ContentType,
            archivo?.Length ?? 0, contenido);
        return CreatedAtAction(nameof(Obtener), new { id = d.Id }, DocumentoResponse.Desde(d));
    }

    [HttpGet]
    [RequiereOperacion(Operaciones.ListarDocumentos)]
    public async Task<ActionResult<IEnumerable<DocumentoResponse>>> Listar() =>
        Ok((await _documentos.ListarAsync(User.UsuarioId(), User.Rol())).Select(DocumentoResponse.Desde));

    [HttpGet("{id:int}")]
    [RequiereOperacion(Operaciones.VerDocumento)]
    public async Task<ActionResult<DocumentoResponse>> Obtener(int id) =>
        Ok(DocumentoResponse.Desde(await _documentos.ObtenerAsync(id, User.UsuarioId(), User.Rol())));

    [HttpGet("{id:int}/descargar")]
    [RequiereOperacion(Operaciones.DescargarDocumento)]
    public async Task<IActionResult> Descargar(int id)
    {
        var (documento, contenido) = await _documentos.DescargarAsync(id, User.UsuarioId(), User.Rol());
        return File(contenido, documento.TipoContenido, documento.NombreOriginal);
    }

    [HttpDelete("{id:int}")]
    [RequiereOperacion(Operaciones.EliminarDocumento)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _documentos.EliminarAsync(id, User.UsuarioId(), User.Rol());
        return Ok(new MensajeResponse("Documento eliminado. Ya no aparece en el listado; su registro se conserva."));
    }
}
