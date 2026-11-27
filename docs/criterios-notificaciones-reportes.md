# Criterios de aceptación: notificaciones, cola de correos y reportes

Escribí estos criterios **antes** de delegar el trabajo al agente (ver `AGENTS.md`). Con ellos acepto o
rechazo lo que entregue. Cada uno se puede comprobar sin preguntarme: dice el punto de partida, la acción y el
resultado observable, y cómo verlo.

Convenciones para comprobarlos:

- La API corre con `docker compose up --build` en `http://localhost:5001`.
- «Admin» es un usuario con rol Administrador y «Estándar» uno con rol Estándar, cada uno con su token de
  `POST /api/auth/login`.
- Los correos se ven en `docker compose logs enviador` (`CORREO_MODO=consola`) y en la consulta de la cola
  (criterio CA-COL-04).
- Sin token válido, **todos** los endpoints nuevos responden `401` (RD-06).

Diseño acordado para la API (los criterios se refieren a estas rutas):

| Ruta | Para qué | Exige |
|---|---|---|
| `GET /api/notificaciones` (`?soloNoLeidas=true` opcional) | Mis notificaciones y cuántas no he leído | Sesión |
| `POST /api/notificaciones/{id}/leer` | Marcar una notificación mía como leída | Sesión (solo el destinatario) |
| `GET /api/notificaciones/preferencias` y `PUT /api/notificaciones/preferencias` con `{ "correo": false }` | Ver o cambiar si recibo las notificaciones también por correo | Sesión |
| `GET /api/correos` (`?estado=Pendiente\|Enviado\|Fallido` opcional) | Consultar la cola de correos | Administrador |
| `GET /api/reportes/solicitudes-permiso?desde=AAAA-MM-DD&hasta=AAAA-MM-DD` | Reporte del Core | Sesión |
| `GET /api/reportes/compras-por-proveedor?desde=AAAA-MM-DD&hasta=AAAA-MM-DD` | Reporte del negocio | Sesión |

---

## 1. Notificaciones (pieza 4)

### CA-NOT-01: crear una solicitud notifica solo a los Administradores (RF-NOT-01)

- **Dado** que hay dos Administradores activos (el inicial y otro al que se le cambió el rol), un Administrador
  desactivado y dos usuarios Estándar A y B,
- **cuando** A crea una solicitud de permiso (`POST /api/solicitudes-permiso`),
- **entonces** cada Administrador **activo** tiene **exactamente una** notificación nueva, sin leer, de tipo
  `SolicitudCreada`, cuyo mensaje nombra la operación pedida y al solicitante. Ni A, ni B, ni el Administrador
  desactivado tienen notificación nueva.
- **Cómo se comprueba:** `GET /api/notificaciones` con el token de cada usuario antes y después de crear la
  solicitud. El Administrador desactivado se comprueba reactivándolo después (sigue sin esa notificación).

### CA-NOT-02: resolver una solicitud notifica al solicitante con el resultado y el motivo (RF-NOT-02)

- **Dado** que A tiene una solicitud pendiente,
- **cuando** un Administrador la **aprueba**,
- **entonces** A tiene una notificación nueva de tipo `SolicitudAprobada` que dice que fue **aprobada** y qué
  operación ya puede usar; nadie más recibe notificación de esa aprobación.
- **Y cuando** un Administrador **rechaza** otra solicitud de A con el motivo «No corresponde a tu puesto»,
- **entonces** A tiene una notificación de tipo `SolicitudRechazada` que dice que fue **rechazada** e incluye
  el texto exacto del motivo.
- **Cómo se comprueba:** `GET /api/notificaciones` de A después de cada resolución; las del Administrador que
  resolvió no cambian.
- Un intento rechazado (por ejemplo, aprobar una solicitud ya rechazada, `409`) **no** genera notificación.

### CA-NOT-03: un evento del negocio notifica (RF-NOT-03)

- **Dado** que el Estándar A creó una orden de compra con al menos un detalle,
- **cuando** un Administrador la **emite** (`POST /api/ordenes-compra/{id}/emitir`),
- **entonces** A tiene una notificación de tipo `OrdenEmitida` con el número de la orden y su total en DOP.
- **Y cuando** después la orden se **recibe** (`POST /api/ordenes-compra/{id}/recibir`), por un Administrador
  o por quien tenga el permiso `Ordenes.Recibir`,
- **entonces** A tiene una notificación de tipo `OrdenRecibida` con el número de la orden.
- Si quien emite o recibe es el mismo creador de la orden, no se le notifica su propia acción.
- **Cómo se comprueba:** los pasos de la sección «Notificaciones» del README, con `GET /api/notificaciones` de A.

### CA-NOT-04: cada usuario consulta solo las suyas, separando leídas de no leídas (RF-NOT-04)

- **Dado** que A y B tienen notificaciones,
- **cuando** A consulta `GET /api/notificaciones`,
- **entonces** solo aparecen notificaciones cuyo destinatario es A, ordenadas de la más nueva a la más vieja.
  Cada una trae `leida` (verdadero o falso) y la respuesta trae `noLeidas` con el número de no leídas de A.
- **Y cuando** consulta con `?soloNoLeidas=true`, solo aparecen las no leídas.
- **Cómo se comprueba:** comparar los `id` que ve A con los que ve B: no hay ninguno en común.

### CA-NOT-05: marcar como leída solo las propias (RF-NOT-05)

- **Dado** que A tiene una notificación sin leer con id N,
- **cuando** A hace `POST /api/notificaciones/N/leer`,
- **entonces** responde `200`, la notificación queda `leida: true` con `fechaLectura`, y `noLeidas` baja en 1.
  Marcarla otra vez responde `200` y no cambia nada.
- **Y cuando** B intenta `POST /api/notificaciones/N/leer`,
- **entonces** responde `403` y la notificación de A sigue como estaba.
- Una notificación que no existe responde `404`.

### CA-NOT-06: cada notificación llega por los dos canales (RF-NOT-06)

- **Dado** un usuario con el canal de correo activo (el valor por defecto),
- **cuando** se genera cualquier notificación de los criterios CA-NOT-01 a 03,
- **entonces** aparece en su bandeja (`GET /api/notificaciones`) **y** se encola un correo para su dirección,
  cuyo asunto es el título de la notificación.
- **Cómo se comprueba:** `GET /api/correos` como Administrador muestra el correo nuevo para esa dirección, y
  `docker compose logs enviador` lo muestra enviado.

### CA-NOT-07: desactivar el canal de correo sin perder el interno (RF-NOT-07)

- **Dado** que A hace `PUT /api/notificaciones/preferencias` con `{ "correo": false }`
  (`GET /api/notificaciones/preferencias` responde `{ "correo": false }`),
- **cuando** se genera una notificación para A (por ejemplo, un Administrador rechaza su solicitud),
- **entonces** la notificación aparece en la bandeja de A y **no** se encola ningún correo para A: el total de
  `GET /api/correos` no cambia para su dirección.
- Volver a `{ "correo": true }` hace que la siguiente notificación sí encole su correo.
- La preferencia solo afecta las notificaciones; los correos de la cuenta (activación y recuperación de
  contraseña) siempre se envían.

---

## 2. Cola de correos

### CA-COL-01: encolar, no enviar dentro de la operación (RF-NOT-08)

- **Dado** que el servidor de correo no responde (enviador detenido con `docker compose stop enviador`, o un
  `SMTP_HOST` inexistente),
- **cuando** se registra un usuario o se crea una solicitud de permiso,
- **entonces** la operación responde igual que siempre (`201`) y el correo queda en la cola en `Pendiente`.

### CA-COL-02: el procesador independiente los envía (RF-NOT-09)

- **Dado** que hay correos `Pendiente`,
- **cuando** corre el enviador (servicio `enviador`, o una vez con
  `docker compose run --rm enviador dotnet /app/enviador/Inventario.EnviadorCorreos.dll`),
- **entonces** cada correo pasa a `Enviado` con `fechaEnvio`, sin repetir la operación que lo creó (no aparece
  un segundo usuario, solicitud ni notificación).

### CA-COL-03: reintentos limitados y estado fallido (RF-NOT-10)

- **Dado** que el envío falla siempre (`CORREO_MODO=smtp` con `SMTP_HOST=smtp.invalido.local`) y el máximo de
  intentos es **3** (`CORREO_MAX_INTENTOS`, declarado en el README),
- **cuando** el enviador procesa la cola varias veces,
- **entonces** después de cada fallo el correo sigue `Pendiente` con `intentos` 1, 2…, `fechaUltimoIntento` y
  `ultimoError`; al **tercer** fallo queda `Fallido` con `intentos: 3` y su `ultimoError`, y las siguientes
  pasadas ya no lo intentan (`intentos` no pasa de 3). El correo no se borra.

### CA-COL-04: el Administrador consulta la cola; el Estándar no (RF-NOT-11)

- **Cuando** un Administrador hace `GET /api/correos`,
- **entonces** ve cada correo con `id`, `destinatario`, `asunto`, `estado` (`Pendiente`, `Enviado` o
  `Fallido`), `intentos`, `fechaCreacion`, `fechaUltimoIntento`, `fechaEnvio` y `ultimoError`, del más nuevo al
  más viejo. Con `?estado=Fallido` solo ve los fallidos. No se muestra el cuerpo, porque lleva enlaces y códigos.
- **Y cuando** un Estándar hace la misma consulta, responde `403`.

### CA-COL-05: sin duplicados al correr el procesador dos veces (RF-NOT-12)

- **Dado** un correo `Pendiente`,
- **cuando** el procesador se ejecuta dos veces seguidas,
- **entonces** la primera informa `Enviados: 1` y la segunda `Enviados: 0`; el correo queda `Enviado` con
  `intentos: 1` y aparece una sola vez en `docker compose logs enviador`.

### CA-COL-06: credenciales SMTP fuera del repositorio (RF-NOT-13)

- `SMTP_HOST`, `SMTP_USER`, `SMTP_PASSWORD` y el resto se leen de variables de entorno (`.env`, que no se
  versiona). `.env.example` solo trae valores de ejemplo.
- **Cómo se comprueba:** `git log --all --oneline -- .env` no muestra commits, y en el historial
  (`git log --all -p`) no aparece ninguna contraseña SMTP real ni la clave de la API externa.

---

## 3. Reportes con agregación (pieza 5)

### CA-REP-01: reporte del Core, solicitudes por estado y por mes (RF-REP-01, RF-REP-03)

- **Dado** que en noviembre hay 3 solicitudes `Aplicada`, 2 `Rechazada` y 1 `Pendiente`,
- **cuando** un Administrador consulta `GET /api/reportes/solicitudes-permiso`,
- **entonces** recibe filas agrupadas por **mes** (`2026-11`) y **estado**, con `cantidad`: 3 filas con 3, 2 y
  1, y un `total` de 6. Es decir, menos filas que las 6 solicitudes que resume, y ningún dato de cada solicitud
  (ni id, ni solicitante).

### CA-REP-02: reporte del negocio, compras por proveedor (RF-REP-02, RF-REP-03)

- Pregunta que responde: **«¿cuánto le compré a cada proveedor en un período?»**.
- **Dado** que se recibieron 3 órdenes del proveedor X (con totales 4 250, 2 400 y 1 700 DOP) y 1 del
  proveedor Y,
- **cuando** se consulta `GET /api/reportes/compras-por-proveedor`,
- **entonces** hay **una fila por proveedor**, ordenadas del mayor monto al menor, con `ordenesRecibidas` (3 para
  X), `unidades` (suma de las cantidades recibidas), `montoTotal` (8 350 para X) y `montoPromedioPorOrden` (2 783.33
  para X), más los totales generales. Solo cuentan las órdenes **Recibidas**: las que están en Borrador, Emitida o
  Cancelada no suman.
- La suma se hace en la consulta a la base (`GROUP BY`), no trayendo todas las órdenes al servidor.

### CA-REP-03: los reportes respetan el rol (RF-REP-04)

- **Dado** que A y B (Estándar) tienen solicitudes y órdenes recibidas,
- **cuando** A consulta cualquiera de los dos reportes,
- **entonces** los números solo cuentan **sus** solicitudes y **sus** órdenes (las que A creó); coinciden con lo
  que A ve en `GET /api/solicitudes-permiso/mias` y `GET /api/ordenes-compra`. El Administrador ve los de todos.
- **Cómo se comprueba:** el `total` de A más el de B es igual al `total` del Administrador cuando solo ellos dos
  tienen datos.

### CA-REP-04: filtro por rango de fechas (RF-REP-05)

- **Dado** los mismos datos,
- **cuando** se consulta un reporte con dos rangos distintos (por ejemplo, `desde` y `hasta` en el día de hoy,
  frente a un rango del año pasado),
- **entonces** los resultados son distintos: el de hoy incluye los datos de hoy y el del año pasado no tiene
  filas (`total: 0`). `desde` y `hasta` son inclusivos (todo el día de `hasta` cuenta), las fechas van en
  formato `AAAA-MM-DD` y son opcionales (sin ellas el rango es todo el historial).
- La fecha que se filtra es la de la solicitud (`fechaSolicitud`) en el reporte del Core y la de recepción
  (`fechaRecepcion`) en el del negocio.
- `desde` posterior a `hasta`, o una fecha con otro formato, responde `400` con un mensaje claro.

### CA-REP-05: sesión obligatoria (RD-06)

- Los dos reportes responden `401` sin token.

---

## 4. Lo que no debe romperse

- `dotnet test` en la raíz pasa completo, con las pruebas anteriores sin tocar y pruebas nuevas para cada
  criterio de este archivo.
- Los seis frentes de la revisión acumulada siguen funcionando con `docker compose up --build`:
  - registro con activación por correo, con el correo saliendo por la cola;
  - inicio y cierre de sesión, y el `403` a un Estándar que invoca una operación de Administrador;
  - recuperación de contraseña;
  - las solicitudes de permiso con sus transiciones;
  - la transición prohibida del negocio;
  - un documento que sobrevive a `docker compose down` sin `-v`.
