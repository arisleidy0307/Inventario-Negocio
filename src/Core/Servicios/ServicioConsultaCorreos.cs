using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Servicios;

/// <summary>Lo que el Administrador ve de un correo de la cola (RF-NOT-11). Sin el cuerpo: lleva enlaces y códigos.</summary>
public record EstadoCorreoEnCola(int Id, string Destinatario, string Asunto, EstadoCorreo Estado, int Intentos,
    DateTime FechaCreacion, DateTime? FechaUltimoIntento, DateTime? FechaEnvio, string? UltimoError);

/// <summary>Consulta de la cola de correos (solo Administrador, exigido en PoliticasAcceso).</summary>
public class ServicioConsultaCorreos
{
    public const int MaximoResultados = 500;

    private readonly CoreDbContext _db;

    public ServicioConsultaCorreos(CoreDbContext db)
    {
        _db = db;
    }

    /// <param name="estado">Pendiente, Enviado o Fallido; vacío para todos.</param>
    public async Task<IReadOnlyList<EstadoCorreoEnCola>> ListarAsync(string? estado)
    {
        EstadoCorreo? filtro = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoCorreo>(estado.Trim(), ignoreCase: true, out var e) || !Enum.IsDefined(e))
                throw ExcepcionControlada.Solicitud("El estado debe ser Pendiente, Enviado o Fallido.");
            filtro = e;
        }

        return await _db.CorreosEnCola.AsNoTracking()
            .Where(c => filtro == null || c.Estado == filtro)
            .OrderByDescending(c => c.Id)
            .Take(MaximoResultados)
            .Select(c => new EstadoCorreoEnCola(c.Id, c.Destinatario, c.Asunto, c.Estado, c.Intentos,
                c.FechaCreacion, c.FechaUltimoIntento, c.FechaEnvio, c.UltimoError))
            .ToListAsync();
    }
}
