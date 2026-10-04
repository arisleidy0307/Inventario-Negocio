using System.Net.Mail;
using Inventario.Core.Comun;

namespace Inventario.Core.Seguridad;

/// <summary>RD-07: un correo vacío o mal formado produce un rechazo controlado.</summary>
public static class ValidadorCorreo
{
    public static string Normalizar(string? correo)
    {
        var limpio = (correo ?? string.Empty).Trim().ToLowerInvariant();
        if (!EsValido(limpio))
            throw ExcepcionControlada.Solicitud("El correo es obligatorio y debe tener un formato válido.");
        return limpio;
    }

    public static bool EsValido(string correo)
    {
        if (string.IsNullOrWhiteSpace(correo) || correo.Length > 254) return false;
        if (!MailAddress.TryCreate(correo, out var direccion)) return false;
        // Exige dominio con punto (usuario@dominio.tld) y que no haya texto extra.
        return direccion.Address == correo && direccion.Host.Contains('.') && !direccion.Host.EndsWith('.');
    }
}
