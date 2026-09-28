# Bitacora de sesion con el agente - Asignación 1

## Tarea: crear el archivo `.gitignore` para C#

- Que le pedi?: Le dije que me generara el archivo `.gitignore` adecuado para proyectos de C# y .NET haciendo enfasis en que debe evitar que se subieran carpetas de compilación como `bin/` u `obj/`.

- Con que me respondio: Me dio el listado estandar de exclusiones para .NET y los comandos basicos para limpiar el historial si algo se filtraba.

- El error que encontre, como lo detecte y como lo corregi:
  - El error: Al principio, la IA me sugirió agregar una ruta muy específica que solo funcionaba en mi computadora local dentro del `.gitignore`.

  - Como lo detecté: Revisando el archivo línea por línea antes de guardarlo, me di cuenta de que si compartía el proyecto con mi compañero o lo abría en otra PC, esa ruta fija iba a dar problemas.

  - Como lo corregí: Le señale el error y le pedi que cambiara esa regla por un patron que fuera mas general y flexible para que pudiera funcionar para cualquier equipo de desarrollo y luego verifique con los comandos de Git que todo quedara limpio y correcto.