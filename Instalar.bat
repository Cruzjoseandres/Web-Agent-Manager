@echo off
title Instalador - TOTVS WebAgent Manager
color 0A
cls
echo ============================================================================
echo               INSTALADOR DE TOTVS WEBAGENT MANAGER (.EXE)
echo ============================================================================
echo.
echo  Instalando en tu equipo (No requiere permisos de Administrador)...
echo.

set "SOURCE_DIR=%~dp0"
set "DEST_DIR=%LOCALAPPDATA%\Programs\WebAgentManager"

REM 1. Crear directorios
if not exist "%DEST_DIR%" mkdir "%DEST_DIR%"
if not exist "%DEST_DIR%\config" mkdir "%DEST_DIR%\config"
if not exist "%DEST_DIR%\tools" mkdir "%DEST_DIR%\tools"
if not exist "%DEST_DIR%\versions" mkdir "%DEST_DIR%\versions"

REM 2. Copiar ejecutable principal y herramientas
echo  [1/3] Copiando aplicacion y herramientas...
copy /Y "%SOURCE_DIR%WebAgentManager.exe" "%DEST_DIR%\WebAgentManager.exe" >nul
xcopy "%SOURCE_DIR%tools\*" "%DEST_DIR%\tools\" /E /Y /Q >nul

if not exist "%DEST_DIR%\config\clientes.json" (
    copy /Y "%SOURCE_DIR%config\clientes.json" "%DEST_DIR%\config\clientes.json" >nul
)

REM 3. Copiar versiones de WebAgent
echo  [2/3] Copiando versiones portables de WebAgent...
xcopy "%SOURCE_DIR%versions\*" "%DEST_DIR%\versions\" /E /Y /Q >nul

REM 4. Crear acceso directo universal (soporta OneDrive y Español)
echo  [3/3] Creando acceso directo en tu Escritorio...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$desktop = [Environment]::GetFolderPath('Desktop'); " ^
  "if (-not $desktop -or -not (Test-Path $desktop)) { " ^
  "  $candidates = @(\"$env:USERPROFILE\OneDrive\Escritorio\", \"$env:USERPROFILE\OneDrive\Desktop\", \"$env:USERPROFILE\Desktop\", \"$env:USERPROFILE\Escritorio\"); " ^
  "  foreach ($c in $candidates) { if ($c -and (Test-Path $c)) { $desktop = $c; break } } " ^
  "} " ^
  "if (-not $desktop -or -not (Test-Path $desktop)) { $desktop = \"$env:USERPROFILE\Desktop\"; New-Item -ItemType Directory -Path $desktop -Force | Out-Null } " ^
  "$ws = New-Object -ComObject WScript.Shell; " ^
  "$s = $ws.CreateShortcut((Join-Path $desktop 'WebAgent Manager.lnk')); " ^
  "$s.TargetPath = \"$env:LOCALAPPDATA\Programs\WebAgentManager\WebAgentManager.exe\"; " ^
  "$s.WorkingDirectory = \"$env:LOCALAPPDATA\Programs\WebAgentManager\"; " ^
  "$s.Description = 'Gestor visual de clientes WebAgent TOTVS Protheus'; " ^
  "$s.IconLocation = \"$env:LOCALAPPDATA\Programs\WebAgentManager\WebAgentManager.exe,0\"; " ^
  "$s.Save()"

echo.
echo ============================================================================
echo   INSTALACION COMPLETADA CON EXITO!
echo   Ya tienes en tu Escritorio el acceso directo: "WebAgent Manager"
echo ============================================================================
echo.
pause
