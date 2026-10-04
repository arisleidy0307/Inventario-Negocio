namespace Inventario.Core.Comun;

/// <summary>
/// Error previsto por las reglas de negocio. La API lo convierte en una respuesta
/// controlada con el código y el mensaje indicados (RD-07, RD-08).
/// </summary>
public class ExcepcionControlada : Exception
{
    public int CodigoEstado { get; }

    public ExcepcionControlada(int codigoEstado, string mensaje) : base(mensaje)
    {
        CodigoEstado = codigoEstado;
    }

    public static ExcepcionControlada Solicitud(string mensaje) => new(400, mensaje);
    public static ExcepcionControlada NoAutorizado(string mensaje) => new(401, mensaje);
    public static ExcepcionControlada Prohibido(string mensaje) => new(403, mensaje);
    public static ExcepcionControlada NoEncontrado(string mensaje) => new(404, mensaje);
    public static ExcepcionControlada Conflicto(string mensaje) => new(409, mensaje);
}
