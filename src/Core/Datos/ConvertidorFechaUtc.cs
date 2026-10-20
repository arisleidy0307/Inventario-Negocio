using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inventario.Core.Datos;

/// <summary>
/// SQL Server (datetime2) no guarda si una fecha es UTC. Todas las fechas del sistema se generan con
/// IReloj.UtcNow (RD-11), así que al leerlas se marcan como UTC: la API las devuelve con "Z" y una
/// fecha consultada coincide exactamente con la que se devolvió al registrarla.
/// </summary>
public sealed class ConvertidorFechaUtc : ValueConverter<DateTime, DateTime>
{
    public ConvertidorFechaUtc()
        : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}
