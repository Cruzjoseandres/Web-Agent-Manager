# TOTVS WebAgent Manager

Aplicación gráfica nativa para administrar y cambiar versiones de **TOTVS WebAgent** entre múltiples clientes y ambientes de Protheus de forma instantánea, visual y sin requerir permisos de Administrador.

---

## 🚀 Características Principales

- **Gestión Multi-Cliente y Multi-Ambiente**: Administra clientes (Producción, Desarrollo, QA) en un solo panel organizado.
- **Detección en Tiempo Real de Procesos de Windows**: Muestra el estado exacto de cada WebAgent (`PID`, `Puerto`, `En ejecución` o `Detenido`).
- **Soporte HTTP y HTTPS (SSL)**: Concatenación automática de URLs de WebApp y apertura directa en el navegador predeterminado.
- **Herencia Automática de IPs**: Al crear un nuevo ambiente para un cliente existente, reutiliza automáticamente la IP/Host y protocolo.
- **Enrutamiento Inteligente de Puertos**: Calcula y sugiere automáticamente el puerto del WebAgent local en base al puerto web.
- **Importación / Exportación de Configuraciones (TI)**: Comparte toda la configuración del equipo mediante archivos JSON con 1 clic.
- **Importador de Instaladores Oficiales**: Extrae y aísla versiones del WebAgent (.exe oficial de TOTVS) sin alterar el sistema operativo ni requerir privilegios de Administrador.
- **Parchado Binario Anti Auto-Update**: Bloquea las actualizaciones silenciosas que corrompen puertos y desconectan sesiones de Protheus.

---

## 🛠️ Instalación y Uso

### Opción A (Uso Directo)
Ejecuta directamente `WebAgentManager.exe`. La aplicación es portable e incluye todas sus dependencias.

### Opción B (Crear acceso directo en el Escritorio)
Ejecuta `Instalar.bat` para instalar la aplicación en `%LOCALAPPDATA%\Programs\WebAgentManager` y generar un acceso directo universal en tu Escritorio.

---

## 💻 Compilación desde Código Fuente

El proyecto está desarrollado en C# WPF usando exclusivamente el compilador de .NET Framework 4.x incluido en Windows (sin necesidad de Visual Studio):

```powershell
.\build.ps1
```

Para ejecutar las pruebas unitarias automatizadas:

```powershell
.\tests\run-tests.ps1
```
