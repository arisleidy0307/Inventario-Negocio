# Bitácora de sesión con el agente - Asignación 2

Agente: Claude (Cowork). Sesión del 20 de octubre de 2026. El agente trabajó en una copia del repositorio en la
nube (compilando con .NET 8) y me pasaba las ramas a mi PC. Yo ejecuté la API con LocalDB y la suite con
`dotnet test` en mi máquina. En cada tarea anoto qué le pedí, qué devolvió, en qué se equivocó y cómo se
detectó y corrigió.

## Tarea 1: implementar la pieza Gestión de permisos

- **Qué le pedí:** implementar en el Core las reglas RF-GP-01 a 07 del PDF. Debía reutilizar lo que ya tenía
  de la Práctica 1:
  - las exigencias de rol en `PoliticasAcceso`;
  - las transiciones en un solo punto, como en `TransicionesOrdenCompra`;
  - controladores delgados;
  - persistencia con EF.

  Eso incluía que, al aprobar, el permiso se aplicara solo y se pudiera comprobar (RF-GP-05).
- **Qué me devolvió:** la carpeta `src/Core/Permisos/` con `SolicitudPermiso`, `TransicionesSolicitudPermiso`
  (RD-04), `ServicioPermisos`, `IRepositorioPermisos` y su versión con EF. Además, la migración `GestionPermisos`
  y el controlador `api/solicitudes-permiso`.

  Para RF-GP-05 propuso que `Usuarios.Listar` fuera una operación *otorgable*. Al aprobar, el sistema crea el
  `PermisoUsuario` y la solicitud termina en `Aplicada`, todo en la misma operación. Commits `24fb66c` a `b382107`.

### Error 1: nombre de método que chocaba con una clase

- **Qué pasó:** en `SolicitudesPermisoController` llamó `Operaciones()` al método que lista las operaciones
  otorgables. Pero ya existe la clase de constantes `Operaciones`, así que los atributos
  `[RequiereOperacion(Operaciones.X)]` del mismo controlador quedaron apuntando al método.
- **Cómo se detectó:** `dotnet build` falló con
  `SolicitudesPermisoController.cs(23,24): error CS0119: 'SolicitudesPermisoController.Operaciones()' is a method,
  which is not valid in the given context`.
- **Cómo se corrigió:** se renombró el método a `Catalogo()` y compiló sin errores antes del commit `b382107`.

### Error 2: las fechas perdían la marca UTC al leerlas de la base de datos

- **Qué pasó:** el agente dio la pieza por terminada porque compilaba y las pruebas pasaban. Pero no había
  comprobado cómo salían las fechas guardadas en SQL Server.
- **Cómo se detectó:**
  1. Levanté la API con el código nuevo y en la consola vi `Applying migration '20261020195146_GestionPermisos'`.
  2. Con la API corriendo en mi PC, el agente recorrió los criterios del README.
  3. Al aprobar la solicitud 3, la respuesta trajo `"fechaAprobacion": "2026-10-20T20:02:46.1974249Z"`.
  4. Al consultarla con `GET /api/solicitudes-permiso/3` salió `"2026-10-20T20:02:46.1974249"`, sin la `Z`.
     La comparación de las dos dio `fecha igual: false`.

  Era el mismo instante, pero leído de la BD ya no indicaba que era UTC. Al calificar parecería que la fecha
  había cambiado, justo en el criterio de "aprobar dos veces no cambia nada".
- **Cómo se corrigió:**
  - Causa: `datetime2` no guarda el tipo de fecha y EF Core la devolvía como `Unspecified`.
  - Se agregó `ConvertidorFechaUtc`, registrado en `CoreDbContext.ConfigureConventions`, para que toda fecha
    leída vuelva como UTC (commit `fdafd25`).
  - Una migración de prueba salió vacía, lo que confirma que no cambia el esquema.
  - Reinicié la API y la solicitud 3 devolvió `"fechaAprobacion": "2026-10-20T20:02:46.1974249Z"`, igual que
    al aprobarla.

## Tarea 2: proponer y redactar las pruebas de transiciones

- **Qué le pedí:** proponer y redactar las pruebas de la sección 1.2 del PDF:
  - al menos una transición válida que verifique el dato registrado;
  - al menos una prohibida que verifique que el estado no cambia;
  - todas con preparar–actuar–verificar, independientes y con nombres que digan qué comprueban;
  - que se ejecuten con un solo comando, sin levantar la API.
- **Qué me devolvió:** `tests/Core.Tests/GestionPermisos/` con un repositorio en memoria y un reloj fijo
  (`Dobles.cs`). Cada prueba crea los suyos, así no comparten estado.
  - **Transición válida:** `Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando` y
    `Rechazar_ConMotivo_QuedaRechazadaYRegistraMotivoQuienYCuando`.
  - **Transición prohibida:** `Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia`,
    `Aprobar_SolicitudRechazada_SeRechazaYSigueRechazada` y `Aplicar_SolicitudRechazada_SeRechazaYElEstadoSigueRechazada`.
  - También cubren RF-GP-01, 02, 05 y 07, y la tabla completa de transiciones.

  Commits `a8881c8` a `2c1a9fe`.

### Error 3: un caso de prueba con `null` que no llegaba como se esperaba

- **Qué pasó:** en `Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSiguePendiente` usó `[InlineData(null)]`.
  Con un solo `null`, C# entrega `null` como el arreglo completo de datos del atributo, no como un argumento.
- **Cómo se detectó:**
  - El compilador avisó `warning CS8625: Cannot convert null literal to non-nullable reference type` en
    `ServicioPermisosTests.cs(161,17)`.
  - Al ejecutar la suite: `FAIL ServicioPermisosTests.Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSiguePendiente:
    Object reference not set to an instance of an object` y `ok=55 fail=1`.
- **Cómo se corrigió:** se cambió a `[InlineData((string?)null)]`, que pasa un argumento nulo. Resultado:
  `ok=56 fail=0`.

### Comprobación de que las pruebas verifican lo que dicen

El agente introdujo dos errores a propósito en la pieza para ver si las pruebas fallaban, y luego los deshizo:

| Error introducido | Pruebas que fallaron |
|---|---|
| Cambiar el estado **antes** de validar la transición | `Aprobar_SolicitudYaAprobada_SeRechazaYElEstadoNoCambia` (`Equal Aplicada Aprobada`) y `Aprobar_SolicitudRechazada_SeRechazaYSigueRechazada` |
| Comentar la regla RF-GP-07 | `Aprobar_AdministradorSuPropiaSolicitud...` y `Rechazar_AdministradorSuPropiaSolicitud...` |

Al restaurar el código, todas volvieron a pasar.

## Tarea 3: pasar las ramas a mi repositorio local

- **Qué le pedí:** dejar las ramas nuevas en mi carpeta `inventario-negocio` para poder ejecutar la API.
- **Qué me devolvió:** importó `feat/gestion-permisos` y `test/gestion-permisos` y cambió a la rama de pruebas.

### Error 4: cambió de rama sin poder reemplazar archivos

- **Qué pasó:** ejecutó `git checkout test/gestion-permisos` sin tener permiso para borrar archivos en mi carpeta.
  Git respondió `error: unable to unlink old 'src/Core/Seguridad/PoliticasAcceso.cs': Operation not permitted`
  en 9 archivos. Quedó a medias: en la rama nueva, pero con esos 9 archivos con el contenido **viejo**.
- **Cómo se detectó:** el `git status` justo después mostraba `M src/Core/Seguridad/PoliticasAcceso.cs`,
  `M src/Core/ConfiguracionCore.cs`, etc., en una rama recién cambiada que debía estar limpia.
- **Cómo se corrigió:** se dio el permiso de borrado para la carpeta del proyecto, se limpiaron los `.lock` que
  Git había dejado y se ejecutó `git checkout -f test/gestion-permisos`. `git status` quedó limpio.

## Verificación en mi PC

- **Migración:** al arrancar la API vi en la consola cómo se aplicaba `GestionPermisos`.
- **Recorrido del README contra mi API** (lo ejecutó el agente con la API corriendo en mi PC):
  - Crear: `201` y `Pendiente`.
  - Estándar viendo las pendientes o la solicitud de otro: `403`.
  - Admin resolviendo su propia solicitud: `403`.
  - Rechazar sin motivo: `400`. Con motivo: `Rechazada`.
  - Aprobar una rechazada o aprobar dos veces: `409` y el estado no cambia.
  - Después de aprobar, el Estándar hace `GET /api/usuarios` con el mismo token: `200`.
- **Reinicio (RD-09):** reinicié la API y las solicitudes seguían igual: la 1 `Rechazada` con su motivo, la 2
  `Pendiente` y la 3 `Aplicada`. El permiso otorgado seguía funcionando.
- **Suite:** ejecuté `dotnet test` en la raíz del repositorio y obtuve
  `Test summary: total: 56; failed: 0; succeeded: 56; skipped: 0`.
