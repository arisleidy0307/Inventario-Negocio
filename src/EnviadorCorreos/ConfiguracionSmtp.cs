namespace Inventario.EnviadorCorreos;

/// <summary>RF-NOT-13 / RD-10: las credenciales SMTP se leen solo de variables de entorno.</summary>
public record ConfiguracionSmtp(string Host, int Puerto, string Usuario, string Contrasena, string Remitente, bool UsarSsl)
{
    public static ConfiguracionSmtp? DesdeEntorno(out string? error)
    {
        string? Leer(string nombre) => Environment.GetEnvironmentVariable(nombre);

        var faltantes = new[] { "SMTP_HOST", "SMTP_PORT", "SMTP_USER", "SMTP_PASSWORD", "SMTP_FROM" }
            .Where(n => string.IsNullOrWhiteSpace(Leer(n))).ToList();
        if (faltantes.Count > 0)
        {
            error = "Faltan variables de entorno: " + string.Join(", ", faltantes);
            return null;
        }
        if (!int.TryParse(Leer("SMTP_PORT"), out var puerto))
        {
            error = "SMTP_PORT debe ser un número (por ejemplo 587).";
            return null;
        }

        error = null;
        var ssl = !string.Equals(Leer("SMTP_SSL"), "false", StringComparison.OrdinalIgnoreCase);
        return new ConfiguracionSmtp(Leer("SMTP_HOST")!, puerto, Leer("SMTP_USER")!, Leer("SMTP_PASSWORD")!,
            Leer("SMTP_FROM")!, ssl);
    }
}
