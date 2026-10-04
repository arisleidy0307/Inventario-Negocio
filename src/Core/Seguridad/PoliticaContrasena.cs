using Inventario.Core.Comun;

namespace Inventario.Core.Seguridad;

/// <summary>
/// RF-CA-14: única política de contraseña. Se usa en el registro, el restablecimiento y el cambio.
/// </summary>
public static class PoliticaContrasena
{
    public const int LongitudMinima = 8;
    public const int LongitudMaxima = 128;
    public const string Mensaje = "La contraseña debe tener al menos 8 caracteres e incluir letras y números.";

    public static bool Cumple(string? contrasena) =>
        !string.IsNullOrEmpty(contrasena)
        && contrasena.Length >= LongitudMinima
        && contrasena.Length <= LongitudMaxima
        && contrasena.Any(char.IsLetter)
        && contrasena.Any(char.IsDigit);

    public static void Validar(string? contrasena)
    {
        if (!Cumple(contrasena))
            throw ExcepcionControlada.Solicitud(Mensaje);
    }
}
