# Bitácora de sesión con el agente - Asignación 1

Tareas de esta asignación que delegué al agente: el .gitignore de C# de mi repositorio y el
README de ejecución del proyecto de mi pareja, además de la preparación de la rama para ese
README. En cada una anoto qué pedí, qué devolvió y qué error cometió el agente.

## Tarea 1: .gitignore para C# en mi repositorio
- **Qué le pedí:** el `.gitignore` adecuado para proyectos C# y .NET, evitando que se subieran
  carpetas de compilación como `bin/` u `obj/`.
- **Qué me devolvió:** el listado estándar de exclusiones para .NET y comandos básicos para
  limpiar el historial si algo se filtraba.
- **Error del agente:** me sugirió una regla con una ruta específica de mi computadora.
- **Cómo lo detecté:** revisando el archivo línea por línea antes de guardarlo; esa ruta daría
  problemas al abrir el proyecto en otra PC.
- **Cómo lo corregí:** le pedí un patrón general y verifiqué con comandos de Git que todo
  quedara limpio. Quedó en el commit `d34f419`, fusionado por el PR #1 de mi repositorio.

## Tarea 2: README de ejecución del proyecto de mi pareja (PR #4)
- **Qué le pedí:** redactar el README con los comandos para compilar y ejecutar el proyecto.
- **Qué me devolvió:** una guía basada en `ant jar`, asumiendo que Ant funcionaría directo.
- **Error del agente:** ese comando no sirve para este proyecto. Al probar en mi PC, PowerShell
  no reconocía `java` ni `ant`. Además, el agente lo probó en su propio entorno y `ant jar`
  falló con `package org.netbeans.lib.awtextra does not exist`: el proyecto usa la librería
  Absolute Layout (`libs.absolutelayout` en `project.properties`), que viene con NetBeans.
- **Cómo lo detecté:** por los errores de PowerShell en mi máquina y por la prueba del agente.
- **Cómo lo corregí:** el README se reenfocó en NetBeans. Lo verifiqué yo: abrí
  `CalculadoraBasica` con File > Open Project, usé Run sin configurar nada y la ventana abrió;
  8 + 2 con Sumar dio 10. Quedó en el commit `93eebde`, PR #4.

## Tarea 3: preparar la rama para el README
- **Qué le pedí:** guiarme paso a paso para probar el proyecto y armar el PR del README.
- **Qué me devolvió:** el comando para crear la rama desde `origin/main`.
- **Error del agente:** `origin/main` es el `main` de mi fork, que estaba desactualizado
  (`af19400`, solo `.gitignore` y README). El código estaba en el `main` de mi pareja (`d136b68`).
- **Cómo lo detecté:** al abrir el proyecto en NetBeans, la carpeta aparecía vacía. Con `dir`
  vi que `calculadora-fork` solo tenía `.gitignore` y `README.md`; faltaba `CalculadoraBasica`.
  El agente comparó los dos remotos con `git ls-remote` y encontró la causa.
- **Cómo lo corregí:** `git remote add upstream`, `git fetch upstream` y
  `git checkout --no-track -B docs/readme-ejecucion upstream/main`.