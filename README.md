# Stock Core: Inventario del negocio

Programación III (TDS-007), ITLA 2026-C-3. **Práctica 1: Control de acceso completo.** · **Asignación 2: Gestión de permisos y su primera suite de pruebas.** · **Práctica 2: una suite que detecta defectos.** · **Práctica 3: módulo de negocio, sus pruebas y su integración.** · **Práctica 4: tu proyecto en un contenedor, con documentos y una API externa.**

API en ASP.NET Core 8 + EF Core + SQL Server. Tiene registro con activación por correo, sesión con
credencial opaca, roles, administración de usuarios, recuperación de contraseña, una cola de correos
con un enviador independiente, Gestión de permisos, un **Manejador de documentos** y el módulo de negocio de
**inventario** (productos, proveedores y órdenes de compra con su máquina de estados), que cotiza las órdenes en otra
moneda con una **API externa** de tasas de cambio. Todo levanta con **`docker compose up --build`**.

## Contenedor

El proyecto completo corre en contenedores. **Solo hace falta Docker** (Docker Desktop en Windows o macOS, o Docker
Engine con Compose v2): no hay que instalar .NET ni SQL Server, ni crear la base a mano. La composición levanta todo
y la API aplica las migraciones al arrancar.

```powershell
git clone https://github.com/arisleidy0307/Inventario-Negocio.git
cd Inventario-Negocio
git checkout practica-4
Copy-Item .env.example .env        # en Linux o macOS: cp .env.example .env
docker compose up --build
```

- **Dirección:** la API responde en **http://localhost:5001**. Swagger: **http://localhost:5001/swagger**.
- **Está lista** cuando el log de `api-1` muestra `Now listening on: http://[::]:8080`. La primera vez tarda unos
  minutos: descarga las imágenes, compila y SQL Server se inicializa.
- **Administrador inicial:** `ADMIN_EMAIL` / `ADMIN_PASSWORD` del `.env`. Con el `.env.example` tal cual:
  `admin@stockcore.com` / `Admin1234`.
- **Detener:** `Ctrl+C` en la terminal de `docker compose up`, o `docker compose down` desde otra terminal en la
  carpeta del proyecto. Los datos **se conservan**. Solo `docker compose down -v` borra los volúmenes (vuelve a cero).
- En segundo plano: `docker compose up --build -d` y, para ver el log, `docker compose logs -f api`.

Archivos: `Dockerfile` (imagen en dos etapas: compila con el SDK de .NET 8 y ejecuta con el runtime de ASP.NET),
`.dockerignore` (deja fuera `bin/`, `obj/`, `.git/`, `.env`, pruebas y documentación), `docker-compose.yml` y
`.env.example`.

| Servicio | Qué es | Puerto del equipo |
|---|---|---|
| `api` | La API (imagen del `Dockerfile`) | **5001** → 8080 del contenedor. Es el único que publica un puerto |
| `db` | SQL Server 2022 | Ninguno: solo lo alcanzan `api` y `enviador`, por el nombre `db` (`Server=db,1433`) |
| `enviador` | El Enviador de correos (misma imagen), revisando la cola cada 15 s | Ninguno |

| Volumen con nombre | Dónde se monta | Qué guarda |
|---|---|---|
| `documentos` | `/app/documentos` del servicio `api` | Los archivos de los documentos (RF-DOC-05), fuera de la imagen |
| `datos-sqlserver` | `/var/opt/mssql` del servicio `db` | La base: usuarios, sesiones, solicitudes, órdenes y registros de documentos (RD-09) |

### Variables del contenedor (`.env`)

`.env.example` trae **todas** las variables con un valor de ejemplo; copiarlo a `.env` es el único paso previo. `.env`
está en `.gitignore` y en `.dockerignore`: no se versiona ni entra a la imagen. `docker-compose.yml` solo tiene
referencias `${...}` a esos valores.

| Variable | Para qué | Valor de ejemplo |
|---|---|---|
| `MSSQL_SA_PASSWORD` | Contraseña de `sa` en el servicio `db`; la API y el enviador la usan para conectarse | `StockCore2026Sql` |
| `APP_BASE_URL` | Con ella se arman los enlaces de los correos | `http://localhost:5001` |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Primer Administrador | `admin@stockcore.com`, `Admin1234` |
| `CORREO_MODO` | `consola`: los correos se escriben en el log del enviador. `smtp`: se envían de verdad | `consola` |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`, `SMTP_SSL` | Solo con `CORREO_MODO=smtp` (sección 5.5) | `smtp.gmail.com`, `587`, ... |
| `EXCHANGE_API_KEY` | Clave de la API externa (sección **API externa**) | `tu-clave-de-exchangerate-api` |
| `EXCHANGE_API_URL` | Dirección de la API externa; solo se cambia para simular que está caída | `https://v6.exchangerate-api.com/v6/` |
| `ACTIVACION_MINUTOS`, `RECUPERACION_MINUTOS`, `SESION_HORAS`, `DOCUMENTOS_MAX_MB` | Opcionales | `1440`, `30`, `8`, `10` |

### Los correos dentro del contenedor

Con `CORREO_MODO=consola` (el del `.env.example`) la API encola los correos como siempre y el enviador los procesa,
pero en lugar de mandarlos por SMTP **escribe cada correo completo en su log**: destinatario, asunto y el cuerpo con el
enlace de activación o el código de recuperación. Para leerlos:

```powershell
docker compose logs enviador
```

Ejemplo real (prueba del 10 de noviembre de 2026 con el `.env.example`):

```
enviador-1  | [01:37:36] Revisando la cola de correos...
enviador-1  |   ----------------------------------------------------------------
enviador-1  |   Para:   ...+p4@gmail.com
enviador-1  |   Asunto: Activa tu cuenta - Inventario
enviador-1  |
enviador-1  |   Hola Almacen P4:
enviador-1  |
enviador-1  |   Para activar tu cuenta abre este enlace:
enviador-1  |   http://localhost:5001/api/auth/activar?token=DN3RFGZwnTNiZzPhRlaIZlfb7H8O...
enviador-1  |
enviador-1  |   El enlace se puede usar una sola vez y vence en 1440 minutos.
enviador-1  |   ----------------------------------------------------------------
enviador-1  |   [enviado] #1 -> ...+p4@gmail.com: Activa tu cuenta - Inventario
enviador-1  | [01:37:36] Enviados: 1. Con error (siguen pendientes): 0.
```

Al arrancar puede salir una vez `No se pudo revisar la cola (SqlException). Se reintenta en 15 s.`: es el enviador
esperando a que la API cree las tablas; a los 15 s ya revisa la cola normalmente.

Los enlaces usan `APP_BASE_URL` (`http://localhost:5001`), así que se abren tal cual en el navegador. Para enviar
correos reales, en `.env` pon `CORREO_MODO=smtp` y los datos `SMTP_*` de una cuenta (por ejemplo, Gmail con una
contraseña de aplicación) y aplica el cambio con `docker compose up -d`.

### Los datos sobreviven a destruir los contenedores (RF-DOC-05, RD-09)

1. Con un usuario Estándar, sube un documento (sección 9) y anota su `id`.
2. `docker compose down` (**sin** `-v`): los contenedores se eliminan.
3. `docker compose up` otra vez y espera a `Now listening on`.
4. `GET /api/documentos` lo sigue listando, `GET /api/documentos/{id}/descargar` devuelve el mismo archivo y el
   usuario inicia sesión con su contraseña de siempre.

### El resto del README, con Docker

Todo lo demás funciona igual contra el contenedor, con `$base = "http://localhost:5001"`. Solo cambia esto:

| Donde el README dice... | Con Docker |
|---|---|
| Definir variables con `scripts\variables.ps1` (sección 3) | Se definen en `.env` |
| "Ejecutar el EnviadorCorreos" | Ya corre solo en el servicio `enviador`; los correos se leen con `docker compose logs enviador` |
| `sqlcmd -S "(localdb)\MSSQLLocalDB" -d InventarioP3 -Q "..."` | `docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P StockCore2026Sql -C -d Inventario -Q "SELECT Id, Correo, Rol, Activo FROM Usuarios"` (la contraseña es la `MSSQL_SA_PASSWORD` de tu `.env`) |
| Detener la API y arrancarla otra vez | `docker compose restart api` |

### Revisión acumulada: dónde se prueba cada frente

| Frente | Sección |
|---|---|
| Registro con activación por correo | 5.1 (el enlace sale en `docker compose logs enviador`) |
| Inicio y cierre de sesión | 5.2 |
| Un Estándar invocando una operación de Administrador: rechazo | 5.3 (RF-CA-06) y 7.2 (crear producto como Estándar: `403`) |
| Recuperación de contraseña | 5.4 (el código sale en `docker compose logs enviador`) |
| Gestión de permisos: crear, aprobar, rechazar y transiciones fuera de la tabla | 6.2 |
| Módulo de negocio: una operación válida y la transición prohibida | 7.2 y 7.3 (Borrador → Recibida: `409`) |

---

## API externa

| | |
|---|---|
| **API** | **ExchangeRate-API** v6 (https://www.exchangerate-api.com), endpoint `pair`: `GET https://v6.exchangerate-api.com/v6/{clave}/pair/DOP/{moneda}`. Exige clave; el plan gratuito da 1 500 consultas al mes |
| **Operación que la usa** | **Cotizar una orden de compra en otra moneda:** `GET /api/ordenes-compra/{id}/cotizacion?moneda=USD` (con sesión; un Estándar solo cotiza sus órdenes) |
| **Variable** | **`EXCHANGE_API_KEY`**, en `.env`. `.env.example` la trae con el valor de ejemplo `tu-clave-de-exchangerate-api` |
| **Código** | `src/Negocio/Servicios/ServicioCotizacionOrdenes.cs` (la operación) y `src/Datos/Externos/ClienteExchangeRateApi.cs` (el cliente HTTP) |

La operación toma la orden (sus detalles y su total, en pesos dominicanos), le pide a la API la tasa DOP → moneda y
devuelve el total convertido, la tasa y la fecha de la tasa. No reenvía la respuesta de la API: si la orden no existe,
es de otro Estándar, no tiene detalles o la moneda no es un código de 3 letras, se rechaza **sin llamar a la API**.

Para usar una clave real: créala gratis en https://www.exchangerate-api.com, ponla en `.env`
(`EXCHANGE_API_KEY=...`) y aplica el cambio con `docker compose up -d`.

```powershell
# con $hEst del dueño de la orden (sección 6.1) y una orden con detalles (sección 7.2)
Invoke-RestMethod "$base/api/ordenes-compra/1/cotizacion?moneda=USD" -Headers $hEst
```

Ejemplo real, obtenido el 10 de noviembre de 2026 con una clave gratuita (la clave no aparece en la respuesta).
Respuesta de ExchangeRate-API a `GET https://v6.exchangerate-api.com/v6/{clave}/pair/DOP/USD`:

```json
{"result":"success","documentation":"https://www.exchangerate-api.com/docs","terms_of_use":"https://www.exchangerate-api.com/terms","time_last_update_unix":1794355202,"time_last_update_utc":"Wed, 11 Nov 2026 00:00:02 +0000","time_next_update_unix":1794441602,"time_next_update_utc":"Thu, 12 Nov 2026 00:00:02 +0000","base_code":"DOP","target_code":"USD","conversion_rate":0.01646}
```

Lo que devolvió la operación del proyecto para una orden de 5 × 850 DOP (`GET /api/ordenes-compra/1/cotizacion?moneda=USD`):

```json
{
  "ordenId": 1,
  "estado": "Borrador",
  "monedaOrden": "DOP",
  "totalOrden": 4250,
  "moneda": "USD",
  "tasa": 0.01646,
  "totalConvertido": 69.96,
  "fechaTasa": "2026-11-11T00:00:02Z",
  "fuente": "ExchangeRate-API"
}
```

Con `moneda=EUR` la misma orden dio `tasa: 0.01469` y `totalConvertido: 62.43`.

### Sin clave o con la API caída: rechazo controlado (RD-07, RD-08)

El resto del proyecto sigue funcionando; solo la cotización responde con un mensaje claro:

| Situación | Respuesta de `GET /api/ordenes-compra/{id}/cotizacion` |
|---|---|
| Con el `.env.example` tal cual (clave de ejemplo) o una clave inválida | `503` "ExchangeRate-API rechazó la clave de EXCHANGE_API_KEY. Revisa el valor en tu archivo .env; el resto del sistema sigue funcionando." |
| Variable vacía (`EXCHANGE_API_KEY=`) | `503` "La cotización no está disponible: falta configurar la variable EXCHANGE_API_KEY en el archivo .env." |
| API caída o sin internet (para provocarlo: `EXCHANGE_API_URL=http://api-caida.invalid/` en `.env` y `docker compose up -d`) | `503` "El servicio de tasas de cambio (ExchangeRate-API) no responde. Intenta más tarde; el resto del sistema sigue funcionando." |
| Moneda inválida (`moneda=dolares`) o la misma `DOP` | `400`, sin llamar a la API |
| Moneda que la API no soporta (`moneda=XYZ`, con clave real) | `400` "La moneda XYZ no está soportada por ExchangeRate-API." |
| Orden de otro Estándar | `403`, sin llamar a la API |

La clave no está en el código, en el `Dockerfile`, en `docker-compose.yml` ni en la imagen: llega al contenedor como
variable de entorno desde `.env`, que nunca se versiona. Los mensajes de error no la muestran y la API no registra
las peticiones HTTP salientes (cuya URL lleva la clave). Para comprobar que no está en el historial:
`git log --all -p | Select-String "<tu clave>"` no encuentra nada, y `git log --all -- .env` no muestra ningún commit.


## 1. Requisitos previos

| Herramienta | Versión |
|---|---|
| .NET SDK | 8.0 o superior. El proyecto apunta a `net8.0` con `RollForward=Major`, así que también corre con .NET 9 o 10. |
| Docker | Solo para la sección **Contenedor**: con Docker no hace falta nada más de esta tabla |
| SQL Server | LocalDB (`(localdb)\MSSQLLocalDB`, viene con Visual Studio) o cualquier SQL Server |
| Cuenta SMTP | Por ejemplo Gmail con una **contraseña de aplicación** (https://myaccount.google.com/apppasswords) |

No hace falta `dotnet ef`. **La API aplica las migraciones sola al arrancar** y crea la base de datos.

## 2. Estructura

```
Inventario.sln
src/
├─ Core/             Control de acceso y correo. NO referencia a Negocio (RD-03)
│  ├─ Dominio/       Usuario, Rol, Sesion, CodigoVerificacion, CorreoEnCola
│  ├─ Servicios/     ServicioRegistro, ServicioSesion, ServicioUsuarios, ServicioContrasenas, ColaCorreos
│  ├─ Seguridad/     PoliticasAcceso, HasherContrasena, GeneradorTokens, PoliticaContrasena, ValidadorCorreo
│  ├─ Permisos/      Gestión de permisos: SolicitudPermiso, TransicionesSolicitudPermiso, ServicioPermisos
│  ├─ Documentos/    Manejador de documentos: Documento, ServicioDocumentos, AlmacenArchivosEnDisco
│  ├─ Datos/         CoreDbContext, RepositorioPermisosEf, RepositorioDocumentosEf + configuraciones del Core
│  └─ Comun/         IReloj (RD-11), ExcepcionControlada
├─ Negocio/          Dominio (Producto, Proveedor, OrdenCompra...), Estados (máquina de estados) y Servicios
│                    (incluye ServicioCotizacionOrdenes, que usa la API externa)
├─ Datos/            AppDbContext (Core + Negocio), RepositorioNegocioEf, Migraciones y
│                    Externos/ClienteExchangeRateApi (API externa de tasas de cambio)
├─ Api/              Controladores delgados (RD-02), autenticación, filtro de roles, manejo de errores
└─ EnviadorCorreos/  Consola independiente que envía la cola por SMTP (o la escribe en su log con CORREO_MODO=consola)
tests/Core.Tests/    Pruebas xUnit sin levantar la API (RD-12): GestionPermisos/, Negocio/, Documentos/, ApiExterna/
docs/                maquina-de-estados.md, gestion-permisos.md, pruebas.http, bitácoras
scripts/             variables.ejemplo.ps1 (para ejecutar sin Docker)
Dockerfile, .dockerignore, docker-compose.yml, .env.example    Contenedor (sección «Contenedor»)
```

## 3. Variables de entorno (sin Docker)

Ningún valor sensible está en el repositorio (RD-10). Todo se lee de variables de entorno. Con Docker se definen en
`.env` (sección **Contenedor**); esta sección es para ejecutar con `dotnet run` y LocalDB.

| Variable | Obligatoria | Para qué |
|---|---|---|
| `ConnectionStrings__Default` | Sí (API y Enviador) | Cadena de conexión a SQL Server |
| `APP_BASE_URL` | Sí | URL de la API con la que se arman los enlaces de los correos. Ej.: `http://localhost:5001` |
| `ADMIN_EMAIL` | Sí | Correo del primer Administrador, que se crea al arrancar si no existe |
| `ADMIN_PASSWORD` | Sí | Contraseña del primer Administrador (mínimo 8 caracteres, con letras y números) |
| `SMTP_HOST` | Sí (Enviador) | Servidor SMTP. Ej.: `smtp.gmail.com` |
| `SMTP_PORT` | Sí (Enviador) | Puerto SMTP. Ej.: `587` |
| `SMTP_USER` | Sí (Enviador) | Usuario de la cuenta SMTP |
| `SMTP_PASSWORD` | Sí (Enviador) | Contraseña (de aplicación) de la cuenta SMTP |
| `SMTP_FROM` | Sí (Enviador) | Remitente de los correos |
| `SMTP_SSL` | No (`true`) | `false` para desactivar STARTTLS (servidores de prueba locales) |
| `ACTIVACION_MINUTOS` | No (`1440`) | Vigencia del enlace de activación |
| `RECUPERACION_MINUTOS` | No (`30`) | Vigencia del código de recuperación |
| `SESION_HORAS` | No (`8`) | Vigencia de la credencial de sesión |
| `CORREO_MODO` | No (`smtp`) | `smtp` envía por SMTP; `consola` escribe cada correo en la salida del Enviador |
| `EXCHANGE_API_KEY` | Solo para cotizar | Clave de ExchangeRate-API (sección **API externa**). Sin ella, la cotización da `503` controlado |
| `DOCUMENTOS_RUTA` | No (`src/Api/almacen-documentos`) | Carpeta donde se guardan los archivos de los documentos (en Docker, el volumen `documentos`) |
| `DOCUMENTOS_MAX_MB` | No (`10`) | Tamaño máximo de un documento |

### Cómo definirlas en PowerShell

Hay una plantilla en `scripts/variables.ejemplo.ps1`:

```powershell
Copy-Item scripts\variables.ejemplo.ps1 scripts\variables.ps1   # scripts\variables.ps1 está en .gitignore
notepad scripts\variables.ps1                                    # rellena tus valores
. .\scripts\variables.ps1                                        # cárgalas (en CADA terminal que abras)
```

También se pueden definir una a una: `$env:SMTP_HOST = "smtp.gmail.com"`.

## 4. Ejecutar sin Docker

Con Docker basta la sección **Contenedor**. Sin Docker:

```powershell
git clone https://github.com/arisleidy0307/Inventario-Negocio.git
cd Inventario-Negocio
git checkout practica-3

# Terminal 1: API (crea la BD, aplica migraciones y crea el Administrador inicial)
. .\scripts\variables.ps1
dotnet run --project src/Api --launch-profile https

# Terminal 2: enviador de correos (procesa la cola una vez y termina)
. .\scripts\variables.ps1
dotnet run --project src/EnviadorCorreos
#   o en modo continuo (revisa la cola cada 15 s):
dotnet run --project src/EnviadorCorreos -- --continuo
```

Las pruebas se ejecutan con un solo comando; ver la sección **Pruebas** más abajo.

- Swagger: **http://localhost:5001/swagger**. Para endpoints con sesión: botón **Authorize**, pegar el `token` de `/api/auth/login`.
- También están todas las peticiones listas en **`docs/pruebas.http`** (Visual Studio 2022 o la extensión REST Client de VS Code).
- El Administrador inicial es el usuario de `ADMIN_EMAIL` / `ADMIN_PASSWORD` y suele tener **Id = 1**.
- Si `dotnet ef` está instalado, `dotnet ef database update --project src/Datos --startup-project src/Api` hace lo mismo que el arranque. Es opcional.

### Endpoints

| Método | Ruta | Operación (PoliticasAcceso) | Exige |
|---|---|---|---|
| POST | `/api/auth/registro` | `Auth.Registrar` | Pública |
| GET | `/api/auth/activar?token=...` | `Auth.Activar` | Pública (enlace del correo) |
| POST | `/api/auth/reenviar-activacion` | `Auth.ReenviarActivacion` | Pública |
| POST | `/api/auth/login` | `Auth.Login` | Pública |
| GET | `/api/auth/yo` | `Auth.Yo` | Sesión |
| POST | `/api/auth/logout` | `Auth.Logout` | Sesión |
| POST | `/api/auth/recuperar` | `Auth.Recuperar` | Pública |
| POST | `/api/auth/restablecer` | `Auth.Restablecer` | Pública |
| POST | `/api/auth/cambiar-password` | `Auth.CambiarPassword` | Sesión |
| GET | `/api/usuarios` | `Usuarios.Listar` | Administrador **o** permiso otorgado (Gestión de permisos) |
| PUT | `/api/usuarios/{id}/rol` | `Usuarios.CambiarRol` | Administrador |
| POST | `/api/usuarios/{id}/desactivar` | `Usuarios.Desactivar` | Administrador |
| POST | `/api/usuarios/{id}/reactivar` | `Usuarios.Reactivar` | Administrador |
| POST | `/api/usuarios/{id}/forzar-restablecimiento` | `Usuarios.ForzarRestablecimiento` | Administrador |
| GET | `/api/solicitudes-permiso/operaciones` | `Permisos.Catalogo` | Sesión |
| POST | `/api/solicitudes-permiso` | `Permisos.Solicitar` | Sesión |
| GET | `/api/solicitudes-permiso/mias` | `Permisos.MisSolicitudes` | Sesión |
| GET | `/api/solicitudes-permiso/{id}` | `Permisos.VerSolicitud` | Sesión (dueño o Administrador) |
| GET | `/api/solicitudes-permiso/pendientes` | `Permisos.ListarPendientes` | Administrador |
| POST | `/api/solicitudes-permiso/{id}/aprobar` | `Permisos.Aprobar` | Administrador |
| POST | `/api/solicitudes-permiso/{id}/rechazar` | `Permisos.Rechazar` | Administrador |
| POST | `/api/productos` | `Productos.Crear` | Administrador |
| GET | `/api/productos` | `Productos.Listar` | Sesión |
| GET | `/api/productos/{id}/movimientos` | `Productos.Movimientos` | Sesión |
| POST | `/api/proveedores` | `Proveedores.Crear` | Administrador |
| GET | `/api/proveedores` | `Proveedores.Listar` | Sesión |
| POST | `/api/ordenes-compra` | `Ordenes.Crear` | Sesión |
| GET | `/api/ordenes-compra` | `Ordenes.Listar` | Sesión (un Estándar solo ve las suyas) |
| GET | `/api/ordenes-compra/{id}` | `Ordenes.Ver` | Sesión (un Estándar solo ve las suyas) |
| POST | `/api/ordenes-compra/{id}/detalles` | `Ordenes.AgregarDetalle` | Sesión (quien la creó o Administrador) |
| POST | `/api/ordenes-compra/{id}/emitir` | `Ordenes.Emitir` | Administrador |
| POST | `/api/ordenes-compra/{id}/recibir` | `Ordenes.Recibir` | Administrador **o** permiso otorgado (Gestión de permisos) |
| POST | `/api/ordenes-compra/{id}/cancelar` | `Ordenes.Cancelar` | Administrador |
| GET | `/api/ordenes-compra/{id}/cotizacion?moneda=USD` | `Ordenes.Cotizar` | Sesión (un Estándar solo cotiza las suyas). Usa la API externa |
| POST | `/api/documentos` (multipart, campo `archivo`) | `Documentos.Subir` | Sesión |
| GET | `/api/documentos` | `Documentos.Listar` | Sesión (un Estándar solo ve los suyos; el Administrador, todos) |
| GET | `/api/documentos/{id}` | `Documentos.Ver` | Sesión (dueño o Administrador) |
| GET | `/api/documentos/{id}/descargar` | `Documentos.Descargar` | Sesión (dueño o Administrador) |
| DELETE | `/api/documentos/{id}` | `Documentos.Eliminar` | Sesión (dueño o Administrador). Eliminación lógica |

La exigencia de rol de **todas** las operaciones está en un solo lugar:
**`src/Core/Seguridad/PoliticasAcceso.cs`** (RF-CA-05). El atributo `[RequiereOperacion]`
(`src/Api/Infraestructura/RequiereOperacionAttribute.cs`) la aplica en el servidor.

## 5. Cómo provocar cada criterio de aceptación

Para consultar la base de datos se puede usar SSMS conectado a `(localdb)\MSSQLLocalDB`, base
`InventarioP3`, o `sqlcmd`:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d InventarioP3 -Q "SELECT Id, Correo, Rol, Activo, Deshabilitado FROM Usuarios"
```

Para enviar una petición a mano desde PowerShell, sin la interfaz (RD-06):

```powershell
$login = Invoke-RestMethod -Method Post http://localhost:5001/api/auth/login -ContentType "application/json" `
         -Body '{"correo":"usuario@correo.com","contrasena":"Clave1234"}'
$token = $login.token
Invoke-RestMethod http://localhost:5001/api/usuarios -Headers @{ Authorization = "Bearer $token" }
```

### 5.1 Registro y activación

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-CA-15 | `POST /api/auth/registro` con `{ "nombre", "correo", "contrasena": "Clave1234" }`. Después, ejecutar el EnviadorCorreos. | `201`. El usuario queda con `Activo = 0`. Llega un correo con el enlace de activación. |
| RF-CA-15 | `POST /api/auth/login` con ese usuario **antes** de activar | `403` "La cuenta no está activa..." |
| RF-CA-16 | Abrir el enlace del correo en el navegador | Página "Cuenta activada". Después el login funciona (`200` con `token`). |
| RF-CA-16 | Abrir el mismo enlace por segunda vez | `400` "El enlace de activación no es válido, ya fue usado o está vencido". El estado no cambia. |
| RF-CA-16 (vencido) | Arrancar la API con `$env:ACTIVACION_MINUTOS="1"`, registrar y abrir el enlace pasado 1 minuto. O bien: `UPDATE CodigosVerificacion SET FechaVencimiento = DATEADD(minute,-1,GETUTCDATE())` | `400`. La cuenta sigue inactiva. |
| RF-CA-01 | Registrar otra vez el mismo correo (también con mayúsculas o espacios) | `409` "Ya existe una cuenta registrada con ese correo" |
| RF-CA-14 | Registro con `"contrasena": "ab123"`, `"abcdefgh"` o `"12345678"` | `400` "La contraseña debe tener al menos 8 caracteres e incluir letras y números." |
| RD-07 | Registro con `"correo": "esto-no-es-correo"` o vacío | `400` controlado, sin excepción |
| RF-CA-02 / RD-05 | Registrar dos usuarios con la **misma** contraseña y consultar `SELECT Correo, ContrasenaHash, ContrasenaSal FROM Usuarios` | La contraseña no aparece. Hash y sal son distintos en los dos usuarios (PBKDF2-SHA256, 100 000 iteraciones, sal de 16 bytes). |
| RF-CA-17 | `POST /api/auth/reenviar-activacion` con un correo existente sin activar, y luego con uno inexistente | Las dos respuestas son idénticas (`200`, mismo mensaje). Si existe, se encola un enlace nuevo y **el anterior deja de servir**. |

### 5.2 Sesión

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-CA-03 | Login con contraseña incorrecta, y login con un correo inexistente | Las dos respuestas son idénticas: `401` "Credenciales inválidas." |
| RF-CA-03 | Login correcto | `200` con `token` (credencial opaca; en la BD solo se guarda su hash, tabla `Sesiones`) |
| RF-CA-07 | `GET /api/auth/yo` con `Authorization: Bearer <token>`, y luego sin encabezado o con un token inventado | Con token: id, nombre, correo y rol. Sin token válido: `401`. |
| RF-CA-18 | `POST /api/auth/logout` con el token. Luego `GET /api/auth/yo` con el mismo token | `200`, y después `401` |
| RF-CA-19 | 5 logins seguidos con contraseña incorrecta y luego uno con la **correcta** | Los 5 primeros: `401`. El 6.º: `423` "La cuenta está bloqueada temporalmente...", aunque la contraseña sea correcta. Se desbloquea a los 15 minutos (o con `UPDATE Usuarios SET BloqueadoHasta = NULL`). Un login correcto pone `IntentosFallidos` en 0. |

### 5.3 Roles y administración

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-CA-04 | `SELECT Correo, Rol FROM Usuarios` | Cada usuario tiene exactamente un rol: `Administrador` o `Estandar`. Los registrados nacen `Estandar`. |
| RF-CA-05 | Abrir `src/Core/Seguridad/PoliticasAcceso.cs` | Diccionario único operación → exigencia |
| RF-CA-06 / RD-06 | Con token de **Estándar**: `GET /api/usuarios` con `Invoke-RestMethod` (sección 5), o `POST /api/usuarios/1/desactivar` | `403` "No tienes permiso para realizar esta operación." (Nota: desde la Asignación 2, `GET /api/usuarios` es otorgable; si a ese Estándar se le aprobó una solicitud de `Usuarios.Listar`, usa `POST /api/usuarios/1/desactivar`, que nunca es otorgable.) |
| RF-CA-08 | Con token de Estándar: `PUT /api/usuarios/{suId}/rol` con `{ "rol": "Administrador" }` | `403`. Un Estándar no cambia ningún rol, ni el propio. |
| RF-CA-21 | Con token de Administrador: `GET /api/usuarios` | Lista con id, nombre, correo, rol y estado. **Sin** hashes, sales ni tokens. |
| RF-CA-08 | Administrador: `PUT /api/usuarios/{id}/rol` con `{ "rol": "Administrador" }` o `"Estandar"` | `200`. El cambio se refleja en `/api/auth/yo` del usuario. |
| RF-CA-20 | El usuario X inicia sesión (token T). El Administrador hace `POST /api/usuarios/{X}/desactivar`. Luego `GET /api/auth/yo` con T, y X intenta hacer login. | Con T: `401`. Login: `403` "La cuenta está desactivada". `POST /api/usuarios/{X}/reactivar` lo revierte. |
| RF-CA-20 | Administrador: `POST /api/usuarios/{suPropioId}/desactivar` | `400` "No puedes desactivar tu propia cuenta." |

### 5.4 Contraseñas

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-CA-09 | `POST /api/auth/recuperar` con un correo existente y luego con uno inexistente | Las dos respuestas son idénticas (`200`, mismo mensaje) |
| RF-CA-10 | Ejecutar el EnviadorCorreos | Llega el correo con el código (vence en `RECUPERACION_MINUTOS`) |
| RF-CA-11 | `POST /api/auth/restablecer` con `{ "codigo", "nuevaContrasena": "Nueva1234" }` | `200`. Login con la vieja: `401`. Login con la nueva: `200`. |
| RF-CA-10 | Repetir `restablecer` con el mismo código (o con uno vencido) | `400` "El código de recuperación no es válido, ya fue usado o está vencido". La contraseña no cambia. |
| RF-CA-12 | Guardar un token de **antes** del restablecimiento y usarlo en `GET /api/auth/yo` | `401` |
| RF-CA-13 | Administrador: `POST /api/usuarios/{id}/forzar-restablecimiento`. Después ejecutar el Enviador. | La contraseña anterior deja de servir (`401`). Sus sesiones se invalidan. Llega el correo con el código para usar en `restablecer`. |
| RF-CA-22 | Con sesión: `POST /api/auth/cambiar-password` con `contrasenaActual` incorrecta | `400` "La contraseña actual no es correcta." |
| RF-CA-22 | Con la actual correcta y una nueva que no cumple la política | `400` (RF-CA-14) |
| RF-CA-22 | Con la actual correcta y una nueva válida | `200`. El token usado deja de servir (RF-CA-12): hay que volver a iniciar sesión. |

### 5.5 Correo por cola

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-NOT-08 (sin SMTP) | **No** ejecutar el Enviador (o desconectarse de internet) y registrar un usuario. La API nunca se conecta al SMTP. | El registro responde `201`. `SELECT Id, Destinatario, Estado FROM CorreosEnCola` muestra el correo en `Pendiente`. |
| RF-NOT-08 (SMTP caído) | Ejecutar el Enviador con `$env:SMTP_HOST="smtp.invalido.local"` | El enviador informa el error y el correo **sigue `Pendiente`** (se guarda `UltimoError`) |
| RF-NOT-09 | Con las variables SMTP correctas: `dotnet run --project src/EnviadorCorreos` | "Enviados: N". El correo llega de verdad y la fila pasa a `Enviado` con `FechaEnvio`. |
| RF-NOT-12 | Ejecutar el Enviador **otra vez** | "Enviados: 0". No hay duplicados: cada fila se reclama con un `UPDATE ... WHERE Estado = 'Pendiente'` antes de enviarla. |
| RF-NOT-13 | `git grep -i -E "smtp_password|password=" -- . ':!README.md' ':!scripts/variables.ejemplo.ps1'` | Las credenciales SMTP solo se leen de variables de entorno |

#### Constancia: prueba del enviador contra un SMTP real

El enviador se probó contra un servidor SMTP real (Gmail), no contra un simulador. Prueba del **25 de octubre de
2026**, desde la etiqueta `practica-2`, con la API arrancada sobre una base de datos nueva (`InventarioP2Revision`).

| Dato | Valor |
|---|---|
| Servidor | `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=587`, STARTTLS (`SMTP_SSL` sin definir, por defecto `true`) |
| Autenticación | Cuenta Gmail del proyecto con **contraseña de aplicación** de Google, leída de `SMTP_USER` / `SMTP_PASSWORD` (nunca en el repositorio) |
| Destinatario | Un buzón Gmail real de la autora (alias `+p2`) |

Secuencia ejecutada y resultado observado:

1. `POST /api/auth/registro` de un usuario nuevo → `201`; el correo queda `Pendiente` en `CorreosEnCola`.
2. `dotnet run --project src/EnviadorCorreos`:
   ```
   [16:00:51] Revisando la cola de correos...
     [enviado] #1 -> ...+p2@gmail.com: Activa tu cuenta - Inventario
   [16:00:56] Enviados: 1. Con error (siguen pendientes): 0.
   ```
3. El correo **«Activa tu cuenta - Inventario»** llegó al buzón real. Al abrir el enlace: «Cuenta activada».
   Al abrirlo por segunda vez: rechazo (enlace ya usado).
4. Segunda ejecución del enviador (RF-NOT-12, sin duplicados):
   ```
   [16:03:56] Revisando la cola de correos...
   [16:03:57] Enviados: 0. Con error (siguen pendientes): 0.
   ```
5. `POST /api/auth/recuperar` → el enviador mandó **«Recupera tu contraseña - Inventario»** (`[enviado] #2`). El código
   recibido en el buzón real restableció la contraseña (`200`). Reutilizarlo dio `400`, la contraseña vieja dio `401`,
   la nueva `200`, y la credencial de sesión emitida antes del cambio dio `401`.

6. **SMTP caído (RF-NOT-08):** con la API corriendo se registró otro usuario → `201` (la API nunca toca el SMTP) y
   se ejecutó el enviador apuntando a un servidor inexistente (`$env:SMTP_HOST = "smtp.invalido.local"`):
   ```
   [16:21:24] Revisando la cola de correos...
     [error]   #3 -> ...+smtpcaido@gmail.com: Failure sending mail.
   [16:21:25] Enviados: 0. Con error (siguen pendientes): 1.
   ```
   En la base (`SELECT Id, Estado, Intentos, FechaEnvio, UltimoError FROM CorreosEnCola`) el correo #3 quedó
   `Pendiente`, `Intentos = 1`, `FechaEnvio` en `NULL` y `UltimoError = Failure sending mail.`; los #1 y #2 seguían
   `Enviado`.
7. **Recuperación del servidor:** con el `SMTP_HOST` real de nuevo, el mismo correo pendiente salió:
   ```
   [16:23:06] Revisando la cola de correos...
     [enviado] #3 -> ...+smtpcaido@gmail.com: Activa tu cuenta - Inventario
   [16:23:09] Enviados: 1. Con error (siguen pendientes): 0.
   ```

Para repetirla: define las variables `SMTP_*` de la sección 3 con una cuenta propia (en Gmail se necesita la
verificación en 2 pasos y una contraseña de aplicación en https://myaccount.google.com/apppasswords) y sigue las
filas RF-NOT-09 y RF-NOT-12 de esta sección.

### 5.6 Persistencia y errores

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RD-09 | Detener la API (Ctrl+C) y arrancarla de nuevo | Los usuarios siguen ahí (SQL Server) |
| RD-08 | Cualquier error inesperado (por ejemplo, detener LocalDB con `sqllocaldb stop MSSQLLocalDB` y llamar a un endpoint) | `500` con `ProblemDetails` genérico, sin traza, ruta ni SQL |

## 6. Gestión de permisos (Asignación 2)

Un usuario pide permiso para ejecutar una operación que su rol no le da. Un Administrador lo aprueba o
lo rechaza. Al aprobar, **el sistema aplica el permiso automáticamente** (RF-GP-05).

- Operaciones que se pueden pedir: las marcadas como *otorgables* en `src/Core/Seguridad/PoliticasAcceso.cs`.
  Hoy son **`Usuarios.Listar`** (`GET /api/usuarios`) y **`Ordenes.Recibir`** (recibir órdenes de compra, sección 7).
  La lista también sale en `GET /api/solicitudes-permiso/operaciones`.
- Estados y transiciones, en **un solo punto** (RD-04): `src/Core/Permisos/TransicionesSolicitudPermiso.cs`.
  Tabla completa en **[`docs/gestion-permisos.md`](docs/gestion-permisos.md)**.

```
Pendiente ──aprobar (Administrador)──▶ Aprobada ──aplicar (sistema, en la misma operación)──▶ Aplicada
    └─────rechazar con motivo (Administrador)──▶ Rechazada
```

- Pieza: `src/Core/Permisos/` (`SolicitudPermiso`, `ServicioPermisos`, `IRepositorioPermisos`).
  Persistencia: tablas `SolicitudesPermiso` y `PermisosUsuario` (migración `GestionPermisos`, se aplica sola al arrancar).
- Los permisos otorgados se leen de la BD en cada petición y los aplica `[RequiereOperacion]` en el servidor (RD-06).

### 6.1 Preparación para probar

Se necesitan dos sesiones: un **Administrador** (el de `ADMIN_EMAIL`) y un **Estándar** (regístralo y actívalo
con el enlace del correo, sección 5.1). En Swagger usa **Authorize** con el token de cada uno, o en PowerShell:

```powershell
$base = "http://localhost:5001"
$adm = (Invoke-RestMethod -Method Post "$base/api/auth/login" -ContentType "application/json" -Body '{"correo":"<ADMIN_EMAIL>","contrasena":"<ADMIN_PASSWORD>"}').token
$est = (Invoke-RestMethod -Method Post "$base/api/auth/login" -ContentType "application/json" -Body '{"correo":"<correo-estandar>","contrasena":"<su-contraseña>"}').token
$hAdm = @{ Authorization = "Bearer $adm" }; $hEst = @{ Authorization = "Bearer $est" }
```

Para ver el código de estado de un rechazo en PowerShell 5.1, envuelve la llamada:
`try { ... } catch { $_.Exception.Response.StatusCode.value__; $_.ErrorDetails.Message }`.
Las mismas peticiones están en `docs/pruebas.http`.

### 6.2 Cómo provocar cada criterio

Síguelo en este orden: primero una solicitud que se **rechaza** (A) y luego otra que se **aprueba** (B).
Con `Invoke-RestMethod` los ejemplos usan `$base`, `$hAdm` y `$hEst` de la sección 6.1.

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-GP-05 (antes) | Como **Estándar**: `GET /api/usuarios` | `403` |
| RF-GP-01 | Como Estándar: `POST /api/solicitudes-permiso` con `{ "operacion": "Usuarios.Listar", "justificacion": "Necesito consultar usuarios" }` → `Invoke-RestMethod -Method Post "$base/api/solicitudes-permiso" -Headers $hEst -ContentType "application/json" -Body '{"operacion":"Usuarios.Listar","justificacion":"Necesito consultar usuarios"}'` | `201` con `"estado": "Pendiente"`. Es la **solicitud A**: anota su `id` |
| RD-07 | Como Estándar: el mismo `POST` con `"operacion": "Usuarios.CambiarRol"` o `"operacion": ""` | `400` "La operación no existe o no se puede solicitar..." |
| RF-GP-02 | Como **Administrador**: `GET /api/solicitudes-permiso/pendientes` | `200`, aparece A con el correo del solicitante |
| RF-GP-02 | Como **Estándar**: `GET /api/solicitudes-permiso/pendientes` | `403` |
| RF-GP-02 | Como Administrador crea una solicitud **propia** (mismo `POST` de RF-GP-01 con `$hAdm`; anota su `id`, es la **solicitud C**). Como Estándar: `GET /api/solicitudes-permiso/{idC}` | `403` "No puedes ver solicitudes de otros usuarios." (el Estándar solo ve las suyas en `GET /api/solicitudes-permiso/mias`) |
| RF-GP-07 | Como Administrador: `POST /api/solicitudes-permiso/{idC}/aprobar`, y `POST /api/solicitudes-permiso/{idC}/rechazar` con `{ "motivo": "x" }` | `403` "No puedes aprobar ni rechazar tu propia solicitud." en ambos; C sigue `Pendiente` |
| RD-06 | Como **Estándar**, construida a mano: `Invoke-RestMethod -Method Post "$base/api/solicitudes-permiso/{idA}/aprobar" -Headers $hEst` | `403`: la autorización se verifica en el servidor |
| RF-GP-04 | Como Administrador: `POST /api/solicitudes-permiso/{idA}/rechazar` con `{ }`, `{ "motivo": "" }` o `{ "motivo": "   " }` | `400` "Para rechazar una solicitud debes escribir el motivo." A sigue `Pendiente` |
| RF-GP-04 | Como Administrador: `POST /api/solicitudes-permiso/{idA}/rechazar` con `{ "motivo": "No corresponde a tu puesto" }` | `200`, `"estado": "Rechazada"` con `motivoRechazo`, `rechazadaPorId` y `fechaRechazo` |
| RF-GP-06 | Como Administrador: `POST /api/solicitudes-permiso/{idA}/aprobar` | `409` "Transición no permitida: la solicitud está Rechazada y no puede pasar a Aprobada." A sigue `Rechazada`. (Rechazada → Aplicada no tiene endpoint: la cubre la prueba `Aplicar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada`) |
| RF-GP-01 | Como Estándar: crea otra solicitud igual a la de RF-GP-01 | `201`, **solicitud B** en `Pendiente` |
| RF-GP-03 | Como Administrador: `POST /api/solicitudes-permiso/{idB}/aprobar` | `200`, con `aprobadaPorId` (id del Administrador), `fechaAprobacion`, `"estado": "Aplicada"` y `fechaAplicacion` |
| RF-GP-05 | En seguida, como Estándar **con el mismo token** (sin volver a iniciar sesión): `GET /api/usuarios` | `200` con el listado. `GET /api/auth/yo` muestra `"permisos": ["Usuarios.Listar"]` |
| RF-GP-06 | Como Administrador, aprobar B **otra vez**: `POST /api/solicitudes-permiso/{idB}/aprobar` | `409` "...está Aplicada y no puede pasar a Aprobada." `GET /api/solicitudes-permiso/{idB}` sigue `Aplicada` con la misma `fechaAprobacion` |
| RD-09 | Detener la API (Ctrl+C), arrancarla de nuevo; `GET /api/solicitudes-permiso/{idA}`, `{idB}` y `GET /api/usuarios` como Estándar (con un login nuevo si cerraste sesión) | A sigue `Rechazada`, B sigue `Aplicada` y el permiso sigue funcionando. En BD: `SELECT Id, SolicitanteId, Operacion, Estado, AprobadaPorId, FechaAprobacion, MotivoRechazo FROM SolicitudesPermiso` |

## Pruebas

Desde un clon limpio, en la raíz del repositorio, **un solo comando** ejecuta toda la suite:

```powershell
dotnet test
```

Resultado esperado en `practica-4`: `total: 155; failed: 0` (en `practica-3` eran 119). No hace falta SQL Server,
Docker, variables de entorno, internet ni levantar la API (RD-12):

- Gestión de permisos y el módulo de negocio se prueban con repositorios en memoria y un reloj fijo
  (`tests/Core.Tests/GestionPermisos/Dobles.cs`, `tests/Core.Tests/Negocio/RepositorioNegocioEnMemoria.cs`).
- La **prueba de integración** (`tests/Core.Tests/Negocio/IntegracionNegocioPermisosTests.cs`) usa los componentes
  **reales** de los dos módulos (servicios, repositorios EF Core y `AppDbContext`) sobre una base **SQLite en memoria**
  que se crea y se descarta en cada prueba. La aplicación usa SQL Server; SQLite solo existe durante la prueba.
- El **Manejador de documentos** (`tests/Core.Tests/Documentos/`) se prueba con dobles en memoria y, además, con sus
  piezas reales: archivos en una carpeta temporal y `RepositorioDocumentosEf` sobre SQLite.
- La **API externa** (`tests/Core.Tests/ApiExterna/`) se prueba con un manejador HTTP falso: respuesta correcta, sin
  clave, clave rechazada, API caída, tiempo agotado y respuesta inválida. No usa la red ni una clave real.

Cada prueba crea sus propios datos, así que son independientes y pasan en cualquier orden. Todas tienen las fases
`// Preparar`, `// Actuar` y `// Verificar`, y su nombre dice qué verifican.

Filtros útiles:

- Solo la máquina de estados del negocio: `dotnet test --filter "FullyQualifiedName~MaquinaEstadosOrdenCompraTests"`
- Solo la integración: `dotnet test --filter "FullyQualifiedName~IntegracionNegocioPermisosTests"`
- Solo un requisito de Gestión de permisos: `dotnet test --filter "Requisito=RF-GP-03"`
- Solo el Manejador de documentos: `dotnet test --filter "FullyQualifiedName~Documentos"`
- Solo la API externa: `dotnet test --filter "FullyQualifiedName~CotizacionOrdenesTests"`

### Pruebas de la máquina de estados del negocio

Hay una prueba por cada fila de las tablas de `docs/maquina-de-estados.md` (permitidas, sus condiciones, prohibidas y
estados terminales), en `tests/Core.Tests/Negocio/MaquinaEstadosOrdenCompraTests.cs`. La correspondencia fila → prueba
está al final de ese documento. Las prohibidas verifican el `409` **y** que el estado no cambió.

### Prueba de integración: negocio + Gestión de permisos

`RecibirOrden_SeRechazaMientrasLaSolicitudNoEstaAprobada_YSePermiteTrasLaAprobacion` verifica las dos caras: un
Estándar con la solicitud `Ordenes.Recibir` todavía **Pendiente** recibe `403` y la orden sigue `Emitida` con el stock
en 0 (leído de la base); un Administrador aprueba la solicitud en Gestión de permisos y el mismo Estándar recibe la
orden: queda `Recibida` y el stock sube a 5. Las otras dos pruebas de la clase cubren «sin solicitud» y «solicitud
rechazada».

### Pruebas por requisito de Gestión de permisos

Archivos en `tests/Core.Tests/GestionPermisos/`: `ServicioPermisosTests.cs` (servicio), `SolicitudPermisoTests.cs`
(entidad y tabla de transiciones) y `AutorizacionPermisosTests.cs` (quién puede ejecutar cada operación).

| Requisito | Qué verifica la suite | Pruebas |
|---|---|---|
| RF-GP-01 | La solicitud nace en Pendiente; el Estándar puede solicitar | `CrearSolicitud_UsuarioEstandar_NaceEnEstadoPendiente`, `SolicitarPermiso_UsuarioEstandar_EstaPermitido`, `CrearSolicitud_OperacionQueNoSePuedeSolicitar_SeRechazaConError400`, `CrearSolicitud_YaHayUnaPendienteParaLaMismaOperacion_SeRechazaConError409` |
| RF-GP-02 | Un Estándar no ve solicitudes de otros ni las pendientes; el Administrador sí ve las pendientes | `ObtenerSolicitud_EstandarPideLaDeOtroUsuario_SeRechazaConError403`, `ObtenerSolicitud_EstandarPideLaSuya_LaDevuelve`, `SolicitudesDeUsuario_DevuelveSoloLasDeEseUsuario`, `Pendientes_HaySolicitudesResueltas_SoloDevuelveLasPendientes`, `ListarPendientes_UsuarioEstandar_SeRechaza`, `ListarPendientes_Administrador_EstaPermitido` |
| RF-GP-03 | Solo el Administrador aprueba; queda registrado quién y cuándo | `Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando`, `Aprobar_SolicitudPendiente_PasaAAprobadaYRegistraQuienYCuando`, `AprobarSolicitud_UsuarioEstandar_SeRechaza`, `AprobarSolicitud_Administrador_EstaPermitido`, `AprobarSolicitud_SinSesion_SeRechaza` |
| RF-GP-04 | Sin motivo (ausente, vacío o solo espacios) no se rechaza y el estado no cambia; con motivo se registra; solo el Administrador rechaza | `Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSiguePendiente`, `Rechazar_MotivoAusenteOVacio_SeRechazaYSiguePendiente`, `Rechazar_ConMotivo_QuedaRechazadaYRegistraMotivoQuienYCuando`, `RechazarSolicitud_UsuarioEstandar_SeRechaza` |
| RF-GP-05 | Tras aprobar llega a Aplicada y el permiso queda efectivo | `Aprobar_SolicitudPendiente_LlegaAAplicadaConFechaDeAplicacion`, `Aprobar_SolicitudPendiente_ElSolicitantePuedeEjecutarLaOperacionSinPasoManual` |
| RF-GP-06 | Aprobar dos veces, Rechazada → Aplicada y cualquier otra transición fuera de la tabla se rechazan y el estado no cambia | `Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia`, `Aprobar_SolicitudRechazada_SeRechazaYSigueRechazada`, `Rechazar_SolicitudYaAplicada_SeRechazaYSigueAplicada`, `Aplicar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada`, `Aplicar_SolicitudPendienteSinAprobar_SeRechazaYSiguePendiente`, `Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada`, `Rechazar_SolicitudYaRechazada_SeRechazaYConservaElPrimerMotivo`, `Puede_SoloPermiteLasTransicionesDeLaTabla` (los 16 pares de estados) |
| RF-GP-07 | Un Administrador no aprueba ni rechaza su propia solicitud | `Aprobar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente`, `Rechazar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente` |

El resto de la suite cubre Control de acceso (`tests/Core.Tests/SeguridadTests.cs`) y la máquina de estados del
negocio (`tests/Core.Tests/TransicionesOrdenCompraTests.cs`).

## Defectos

### Práctica 3: defectos plantados en mi propio código

Cada defecto es un commit cuyo padre es `practica-3`, etiquetado `practica-3-defecto-N`. Solo modifica código de
producción (ningún archivo de pruebas) y su mensaje dice qué archivo y qué línea cambia. Sobre `practica-3` pasan
todas las pruebas; sobre cada etiqueta, `dotnet test` reporta al menos estas fallas:

| Etiqueta | Defecto plantado | Prueba que lo detecta | Comportamiento que comprueba |
|---|---|---|---|
| `practica-3-defecto-1` | Se agrega a las permitidas la primera fila de prohibidas (Borrador → Recibida) en `TransicionesOrdenCompra.cs` | `Recibir_OrdenEnBorrador_SeRechazaYSigueEnBorrador` (y `Puede_SoloPermiteLasTransicionesDeLaTabla`, `ProhibidasExplicitas_NingunaEstaPermitida`) | Recibir una orden en Borrador da 409 y sigue en Borrador |
| `practica-3-defecto-2` | Se agrega Cancelada → Borrador: el estado terminal deja de serlo, en `TransicionesOrdenCompra.cs` | `Cancelada_EsTerminal_NingunaTransicionParteDeElla` (y `Puede_SoloPermiteLasTransicionesDeLaTabla`) | De Cancelada no parte ninguna transición |
| `practica-3-defecto-3` | Se elimina la verificación de la condición de la primera fila con condición (Borrador → Emitida: «al menos un detalle») en `OrdenCompra.cs` | `Emitir_OrdenSinDetalles_SeRechazaYSigueEnBorrador` | Emitir una orden sin detalles da 400 y sigue en Borrador |
| `practica-3-defecto-4` | Se elimina la consulta a Gestión de permisos en la operación de recibir órdenes, en `ServicioOrdenesCompra.cs` | `RecibirOrden_SeRechazaMientrasLaSolicitudNoEstaAprobada_YSePermiteTrasLaAprobacion` (integración; también `RecibirOrden_EstandarSinSolicitud_SeRechazaYLaOrdenSigueEmitida`, `RecibirOrden_SolicitudRechazada_SigueSinPermitirse` y `RecibirOrden_EstandarSinPermiso_SeRechazaYLaOrdenSigueEmitida`) | Un Estándar sin el permiso `Ordenes.Recibir` aprobado no puede recibir la orden |

Para comprobar un defecto:

```powershell
git checkout practica-3-defecto-1
git diff practica-3 practica-3-defecto-1 --stat
dotnet test
git checkout practica-3
```

### Práctica 2: variante B

Práctica 2, **variante B**. Cada defecto es un commit cuyo padre es `practica-2`, etiquetado
`practica-2-defecto-N`. Solo modifica código de Gestión de permisos (ningún archivo de pruebas) y su mensaje
dice qué archivo y qué línea cambia. Sobre `practica-2` pasan todas las pruebas; sobre cada etiqueta de defecto,
`dotnet test` reporta al menos estas fallas:

| Etiqueta | Defecto plantado | Prueba que lo detecta | Comportamiento que comprueba |
|---|---|---|---|
| `practica-2-defecto-1` | RF-GP-04: `SolicitudPermiso.cs` línea 52, el motivo solo se comprueba que fue enviado (`motivo is null`) y no que tenga contenido | `Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSiguePendiente` (y `Rechazar_MotivoAusenteOVacio_SeRechazaYSiguePendiente`) | Rechazar con motivo `""` o `"   "` debe dar 400 y la solicitud seguir Pendiente |
| `practica-2-defecto-2` | RF-GP-06: `TransicionesSolicitudPermiso.cs` línea 17, se agrega Aprobada → Rechazada | `Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada` (y `Puede_SoloPermiteLasTransicionesDeLaTabla`) | Rechazar una solicitud Aprobada debe dar 409 y seguir Aprobada, sin motivo |
| `practica-2-defecto-3` | RF-GP-03: `PoliticasAcceso.cs` línea 83, se elimina la exigencia de Administrador para aprobar (queda solo «con sesión») | `AprobarSolicitud_UsuarioEstandar_SeRechaza` | Un usuario Estándar no puede ejecutar la operación de aprobar |
| `practica-2-defecto-4` | RF-GP-07: `ServicioPermisos.cs` línea 109, se invierte la condición (`!=` en vez de `==`): rechaza a los demás en lugar de al mismo usuario | `Aprobar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente` (y `Rechazar_AdministradorSuPropiaSolicitud_SeRechazaYSiguePendiente`) | El Administrador que resuelve su propia solicitud debe recibir 403 y la solicitud seguir Pendiente |
| `practica-2-defecto-5` | RF-GP-06: `TransicionesSolicitudPermiso.cs` línea 18, se agrega Aplicada → Rechazada | `Rechazar_SolicitudYaAplicada_SeRechazaYSigueAplicada` (y `Puede_SoloPermiteLasTransicionesDeLaTabla`) | Rechazar una solicitud ya Aplicada debe dar 409 y seguir Aplicada, sin motivo |
| `practica-2-defecto-6` | RF-GP-03: `SolicitudPermiso.cs` líneas 44 y 45, se elimina el registro de quién aprobó y cuándo | `Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando` (y `Aprobar_SolicitudPendiente_PasaAAprobadaYRegistraQuienYCuando`) | Al aprobar quedan registrados `AprobadaPorId` y `FechaAprobacion` |

Para comprobar un defecto:

```powershell
git checkout practica-2-defecto-1
git diff practica-2 practica-2-defecto-1 --stat
dotnet test
git checkout practica-2
```

## 7. Módulo de negocio (Práctica 3)

Inventario del negocio: productos, proveedores y **órdenes de compra** con su máquina de estados
(Borrador → Emitida → Recibida, y Cancelada). Código en `src/Negocio/` (dominio, estados y servicios), persistencia en
`src/Datos/RepositorioNegocioEf.cs` (tablas `Productos`, `Proveedores`, `OrdenesCompra`, `DetallesOrdenCompra`,
`MovimientosStock`; migración `OperacionesNegocio`, se aplica sola al arrancar) y API en `src/Api/Controllers/`.

- **RF-NEG-06:** todas las operaciones exigen sesión y declaran su rol en `PoliticasAcceso` (tabla de endpoints de la
  sección 4). Sin token: `401`. Con un rol que no corresponde: `403`.
- **Operación que depende de Gestión de permisos:** **recibir una orden** (`Ordenes.Recibir`). Un Estándar la pide con
  una solicitud, un Administrador la aprueba y solo entonces puede recibir órdenes. `ServicioOrdenesCompra.RecibirAsync`
  consulta a Gestión de permisos los permisos aprobados del usuario antes de ejecutar.
- **RD-07:** datos inválidos (código vacío, cantidad 0, proveedor inexistente, etc.) dan `400` controlado.
- **RD-09:** productos, órdenes, stock y movimientos quedan en SQL Server y sobreviven a reiniciar la API.

### 7.1 Preparación

Usa los tokens `$hAdm` (Administrador) y `$hEst` (Estándar activado) de la sección 6.1, y `$base = "http://localhost:5001"`.
Para ver el código de un rechazo en PowerShell: `try { ... } catch { $_.Exception.Response.StatusCode.value__; $_.ErrorDetails.Message }`.

```powershell
function Post($ruta, $cuerpo, $h) { Invoke-RestMethod -Method Post "$base$ruta" -Headers $h -ContentType "application/json" -Body ($cuerpo | ConvertTo-Json) }
```

### 7.2 Las funcionalidades y cómo probar cada una por separado (RF-NEG-02)

| # | Funcionalidad | Cómo provocarla | Resultado esperado |
|---|---|---|---|
| 1 | **Productos** | Admin: `$prod = Post "/api/productos" @{ codigo="P-001"; nombre="Arroz 25 lb"; precioCosto=900 } $hAdm`. Luego `Invoke-RestMethod "$base/api/productos" -Headers $hEst` | `201` con `stock: 0`; el listado lo muestra. Repetir el código: `409`. Crear como Estándar: `403`. Sin token: `401` |
| 2 | **Proveedores** | Admin: `$prov = Post "/api/proveedores" @{ nombre="Distribuidora Caribe"; correo="ventas@caribe.com" } $hAdm`. Luego `GET /api/proveedores` | `201`; el listado lo muestra. Nombre vacío: `400`. Crear como Estándar: `403` |
| 3 | **Órdenes en Borrador y sus detalles** | Estándar: `$orden = Post "/api/ordenes-compra" @{ proveedorId=$prov.id } $hEst` y `Post "/api/ordenes-compra/$($orden.id)/detalles" @{ productoId=$prod.id; cantidad=5; costoUnitario=850 } $hEst` | `201`, `estado: Borrador`; con el detalle, `total: 4250`. Cantidad 0: `400`. Proveedor inexistente: `400`. Otro Estándar agregando detalle o viendo la orden: `403` |
| 4 | **Emitir** (Borrador → Emitida) | Admin: `Post "/api/ordenes-compra/$($orden.id)/emitir" @{} $hAdm` | `200`, `estado: Emitida` con `fechaEmision`. Una orden sin detalles: `400` y sigue Borrador. Como Estándar: `403` |
| 5 | **Recibir** (Emitida → Recibida), **con Gestión de permisos** | 1) Estándar: `Post ".../recibir" @{} $hEst` → `403`. 2) Estándar: `Post "/api/solicitudes-permiso" @{ operacion="Ordenes.Recibir"; justificacion="Recibo la mercancía" } $hEst` (anota el `id`). 3) Repite el paso 1 → sigue `403` (pendiente). 4) Admin: `Post "/api/solicitudes-permiso/{id}/aprobar" @{} $hAdm`. 5) Estándar, mismo token: `Post ".../recibir" @{} $hEst` | Paso 5: `200`, `estado: Recibida` con `recibidaPorUsuarioId` y `fechaRecepcion`. `GET /api/productos` muestra `stock: 5`. Un Administrador puede recibir sin solicitud |
| 6 | **Cancelar** | Admin, sobre otra orden en Borrador: `Post ".../cancelar" @{} $hAdm`. Sobre una Emitida: con `@{}` → `400`; con `@{ motivo="Sin existencia" }` → `200` | `estado: Cancelada`; desde Emitida guarda `motivoCancelacion`; el stock no cambia |
| 7 | **Movimientos de stock** | `Invoke-RestMethod "$base/api/productos/$($prod.id)/movimientos" -Headers $hEst` | Tras recibir la orden: un movimiento `Entrada` de 5 unidades con su `ordenCompraId`. Producto inexistente: `404` |

### 7.3 Transición prohibida y estado terminal (RF-NEG-04, RF-NEG-05)

| Qué | Cómo provocarlo | Resultado esperado |
|---|---|---|
| Prohibida: Borrador → Recibida | Admin: `Post ".../recibir" @{} $hAdm` sobre una orden en **Borrador** | `409` "Transición no permitida: la orden está Borrador y no puede pasar a Recibida." y sigue Borrador |
| Prohibida: Recibida → Cancelada | Admin: `Post ".../cancelar" @{ motivo="x" } $hAdm` sobre la orden **Recibida** | `409` y sigue Recibida |
| Terminal: Cancelada | Admin: `Post ".../emitir" @{} $hAdm` o `.../cancelar` sobre una orden **Cancelada** | `409`; de Cancelada no sale ninguna transición |
| RD-09 | Detener la API (Ctrl+C), arrancarla otra vez y `GET /api/ordenes-compra/{id}` y `GET /api/productos` | Las órdenes conservan su estado y el stock sigue igual |


## 8. Máquina de estados del negocio

- Estados (RF-NEG-03): **`src/Negocio/Estados/EstadoOrdenCompra.cs`**. Son 4: Borrador, Emitida, Recibida y Cancelada.
- Transiciones (RD-04): **`src/Negocio/Estados/TransicionesOrdenCompra.cs`**, métodos `Puede` y `Validar`, y la lista `ProhibidasExplicitas` (Borrador → Recibida, Recibida → Cancelada, Recibida → Emitida, Cancelada → Emitida). Terminales: Recibida y Cancelada.
- Es independiente de la máquina de Gestión de permisos (RF-NEG-09).
- Tablas de permitidas y prohibidas (desde, hacia, quién, condición) y la prueba de cada fila: **[`docs/maquina-de-estados.md`](docs/maquina-de-estados.md)**.

## 9. Manejador de documentos (Práctica 4)

Pieza 3 del Core: `src/Core/Documentos/` (`Documento`, `ServicioDocumentos`, `AlmacenArchivosEnDisco`) y
`src/Core/Datos/RepositorioDocumentosEf.cs`. API: `src/Api/Controllers/DocumentosController.cs`. Migración
`ManejadorDocumentos` (tabla `Documentos`, se aplica sola al arrancar).

- **Todas** las operaciones exigen sesión, declarada en `PoliticasAcceso` y verificada en el servidor (RD-06): sin
  token, `401`, aunque se invoque el endpoint directamente.
- El archivo se guarda en `DOCUMENTOS_RUTA` con un nombre generado (no con el que envió el usuario). En Docker esa
  carpeta es el **volumen con nombre `documentos`**, fuera de la imagen (RF-DOC-05). La base solo guarda el registro.
- La eliminación es **lógica** (RF-DOC-04): el registro queda con `Eliminado = 1`, `FechaEliminacion` y
  `EliminadoPorUsuarioId`, y el archivo no se borra.
- Tamaño máximo: `DOCUMENTOS_MAX_MB` (10 MB por defecto).

En PowerShell 5.1 se sube con `curl.exe` (viene con Windows). Con `$base`, `$adm` y `$est` de la sección 6.1, y `$est2`
el token de **otro** usuario Estándar:

```powershell
"Factura 001 - Distribuidora Caribe" | Out-File -Encoding utf8 factura.txt
curl.exe -s -w "`n-> %{http_code}`n" -X POST "$base/api/documentos" -H "Authorization: Bearer $est" -F "archivo=@factura.txt"
Invoke-RestMethod "$base/api/documentos" -Headers @{ Authorization = "Bearer $est" }
curl.exe -s -w "`n-> %{http_code}`n" "$base/api/documentos/1/descargar" -H "Authorization: Bearer $est"
curl.exe -s -w "`n-> %{http_code}`n" -X DELETE "$base/api/documentos/1" -H "Authorization: Bearer $est"
```

En Swagger, `POST /api/documentos` muestra un botón para elegir el archivo (campo `archivo`).

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RF-DOC-01 | Estándar: `POST /api/documentos` con `-F "archivo=@factura.txt"`. Luego `GET /api/documentos/{id}` | `201` con `id`, `nombre`, `tipo`, `tamanoBytes`, `fechaSubida` y `subidoPorUsuarioId`. La consulta devuelve los mismos datos |
| RD-07 | `POST /api/documentos` sin archivo (`-F "otro=x"`) o con un archivo vacío (`New-Item vacio.txt` y `-F "archivo=@vacio.txt"`) | `400` "Debes adjuntar un archivo en el campo «archivo»." / "El archivo está vacío." Nada se registra |
| RD-06 | Cualquier endpoint de documentos sin `Authorization` | `401` |
| RF-DOC-02 | El otro Estándar sube un documento. Cada Estándar hace `GET /api/documentos`. Luego el Administrador | Cada Estándar ve **solo los suyos**. El Administrador ve los de todos. Ninguno ve los eliminados |
| RF-DOC-03 | Dueño: `GET /api/documentos/{id}/descargar`. Administrador: lo mismo | `200` con el archivo (mismo contenido y nombre) |
| RF-DOC-03 | **Otro** Estándar: `GET /api/documentos/{id}/descargar` (y `GET /api/documentos/{id}`) | `403` "Solo quien subió el documento o un Administrador puede descargarlo." |
| RF-DOC-04 | **Otro** Estándar: `DELETE /api/documentos/{id}` | `403`, y el documento sigue en el listado del dueño |
| RF-DOC-04 | Dueño (o Administrador): `DELETE /api/documentos/{id}`. Luego `GET /api/documentos` y `GET /api/documentos/{id}/descargar` | `200`. Ya no aparece en el listado y descargarlo da `404`. En la base el registro sigue: `docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P StockCore2026Sql -C -d Inventario -Q "SELECT Id, NombreOriginal, Eliminado, FechaEliminacion FROM Documentos"` muestra `Eliminado = 1` |
| RF-DOC-05 | Sección **Contenedor** → «Los datos sobreviven»: `docker compose down` (sin `-v`) y `docker compose up` | El documento sigue en el listado y se descarga; el usuario inicia sesión |

La auditoría de subidas y eliminaciones (RF-DOC-06) llega con la Pieza 6; no forma parte de esta entrega.

## 10. Flujo de trabajo

Cada funcionalidad se hizo en su propia rama y se fusionó a `main` por pull request con la plantilla
de `.github/PULL_REQUEST_TEMPLATE.md` (Qué cambia, Por qué, Cómo probarlo, Qué NO incluye):

| # | Rama | Requisitos |
|---|---|---|
| 0 | `chore/estructura-base` | RD-01, RD-03, RD-08, RD-09, RD-11 |
| 1 | `feat/registro-activacion` | RF-CA-01, 02, 14, 15, 16, 17 + RF-NOT-08, 09, 12, 13 |
| 2 | `feat/sesion` | RF-CA-03, 07, 18, 19 |
| 3 | `feat/administracion-usuarios` | RF-CA-04, 05, 06, 08, 20, 21 + RD-06 |
| 4 | `feat/recuperacion-password` | RF-CA-09 a 13, 22 |
| 5 | `feat/maquina-estados-negocio` | RF-NEG-01, 03, 04, 05, RD-04 |
| 6 | `docs/readme-final` | README, pruebas, RD-12 |
| 7 | `feat/gestion-permisos` | RF-GP-01 a 07, RD-04, RD-06, RD-07, RD-09 |
| 8 | `test/gestion-permisos` | Suite de pruebas de Gestión de permisos (RD-12) |
| 9 | `docs/asignacion-2` | README de la pieza, sección Pruebas y bitácora |
| 10 | `test/suite-completa-permisos` | Suite ampliada: RF-GP-01 a 07 identificados, autorización, 16 pares de transiciones, CI |
| 11 | `docs/practica-2` | README: pruebas por requisito y sección Defectos |
| 12 | `feat/negocio-ordenes` | Módulo de negocio: productos, proveedores, órdenes de compra y su máquina de estados (RF-NEG-02 a 06, 09) |
| 13 | `test/negocio-ordenes` | Suite de la máquina de estados, funcionalidades e integración con Gestión de permisos |
| 14 | `docs/practica-3` | README del negocio, `docs/maquina-de-estados.md` y sección Defectos de la Práctica 3 |
| 15 | `feat/manejador-documentos` | Manejador de documentos del Core (RF-DOC-01 a 05, RD-06, RD-07) |
| 16 | `feat/api-externa-tasa-cambio` | Cotización de órdenes con ExchangeRate-API, clave en `EXCHANGE_API_KEY` (RD-07, RD-08, RD-10) |
| 17 | `feat/contenedor` | `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example` y correos en modo consola |
| 18 | `test/practica-4` | Pruebas del Manejador de documentos y de la API externa |
| 19 | `docs/practica-4` | Secciones Contenedor, API externa y Manejador de documentos |

Entregas: etiquetas **`practica-1`** (Práctica 1), **`asignacion-2`** (Asignación 2) y **`practica-2`** con
`practica-2-defecto-1` a `practica-2-defecto-6` (Práctica 2), y **`practica-3`** con
`practica-3-defecto-1` a `practica-3-defecto-4` (Práctica 3), y **`practica-4`** (Práctica 4).
Bitácoras con el agente: `docs/bitacora-asignacion-1.md` y `docs/bitacora-asignacion-2.md`.
