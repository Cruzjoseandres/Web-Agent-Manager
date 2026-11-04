#Requires -Version 5.1

Write-Host "============================================================================" -ForegroundColor Green
Write-Host "              INSTALADOR DE TOTVS WEBAGENT MANAGER" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Green
Write-Host ""
Write-Host " Instalando en tu equipo de forma local (No requiere permisos de Administrador)..." -ForegroundColor White
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Test-Path "$scriptDir\..\versions") {
    $sourceRoot = (Get-Item "$scriptDir\..").FullName
} else {
    $sourceRoot = $scriptDir
}

$destDir = "$env:LOCALAPPDATA\Programs\WebAgentManager"

# 1. Crear directorios de destino
if (-not (Test-Path "$destDir\config")) { New-Item -ItemType Directory -Path "$destDir\config" -Force | Out-Null }
if (-not (Test-Path "$destDir\src")) { New-Item -ItemType Directory -Path "$destDir\src" -Force | Out-Null }
if (-not (Test-Path "$destDir\versions")) { New-Item -ItemType Directory -Path "$destDir\versions" -Force | Out-Null }

# 2. Copiar componentes
Write-Host " [1/3] Copiando componentes y configuraciones..." -ForegroundColor Cyan
Copy-Item "$sourceRoot\src\*" -Destination "$destDir\src\" -Recurse -Force
Copy-Item "$sourceRoot\Iniciar.vbs" -Destination "$destDir\" -Force
if (Test-Path "$sourceRoot\Abrir WebAgent Manager.bat") {
    Copy-Item "$sourceRoot\Abrir WebAgent Manager.bat" -Destination "$destDir\" -Force
}

if (-not (Test-Path "$destDir\config\clientes.json")) {
    Copy-Item "$sourceRoot\config\clientes.json" -Destination "$destDir\config\clientes.json" -Force
}

# 3. Copiar versiones
Write-Host " [2/3] Copiando versiones portables de WebAgent..." -ForegroundColor Cyan
Copy-Item "$sourceRoot\versions\*" -Destination "$destDir\versions\" -Recurse -Force

# 4. Obtener la ruta REAL del Escritorio (compatible con OneDrive y Windows en Español)
Write-Host " [3/3] Creando acceso directo en tu Escritorio..." -ForegroundColor Cyan

$desktop = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)

if (-not $desktop -or -not (Test-Path $desktop)) {
    $candidates = @(
        "$env:USERPROFILE\OneDrive\Escritorio",
        "$env:USERPROFILE\OneDrive\Desktop",
        "$env:USERPROFILE\Desktop",
        "$env:USERPROFILE\Escritorio",
        [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::CommonDesktopDirectory)
    )
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) {
            $desktop = $c
            break
        }
    }
}

if (-not $desktop -or -not (Test-Path $desktop)) {
    $desktop = "$env:USERPROFILE\Desktop"
    New-Item -ItemType Directory -Path $desktop -Force | Out-Null
}

try {
    $ws = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path $desktop "WebAgent Manager.lnk"
    $s = $ws.CreateShortcut($shortcutPath)
    $s.TargetPath = "powershell.exe"
    $s.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -STA -File `"$destDir\src\WebAgentManager.ps1`""
    $s.WorkingDirectory = $destDir
    $s.Description = "Gestor visual de clientes WebAgent TOTVS Protheus"
    
    $icon = "$destDir\versions\web-agent-1.1.1-x64\web-agent.exe"
    if (Test-Path $icon) {
        $s.IconLocation = "$icon,0"
    } else {
        $s.IconLocation = "shell32.dll,276"
    }
    $s.Save()
    Write-Host " [OK] Acceso directo creado en: $shortcutPath" -ForegroundColor Green
} catch {
    Write-Host " [AVISO] Error al guardar acceso directo: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Green
Write-Host "  INSTALACION COMPLETADA CON EXITO!" -ForegroundColor Green
Write-Host "  Ya tienes en tu Escritorio el acceso directo: `"WebAgent Manager`"" -ForegroundColor White
Write-Host "============================================================================" -ForegroundColor Green
Write-Host ""
