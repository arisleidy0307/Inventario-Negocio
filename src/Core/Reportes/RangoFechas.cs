using System.Globalization;
using Inventario.Core.Comun;

namespace Inventario.Core.Reportes;

/// <summary>
/// Rango de fechas de un reporte (RF-REP-05). Las fechas llegan como AAAA-MM-DD en hora de República Dominicana
/// (UTC−4, sin horario de verano) y ambas son inclusivas: <c>hasta</c> cuenta el día completo.
/// En la base todo está en UTC, así que el rango se convierte a [desdeUtc, hastaUtc).
/// </summary>
public sealed record RangoFechas(DateOnly? Desde, DateOnly? Hasta)
{
    /// <summary>Diferencia de la hora local del negocio con UTC.</summary>
    public static readonly TimeSpan DesfaseLocal = TimeSpan.FromHours(-4);

    public static readonly RangoFechas Todo = new(null, null);

    public DateTime? DesdeUtc => Desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DesfaseLocal;

    /// <summary>Exclusivo: el primer instante del día siguiente a <see cref="Hasta"/>.</summary>
    public DateTime? HastaUtcExclusivo => Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DesfaseLocal;

    /// <summary>Valida y construye el rango; formato inválido o desde > hasta dan 400 controlado.</summary>
    public static RangoFechas Crear(string? desde, string? hasta)
    {
        var d = Leer(desde, "desde");
        var h = Leer(hasta, "hasta");
        if (d is not null && h is not null && d > h)
            throw ExcepcionControlada.Solicitud("La fecha «desde» no puede ser posterior a «hasta».");
        return new RangoFechas(d, h);
    }

    private static DateOnly? Leer(string? valor, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        if (DateOnly.TryParseExact(valor.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return fecha;
        throw ExcepcionControlada.Solicitud($"La fecha «{nombre}» debe tener el formato AAAA-MM-DD, por ejemplo 2026-11-26.");
    }
}
