using System.Security.Cryptography;
using System.Text;

namespace Inventario.Core.Seguridad;

/// <summary>
/// Genera valores aleatorios criptográficamente seguros (32 bytes, Base64Url) para enlaces,
/// códigos y credenciales de sesión. En la base de datos solo se guarda su hash SHA-256.
/// </summary>
public class GeneradorTokens
{
    public (string Token, string Hash) Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (token, Hashear(token));
    }

    public string Hashear(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty));
        return Convert.ToHexString(hash);
    }
}
