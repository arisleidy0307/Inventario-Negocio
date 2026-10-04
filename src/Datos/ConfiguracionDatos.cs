using Inventario.Core.Datos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Datos;

public static class ConfiguracionDatos
{
    /// <summary>
    /// Registra el contexto con SQL Server. La cadena de conexión se lee de la variable
    /// de entorno ConnectionStrings__Default (RD-10); nunca se guarda en el repositorio.
    /// </summary>
    public static IServiceCollection AgregarDatos(this IServiceCollection services, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException(
                "Falta la variable de entorno ConnectionStrings__Default con la cadena de conexión a SQL Server.");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cadena));
        services.AddScoped<CoreDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }
}
