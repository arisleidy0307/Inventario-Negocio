using Inventario.Core.Documentos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Datos;

/// <summary>Registros de documentos en SQL Server: sobreviven a reiniciar o recrear el contenedor (RD-09).</summary>
public class RepositorioDocumentosEf : IRepositorioDocumentos
{
    private readonly CoreDbContext _db;

    public RepositorioDocumentosEf(CoreDbContext db)
    {
        _db = db;
    }

    public Task<Documento?> ObtenerAsync(int id) => _db.Documentos.FirstOrDefaultAsync(d => d.Id == id);

    public async Task<IReadOnlyList<Documento>> ListarVigentesAsync(int? usuarioId) =>
        await _db.Documentos
            .Where(d => !d.Eliminado && (usuarioId == null || d.SubidoPorUsuarioId == usuarioId))
            .OrderByDescending(d => d.FechaSubida)
            .ToListAsync();

    public void Agregar(Documento documento) => _db.Documentos.Add(documento);

    public Task GuardarAsync() => _db.SaveChangesAsync();
}
