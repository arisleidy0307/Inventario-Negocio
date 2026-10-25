# Stock Core: Inventario del negocio

Programación III (TDS-007), ITLA 2026-C-3. **Práctica 1: Control de acceso completo.** · **Asignación 2: Gestión de permisos y su primera suite de pruebas.** · **Práctica 2: una suite que detecta defectos.**

API en ASP.NET Core 8 + EF Core + SQL Server. Tiene registro con activación por correo, sesión con
credencial opaca, roles, administración de usuarios, recuperación de contraseña, una cola de correos
con un enviador independiente y la estructura de la máquina de estados del módulo de negocio
(`OrdenCompra`).

---

## 1. Requisitos previos

| Herramienta | Versión |
|---|---|
| .NET SDK | 8.0 o superior. El proyecto apunta a `net8.0` con `RollForward=Major`, así que también corre con .NET 9 o 10. |
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
│  ├─ Datos/         CoreDbContext, RepositorioPermisosEf + configuraciones de las entidades del Core
│  └─ Comun/         IReloj (RD-11), ExcepcionControlada
├─ Negocio/          Producto, Proveedor, OrdenCompra, DetalleOrdenCompra, MovimientoStock + máquina de estados
├─ Datos/            AppDbContext (Core + Negocio) y Migraciones
├─ Api/              Controladores delgados (RD-02), autenticación, filtro de roles, manejo de errores
└─ EnviadorCorreos/  Consola independiente que envía la cola por SMTP
tests/Core.Tests/    Pruebas xUnit del Core sin levantar la API (RD-12); GestionPermisos/ para la pieza 2
docs/                maquina-de-estados.md, gestion-permisos.md, pruebas.http, bitácoras
scripts/             variables.ejemplo.ps1
```

## 3. Variables de entorno

Ningún valor sensible está en el repositorio (RD-10). Todo se lee de variables de entorno.

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

### Cómo definirlas en PowerShell

Hay una plantilla en `scripts/variables.ejemplo.ps1`:

```powershell
Copy-Item scripts\variables.ejemplo.ps1 scripts\variables.ps1   # scripts\variables.ps1 está en .gitignore
notepad scripts\variables.ps1                                    # rellena tus valores
. .\scripts\variables.ps1                                        # cárgalas (en CADA terminal que abras)
```

También se pueden definir una a una: `$env:SMTP_HOST = "smtp.gmail.com"`.

## 4. Ejecutar

```powershell
git clone https://github.com/arisleidy0307/Inventario-Negocio.git
cd Inventario-Negocio
git checkout practica-2

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

### 5.6 Persistencia y errores

| Criterio | Cómo provocarlo | Resultado esperado |
|---|---|---|
| RD-09 | Detener la API (Ctrl+C) y arrancarla de nuevo | Los usuarios siguen ahí (SQL Server) |
| RD-08 | Cualquier error inesperado (por ejemplo, detener LocalDB con `sqllocaldb stop MSSQLLocalDB` y llamar a un endpoint) | `500` con `ProblemDetails` genérico, sin traza, ruta ni SQL |

## 6. Gestión de permisos (Asignación 2)

Un usuario pide permiso para ejecutar una operación que su rol no le da. Un Administrador lo aprueba o
lo rechaza. Al aprobar, **el sistema aplica el permiso automáticamente** (RF-GP-05).

- Operaciones que se pueden pedir: las marcadas como *otorgables* en `src/Core/Seguridad/PoliticasAcceso.cs`.
  Hoy es **`Usuarios.Listar`** (`GET /api/usuarios`). La lista también sale en `GET /api/solicitudes-permiso/operaciones`.
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

Resultado esperado en `practica-2`: `total: 76; failed: 0`. No hace falta base de datos, variables de entorno ni
levantar la API (RD-12). Gestión de permisos se prueba con un repositorio en memoria y un reloj fijo
(`tests/Core.Tests/GestionPermisos/Dobles.cs`). Cada prueba crea los suyos, así que son independientes y pasan
en cualquier orden. Cada prueba tiene las fases `// Preparar`, `// Actuar` y `// Verificar`, su nombre dice qué
verifica y lleva el atributo `[Trait("Requisito", "RF-GP-0X")]` con el requisito que cubre.

La misma suite corre en GitHub Actions en cada push de rama o etiqueta (`.github/workflows/pruebas.yml`).

Para correr solo las pruebas de un requisito: `dotnet test --filter "Requisito=RF-GP-03"`.
Para correr una prueba: `dotnet test --filter "FullyQualifiedName~Aprobar_SolicitudYaAprobada"`.

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

## 7. Máquina de estados del negocio

- Estados (RF-NEG-03): **`src/Negocio/Estados/EstadoOrdenCompra.cs`**. Son 4: Borrador, Emitida, Recibida y Cancelada.
- Transiciones (RD-04): **`src/Negocio/Estados/TransicionesOrdenCompra.cs`**, método `Puede(desde, hacia)`. Prohibida explícita: Recibida → Cancelada. Terminales: Recibida y Cancelada.
- Tabla completa (desde, hacia, quién, condición): **[`docs/maquina-de-estados.md`](docs/maquina-de-estados.md)**.

## 8. Flujo de trabajo

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

Entregas: etiquetas **`practica-1`** (Práctica 1), **`asignacion-2`** (Asignación 2) y **`practica-2`** con
`practica-2-defecto-1` a `practica-2-defecto-6` (Práctica 2).
Bitácoras con el agente: `docs/bitacora-asignacion-1.md` y `docs/bitacora-asignacion-2.md`.
