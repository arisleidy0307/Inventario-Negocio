using Inventario.Api.Infraestructura;
using Inventario.Core;
using Inventario.Core.Servicios;
using Inventario.Core.Comun;
using Inventario.Datos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AgregarDatos(builder.Configuration);
builder.Services.AddSingleton<IReloj, RelojSistema>();

// Parámetros desde variables de entorno (RD-10). Ningún valor sensible vive en el repositorio.
var opciones = new OpcionesCore
{
    AppBaseUrl = builder.Configuration["APP_BASE_URL"] ?? "https://localhost:7001",
    MinutosActivacion = int.TryParse(builder.Configuration["ACTIVACION_MINUTOS"], out var ma) && ma > 0 ? ma : 24 * 60,
    HorasSesion = int.TryParse(builder.Configuration["SESION_HORAS"], out var hs) && hs > 0 ? hs : 8
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
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
