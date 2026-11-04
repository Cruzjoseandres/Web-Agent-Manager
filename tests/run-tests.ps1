$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$compiler = Join-Path $frameworkDirectory "csc.exe"
$wpfDirectory = Join-Path $frameworkDirectory "WPF"
$srcDir = Join-Path $root "src-cs"
$tests = Join-Path $PSScriptRoot "UiPresentationHelperTests.cs"
$testExecutable = Join-Path ([System.IO.Path]::GetTempPath()) ("WebAgentManager.UiTests." + [Guid]::NewGuid().ToString("N") + ".exe")

$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName

try {
    & $compiler /nologo /target:exe /codepage:65001 `
        /main:WebAgentManagerApp.Tests.UiPresentationHelperTests `
        /out:$testExecutable `
        /r:"$wpfDirectory\PresentationFramework.dll" `
        /r:"$wpfDirectory\PresentationCore.dll" `
        /r:"$wpfDirectory\WindowsBase.dll" `
        /r:"System.Xaml.dll" `
        /r:"System.Web.Extensions.dll" `
        $csFiles $tests

    if ($LASTEXITCODE -ne 0) {
        throw "La compilacion de pruebas fallo (codigo $LASTEXITCODE)."
    }

    & $testExecutable
    if ($LASTEXITCODE -ne 0) {
        throw "Las pruebas fallaron (codigo $LASTEXITCODE)."
    }
}
finally {
    if (Test-Path -LiteralPath $testExecutable) {
        Remove-Item -LiteralPath $testExecutable -Force
    }
}
