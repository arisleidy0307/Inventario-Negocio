# Imagen de Stock Core: compila la API y el Enviador de correos y los deja en una imagen de ejecución.
# La usan los servicios «api» y «enviador» de docker-compose.yml. No contiene ninguna clave (RD-10).

# --- Etapa 1: compilación ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion
WORKDIR /src

# Primero solo los proyectos, para que Docker reutilice la restauración de paquetes entre compilaciones.
COPY Directory.Build.props ./
COPY src/Core/Core.csproj src/Core/
COPY src/Negocio/Negocio.csproj src/Negocio/
COPY src/Datos/Datos.csproj src/Datos/
COPY src/Api/Api.csproj src/Api/
COPY src/EnviadorCorreos/EnviadorCorreos.csproj src/EnviadorCorreos/
RUN dotnet restore src/Api/Api.csproj \
 && dotnet restore src/EnviadorCorreos/EnviadorCorreos.csproj

COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/EnviadorCorreos/EnviadorCorreos.csproj -c Release -o /app/enviador --no-restore

# --- Etapa 2: ejecución ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=compilacion /app ./

# Carpeta de los documentos: en docker-compose.yml se monta encima el volumen con nombre «documentos» (RF-DOC-05).
RUN mkdir -p /app/documentos && chown -R $APP_UID /app/documentos
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    DOCUMENTOS_RUTA=/app/documentos
EXPOSE 8080

# Por defecto arranca la API; el servicio «enviador» cambia el comando.
CMD ["dotnet", "/app/api/Inventario.Api.dll"]
