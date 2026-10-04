namespace Inventario.Core.Comun;

/// <summary>Única fuente de la hora del sistema (RD-11). Siempre UTC.</summary>
public interface IReloj
{
    DateTime UtcNow { get; }
}

public sealed class RelojSistema : IReloj
{
    public DateTime UtcNow => DateTime.UtcNow;
}
