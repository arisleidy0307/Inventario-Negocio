using Inventario.Api.Infraestructura;
using Inventario.Core;
using Inventario.Core.Servicios;
using Inventario.Core.Comun;
using Inventario.Datos;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AgregarDatos(builder.Configuration);
builder.Services.AddSingleton<IReloj, RelojSistema>();

// Parámetros desde variables de entorno (RD-10). Ningún valor sensible vive en el repositorio.
var opciones = new OpcionesCore
{
    AppBaseUrl = builder.Configuration["APP_BASE_URL"] ?? "https://localhost:7001",
    MinutosActivacion = int.TryParse(builder.Configuration["ACTIVACION_MINUTOS"], out var ma) && ma > 0 ? ma : 24 * 60
};
builder.Services.AgregarCore(opciones);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapControllers();

app.Run();
