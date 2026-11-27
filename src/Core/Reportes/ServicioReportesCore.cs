using Inventario.Core.Datos;
using Inventario.Core.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Core.Reportes;

public record FilaSolicitudesPorMes(string Mes, string Estado, int Cantidad);

public record TotalSolicitudesPorEstado(string Estado, int Cantidad);

public record ReporteSolicitudesPermiso(RangoFechas Rango, string Alcance, IReadOnlyList<FilaSolicitudesPorMes> Filas,
    IReadOnlyList<TotalSolicitudesPorEstado> PorEstado, int Total);

/// <summary>
/// Reporte del Core (RF-REP-01): solicitudes de permiso agrupadas por mes y estado. La agregación la hace la
/// base (GROUP BY + COUNT); nunca se traen las solicitudes una por una (RF-REP-03).
/// RF-REP-04: un Estándar solo cuenta las suyas; el Administrador, las de todos.
/// </summary>
public class ServicioReportesCore
{
    private readonly CoreDbContext _db;

    public ServicioReportesCore(CoreDbContext db)
    {
        _db = db;
    }

    public async Task<ReporteSolicitudesPermiso> SolicitudesPermisoAsync(int actorId, Rol rolActor, RangoFechas rango)
    {
        int? soloDe = rolActor == Rol.Administrador ? null : actorId;
        var desde = rango.DesdeUtc;
        var hasta = rango.HastaUtcExclusivo;
        var horas = RangoFechas.DesfaseLocal.TotalHours;

        var grupos = await _db.SolicitudesPermiso.AsNoTracking()
            .Where(s => soloDe == null || s.SolicitanteId == soloDe)
            .Where(s => desde == null || s.FechaSolicitud >= desde)
            .Where(s => hasta == null || s.FechaSolicitud < hasta)
            .GroupBy(s => new
            {
                Anio = s.FechaSolicitud.AddHours(horas).Year,
                Mes = s.FechaSolicitud.AddHours(horas).Month,
                s.Estado
            })
            .Select(g => new { g.Key.Anio, g.Key.Mes, g.Key.Estado, Cantidad = g.Count() })
            .ToListAsync();

        var filas = grupos
            .OrderBy(g => g.Anio).ThenBy(g => g.Mes).ThenBy(g => g.Estado)
            .Select(g => new FilaSolicitudesPorMes($"{g.Anio:0000}-{g.Mes:00}", g.Estado.ToString(), g.Cantidad))
            .ToList();
        var porEstado = grupos
            .GroupBy(g => g.Estado)
            .OrderBy(g => g.Key)
            .Select(g => new TotalSolicitudesPorEstado(g.Key.ToString(), g.Sum(x => x.Cantidad)))
            .ToList();

        return new ReporteSolicitudesPermiso(rango, Alcance(soloDe), filas, porEstado, filas.Sum(f => f.Cantidad));
    }

    public static string Alcance(int? soloDe) =>
        soloDe is null ? "Todos los usuarios (Administrador)" : "Solo tus datos (usuario Estándar)";
}
