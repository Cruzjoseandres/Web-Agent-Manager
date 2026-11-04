$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$frameworkDirectory = [System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$compiler = Join-Path $frameworkDirectory "csc.exe"
$wpfDirectory = Join-Path $frameworkDirectory "WPF"
$source = Join-Path $root "src-cs\Program.cs"
$tests = Join-Path $PSScriptRoot "UiPresentationHelperTests.cs"
$testExecutable = Join-Path ([System.IO.Path]::GetTempPath()) ("WebAgentManager.UiTests." + [Guid]::NewGuid().ToString("N") + ".exe")

try {
    & $compiler /nologo /target:exe /codepage:65001 `
        /main:WebAgentManagerApp.Tests.UiPresentationHelperTests `
        /out:$testExecutable `
        /r:"$wpfDirectory\PresentationFramework.dll" `
        /r:"$wpfDirectory\PresentationCore.dll" `
        /r:"$wpfDirectory\WindowsBase.dll" `
        /r:"System.Xaml.dll" `
        /r:"System.Web.Extensions.dll" `
        $source $tests

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

