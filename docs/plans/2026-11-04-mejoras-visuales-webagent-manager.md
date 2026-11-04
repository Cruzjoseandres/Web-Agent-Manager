# Mejoras visuales de WebAgent Manager — Plan de implementación

> **Para Codex:** ejecutar las tareas en orden, con pruebas antes de modificar el código productivo.

**Objetivo:** añadir icono de aplicación, enlaces de servidor, búsqueda y clientes plegables sin alterar el enrutamiento ni el control de procesos WebAgent.

**Arquitectura:** la aplicación seguirá siendo un ejecutable WPF de un solo archivo fuente. La lógica comprobable de presentación se aislará en un helper puro; `MainWindow` solo conectará esos resultados con controles WPF. El estado de plegado y búsqueda vivirá únicamente en memoria y no cambiará `clientes.json`.

**Tecnologías:** C#/.NET Framework 4.x, WPF programático, PowerShell y `csc.exe`.

---

### Tarea 1: Comportamientos puros de presentación

**Archivos:**

- Crear: `tests/UiPresentationHelperTests.cs`
- Crear: `tests/run-tests.ps1`
- Modificar: `src-cs/Program.cs`

1. Escribir pruebas para normalización segura de IP/URL, búsqueda por cliente/ambiente/IP y expansión durante búsquedas.
2. Ejecutar `powershell -ExecutionPolicy Bypass -File tests/run-tests.ps1` y confirmar que falla porque el helper aún no existe.
3. Implementar el helper mínimo en `Program.cs`.
4. Repetir la prueba y confirmar salida exitosa.

### Tarea 2: Búsqueda y plegado en WPF

**Archivos:**

- Modificar: `src-cs/Program.cs`

1. Incorporar una caja de búsqueda y acción para limpiarla.
2. Renderizar solo clientes y ambientes coincidentes sin mutar las colecciones originales.
3. Añadir un botón por cliente para mostrar u ocultar ambientes.
4. Conservar el `ScrollViewer` existente y el registro visual requerido por el monitor en vivo.

### Tarea 3: Enlaces de servidor

**Archivos:**

- Modificar: `src-cs/Program.cs`

1. Reemplazar el servidor de texto plano por un `Hyperlink` cuando la dirección sea válida.
2. Abrir la URL con `ProcessStartInfo.UseShellExecute = true` para usar el navegador predeterminado.
3. Mostrar un error controlado si Windows no puede abrir el enlace.

### Tarea 4: Icono de aplicación

**Archivos:**

- Modificar: `build.ps1`
- Modificar: `src-cs/Program.cs`
- Reutilizar: `app.ico`

1. Incrustar `app.ico` como icono Win32 y como recurso administrado.
2. Cargar el recurso en la propiedad `Window.Icon`.
3. Verificar que el recurso esté dentro del ejecutable generado.

### Tarea 5: Verificación integral

1. Ejecutar todas las pruebas del helper.
2. Ejecutar `build.ps1` y comprobar código de salida cero.
3. Validar por inspección que no cambiaron `SmartRouter`, `StartEnvironment`, `StopEnvironment`, persistencia ni registro.
4. Revisar de forma independiente el diff y documentar la cobertura requisito por requisito.

