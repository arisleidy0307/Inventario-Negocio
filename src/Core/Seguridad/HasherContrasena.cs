using System.Security.Cryptography;

namespace Inventario.Core.Seguridad;

/// <summary>
/// RF-CA-02 / RD-05: PBKDF2-SHA256 con sal aleatoria de 16 bytes por usuario y 100 000 iteraciones.
/// Dos usuarios con la misma contraseña obtienen valores distintos porque la sal es distinta.
/// </summary>
public class HasherContrasena
{
    private const int TamanoSal = 16;
    private const int TamanoHash = 32;
    private const int Iteraciones = 100_000;

    public (string Hash, string Sal) Hashear(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(sal));
    }

    public bool Verificar(string contrasena, string hashGuardado, string salGuardada)
    {
        if (string.IsNullOrEmpty(hashGuardado) || string.IsNullOrEmpty(salGuardada)) return false;
        try
        {
            var sal = Convert.FromBase64String(salGuardada);
            var esperado = Convert.FromBase64String(hashGuardado);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(contrasena, sal, Iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false; // hash inutilizable (p. ej. tras un restablecimiento forzado)
        }
    }
}
