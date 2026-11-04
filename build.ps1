# Compila WebAgentManager.exe (aplicación WPF ligera y limpia con metadatos oficiales y sin payloads de alta entropía).
# Usa solo rutas relativas a esta carpeta y el compilador de .NET Framework 4.x incluido en Windows.
$ErrorActionPreference = "Stop"
$root   = $PSScriptRoot
$fwDir  = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$csc    = Join-Path $fwDir "csc.exe"
$wpfDir = Join-Path $fwDir "WPF"
$srcDir = Join-Path $root "src-cs"
$out    = Join-Path $root "WebAgentManager.exe"
$icon   = Join-Path $root "app.ico"

# Limpieza de paquetes temporales si existieran
Get-ChildItem -Path (Join-Path $root "tools") -Filter "*.pkg" -ErrorAction SilentlyContinue | Remove-Item -Force

foreach ($f in @($csc, $srcDir, $icon)) {
    if (-not (Test-Path $f)) { throw "No se encontró: $f" }
}

$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

& $csc /nologo /target:winexe /codepage:65001 /win32icon:"$icon" /out:$out `
    /r:"$wpfDir\PresentationFramework.dll" `
    /r:"$wpfDir\PresentationCore.dll" `
    /r:"$wpfDir\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.Web.Extensions.dll" `
    /res:"$icon,app.ico" `
    $csFiles

if ($LASTEXITCODE -ne 0) { throw "La compilación falló (código $LASTEXITCODE)" }
Write-Output "OK -> $out"
