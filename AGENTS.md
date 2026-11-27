# AGENTS.md: contexto para el agente

Este archivo es lo primero que debes leer antes de tocar el repositorio. Si algo de lo que te pido choca con
estas reglas, **detente y pregúntame**; no decidas por tu cuenta.

## Propósito del proyecto

**Stock Core** es la API de inventario de un negocio pequeño (Programación III, ITLA). Tiene:

- **Core**, reutilizable por cualquier negocio:
  - Control de acceso: registro con activación por correo, sesión, roles Administrador/Estándar y recuperación de contraseña.
  - Gestión de permisos: solicitudes que un Administrador aprueba o rechaza.
  - Manejador de documentos.
  - Una **cola de correos** con un proceso enviador independiente.
- **Negocio**: productos, proveedores y órdenes de compra con su máquina de estados
  (Borrador → Emitida → Recibida, y Cancelada). Además, la cotización de órdenes con una API externa de tasas de cambio.

La tarea que te delego es **el motor de notificaciones, completar la cola de correos y dos reportes con
agregación**. Lo que debe cumplir está en **`docs/criterios-notificaciones-reportes.md`**: tu trabajo se acepta
solo si cumple cada criterio de ese archivo, y yo los compruebo uno por uno.

## Lenguaje, framework y herramientas

| Qué | Versión |
|---|---|
| Lenguaje | C# 12 (`<LangVersion>latest</LangVersion>`), `Nullable` e `ImplicitUsings` activados |
| Framework | .NET 8 (`net8.0`), ASP.NET Core Web API con controladores |
| Datos | EF Core 8.0.8 sobre SQL Server. Migraciones en `src/Datos/Migraciones`; la API las aplica sola al arrancar |
| Pruebas | xUnit 2.9 en `tests/Core.Tests`; SQLite en memoria solo para pruebas de integración |
| Contenedores | Docker Compose: servicios `db` (SQL Server), `api` y `enviador` |

## Estructura

```
src/Core/            Control de acceso, Gestión de permisos, Documentos, cola de correos. NO referencia a Negocio
src/Negocio/         Dominio, máquina de estados y servicios del inventario. Referencia solo a Core
src/Datos/           AppDbContext (Core + Negocio), repositorios EF, migraciones y clientes externos
src/Api/             Controladores delgados, autenticación, filtro [RequiereOperacion], manejo de errores
src/EnviadorCorreos/ Proceso independiente que envía la cola
tests/Core.Tests/    Pruebas (una carpeta por pieza)
```

## Cómo correrlo

Con Docker (es como lo revisan):

```powershell
Copy-Item .env.example .env
docker compose up --build
```

La API responde en **http://localhost:5001** (Swagger en `/swagger`). Los correos, con `CORREO_MODO=consola`, se
leen con `docker compose logs enviador`. Sin Docker, ver la sección «4. Ejecutar sin Docker» del README.

## Cómo probarlo

En la raíz:

```powershell
dotnet test
```

**Todas** las pruebas deben pasar antes de que me entregues algo. No necesita Docker, SQL Server ni internet.
Cada comportamiento nuevo lleva su prueba.

## Convenciones de código

- **Todo en español**: clases, métodos, variables, mensajes y comentarios (`ServicioNotificaciones`,
  `MarcarComoLeidaAsync`). PascalCase para tipos y miembros públicos, camelCase para variables y parámetros,
  `_camelCase` para campos privados. Los métodos asíncronos terminan en `Async`.
- **Controladores delgados (RD-02)**: solo traducen HTTP y llaman a un servicio. Las reglas van en los servicios
  o en las entidades.
- **Autorización en un solo lugar**: cada operación nueva se declara en `src/Core/Seguridad/PoliticasAcceso.cs`
  (`Operaciones` + `PorOperacion`) y el endpoint lleva `[RequiereOperacion(...)]`. Las reglas de dueño
  («solo el propio usuario») van en el servicio.
- **Errores previstos** con `ExcepcionControlada` (`Solicitud` 400, `Prohibido` 403, `NoEncontrado` 404,
  `Conflicto` 409, `NoDisponible` 503). Nunca devolver trazas ni excepciones sin manejar (RD-07, RD-08).
- **Fechas** siempre en UTC con `IReloj` (nada de `DateTime.Now` en servicios), para poder fijar el reloj en
  las pruebas (RD-11).
- **Correos**: la API **nunca** envía correo directamente. Solo los encola (`ColaCorreos` o la tabla
  `CorreosEnCola`) y el `EnviadorCorreos` los envía (RF-NOT-08).
- **Persistencia**: los cambios de modelo van en una **migración nueva** dentro de `src/Datos/Migraciones`.
  La agregación de los reportes se hace en la consulta (`GroupBy`/`Sum`/`Count` traducidos a SQL), no trayendo
  todas las filas para sumarlas en el controlador.
- **Pruebas**: una prueba por comportamiento, con nombre `Operacion_Situacion_Resultado` y las tres fases
  marcadas `// Preparar`, `// Actuar`, `// Verificar`. Cada prueba crea sus propios datos.
- **Commits**: uno por cambio coherente, con el formato `tipo(RF-XX-NN): descripción en español`
  (`feat`, `test`, `docs`, `build`). Una rama por funcionalidad.

## Restricciones

**No puedes tocar** (si crees que hace falta, pregúntame primero):

- Las **migraciones existentes** en `src/Datos/Migraciones`: no se editan ni se borran; solo se agregan nuevas.
- Las **máquinas de estados**: `src/Negocio/Estados/TransicionesOrdenCompra.cs` y
  `src/Core/Permisos/TransicionesSolicitudPermiso.cs`, con sus tablas de transiciones.
- Las **reglas existentes** de Control de acceso, Gestión de permisos, Documentos y Negocio. Puedes llamarlas o
  avisar de sus eventos, pero no cambiar lo que permiten o rechazan.
- Las **pruebas existentes**: no se borran, no se debilitan, no se saltan (`Skip`).
- Las **rutas y respuestas de los endpoints existentes**, el puerto `5001` y los nombres de los servicios y
  volúmenes de `docker-compose.yml`.
- Los **paquetes NuGet**: no agregues dependencias nuevas sin preguntar.

**Secretos (nunca, bajo ninguna circunstancia):**

- **No leas, abras, imprimas ni modifiques el archivo `.env`** ni `scripts/variables.ps1`. Si necesitas saber
  qué variables existen, mira `.env.example` y `scripts/variables.ejemplo.ps1`.
- **No escribas credenciales** (contraseñas, claves de API, usuarios SMTP reales, cadenas de conexión con
  contraseña real) en el código, en `appsettings`, en `Dockerfile`, en `docker-compose.yml`, en la documentación
  ni en los mensajes de commit. Todo valor sensible se lee de una variable de entorno (RD-10).
- En los ejemplos usa valores ficticios evidentes (`tu-correo@gmail.com`, `tu-clave-de-exchangerate-api`).

**Git:** trabaja en ramas y deja commits; **no hagas `push`, no crees etiquetas y no fusiones a `main`**. Eso
lo hago yo después de revisar contra los criterios.
