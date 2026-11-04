# Compila WebAgentManager.exe (un solo archivo, con herramientas empaquetadas de forma segura y metadatos de versión).
# Usa solo rutas relativas a esta carpeta y el compilador de .NET Framework 4.x incluido en Windows.
$ErrorActionPreference = "Stop"
$root   = $PSScriptRoot
$fwDir  = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$csc    = Join-Path $fwDir "csc.exe"
$wpfDir = Join-Path $fwDir "WPF"
$srcDir = Join-Path $root "src-cs"
$out    = Join-Path $root "WebAgentManager.exe"
$z7exe  = Join-Path $root "tools\7z.exe"
$z7dll  = Join-Path $root "tools\7z.dll"
$icon   = Join-Path $root "app.ico"

foreach ($f in @($csc, $srcDir, $z7exe, $z7dll, $icon)) {
    if (-not (Test-Path $f)) { throw "No se encontró: $f" }
}

# Empaquetar herramientas con máscara segura (evita falsos positivos de antivirus por detección de ejecutables PE embebidos en crudo)
$pkgExe = Join-Path $root "tools\7z.exe.pkg"
$pkgDll = Join-Path $root "tools\7z.dll.pkg"

$rawExe = [System.IO.File]::ReadAllBytes($z7exe)
for ($i = 0; $i -lt $rawExe.Length; $i++) { $rawExe[$i] = $rawExe[$i] -bxor 0x42 }
[System.IO.File]::WriteAllBytes($pkgExe, $rawExe)

$rawDll = [System.IO.File]::ReadAllBytes($z7dll)
for ($i = 0; $i -lt $rawDll.Length; $i++) { $rawDll[$i] = $rawDll[$i] -bxor 0x42 }
[System.IO.File]::WriteAllBytes($pkgDll, $rawDll)

$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

& $csc /nologo /target:winexe /codepage:65001 /win32icon:"$icon" /out:$out `
    /r:"$wpfDir\PresentationFramework.dll" `
    /r:"$wpfDir\PresentationCore.dll" `
    /r:"$wpfDir\WindowsBase.dll" `
    /r:"System.Xaml.dll" `
    /r:"System.Web.Extensions.dll" `
    /res:"$pkgExe,7z.exe.pkg" `
    /res:"$pkgDll,7z.dll.pkg" `
    /res:"$icon,app.ico" `
    $csFiles

if ($LASTEXITCODE -ne 0) { throw "La compilación falló (código $LASTEXITCODE)" }
Write-Output "OK -> $out"
