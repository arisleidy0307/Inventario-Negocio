using Inventario.Core.Comun;
using Inventario.Core.Datos;
using Inventario.Core.Documentos;
using Inventario.Core.Notificaciones;
using Inventario.Core.Permisos;
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
        services.AddScoped<ServicioConsultaCorreos>();
        services.AddScoped<ServicioRegistro>();
        services.AddScoped<ServicioSesion>();
        services.AddScoped<ServicioUsuarios>();
        services.AddScoped<ServicioContrasenas>();
        services.AddScoped<IRepositorioPermisos, RepositorioPermisosEf>();
        // Motor de notificaciones (pieza 4): lo usan Gestión de permisos y el negocio a través de INotificador
        services.AddScoped<IRepositorioNotificaciones, RepositorioNotificacionesEf>();
        services.AddScoped<ServicioNotificaciones>();
        services.AddScoped<INotificador>(sp => sp.GetRequiredService<ServicioNotificaciones>());
        services.AddScoped<ServicioPermisos>();

        // Manejador de documentos (Pieza 3)
        services.AddSingleton<IAlmacenArchivos>(new AlmacenArchivosEnDisco(opciones.DocumentosRuta));
        services.AddScoped<IRepositorioDocumentos, RepositorioDocumentosEf>();
        services.AddScoped(sp => new ServicioDocumentos(
            sp.GetRequiredService<IRepositorioDocumentos>(), sp.GetRequiredService<IAlmacenArchivos>(),
            sp.GetRequiredService<IReloj>(), opciones.DocumentosMaxMb * 1024L * 1024L));
        return services;
    }
}
