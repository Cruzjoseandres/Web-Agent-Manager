# Compila WebAgentManager.exe (un solo archivo, con 7z.exe/7z.dll embebidos).
# Usa solo rutas relativas a esta carpeta y el compilador de .NET Framework 4.x incluido en Windows.
$ErrorActionPreference = "Stop"
$root   = $PSScriptRoot
$fwDir  = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$csc    = Join-Path $fwDir "csc.exe"
$wpfDir = Join-Path $fwDir "WPF"
$src    = Join-Path $root "src-cs\Program.cs"
$out    = Join-Path $root "WebAgentManager.exe"
$z7exe  = Join-Path $root "tools\7z.exe"
$z7dll  = Join-Path $root "tools\7z.dll"

$icon   = Join-Path $root "app.ico"

foreach ($f in @($csc, $src, $z7exe, $z7dll, $icon)) {
    if (-not (Test-Path $f)) { throw "No se encontró: $f" }
}

& $csc /nologo /target:winexe /codepage:65001 /win32icon:"$icon" /out:$out `
    /r:"$wpfDir\PresentationFramework.dll" `
    /r:"$wpfDir\PresentationCore.dll" `
    /r:"$wpfDir\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.Web.Extensions.dll" `
    /res:"$z7exe,7z.exe" `
    /res:"$z7dll,7z.dll" `
    /res:"$icon,app.ico" `
    $src

if ($LASTEXITCODE -ne 0) { throw "La compilación falló (código $LASTEXITCODE)" }
Write-Output "OK -> $out"

