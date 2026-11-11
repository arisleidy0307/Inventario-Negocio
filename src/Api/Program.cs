using Inventario.Api.Infraestructura;
using Inventario.Core;
using Inventario.Core.Servicios;
using Inventario.Core.Comun;
using Inventario.Datos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// La URL de la API externa lleva la clave: nunca se registran las peticiones HTTP salientes (RD-10).
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AgregarDatos(builder.Configuration);
builder.Services.AddSingleton<IReloj, RelojSistema>();

// Parámetros desde variables de entorno (RD-10). Ningún valor sensible vive en el repositorio.
var opciones = new OpcionesCore
{
    AppBaseUrl = builder.Configuration["APP_BASE_URL"] ?? "https://localhost:7001",
    MinutosActivacion = int.TryParse(builder.Configuration["ACTIVACION_MINUTOS"], out var ma) && ma > 0 ? ma : 24 * 60,
    MinutosRecuperacion = int.TryParse(builder.Configuration["RECUPERACION_MINUTOS"], out var mr) && mr > 0 ? mr : 30,
    HorasSesion = int.TryParse(builder.Configuration["SESION_HORAS"], out var hs) && hs > 0 ? hs : 8,
    DocumentosRuta = string.IsNullOrWhiteSpace(builder.Configuration["DOCUMENTOS_RUTA"])
        ? Path.Combine(builder.Environment.ContentRootPath, "almacen-documentos")
        : builder.Configuration["DOCUMENTOS_RUTA"]!,
    DocumentosMaxMb = int.TryParse(builder.Configuration["DOCUMENTOS_MAX_MB"], out var mb) && mb > 0 ? mb : 10
};
builder.Services.AgregarCore(opciones);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var esquema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        In = ParameterLocation.Header,
        Description = "Pega aquí el token que devuelve POST /api/auth/login",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", esquema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [esquema] = Array.Empty<string>() });
});

builder.Services.AddAuthentication(AutenticacionSesion.Esquema)
    .AddScheme<AuthenticationSchemeOptions, AutenticacionSesion>(AutenticacionSesion.Esquema, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UsarManejoErrores();

// Aplica las migraciones pendientes al arrancar (RD-09: los datos viven en SQL Server).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // En Docker la base puede tardar unos segundos más en aceptar conexiones: se reintenta antes de rendirse.
    for (var intento = 1; ; intento++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (intento < 12)
        {
            app.Logger.LogWarning("La base de datos aún no responde ({Error}). Reintento {Intento} de 12 en 5 s.",
                ex.GetType().Name, intento);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }

    // Primer Administrador desde ADMIN_EMAIL y ADMIN_PASSWORD (sin él no se puede probar nada de Administrador).
    var usuarios = scope.ServiceProvider.GetRequiredService<ServicioUsuarios>();
    if (await usuarios.AsegurarAdministradorAsync(builder.Configuration["ADMIN_EMAIL"], builder.Configuration["ADMIN_PASSWORD"]))
        app.Logger.LogInformation("Administrador inicial creado.");
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
