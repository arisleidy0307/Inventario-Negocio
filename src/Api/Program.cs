using Inventario.Api.Infraestructura;
using Inventario.Core.Comun;
using Inventario.Datos;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AgregarDatos(builder.Configuration);
builder.Services.AddSingleton<IReloj, RelojSistema>();

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
