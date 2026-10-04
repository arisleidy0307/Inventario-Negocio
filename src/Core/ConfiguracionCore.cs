using Inventario.Core.Seguridad;
using Inventario.Core.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Core;

public static class ConfiguracionCore
{
    public static IServiceCollection AgregarCore(this IServiceCollection services, OpcionesCore opciones)
    {
        services.AddSingleton(opciones);
        services.AddSingleton<HasherContrasena>();
        services.AddSingleton<GeneradorTokens>();
        services.AddScoped<ColaCorreos>();
        services.AddScoped<ServicioRegistro>();
        services.AddScoped<ServicioSesion>();
        return services;
    }
}
