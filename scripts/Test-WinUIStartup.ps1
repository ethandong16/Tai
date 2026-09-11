param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory
)

$ErrorActionPreference = 'Stop'
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
foreach ($file in @('Tai.WinUI.exe', 'resources.pri')) {
    $item = Get-Item -LiteralPath (Join-Path $publishPath $file)
    if ($item.Length -eq 0) { throw "Empty publish file: $file" }
}

# Run against a fresh copy so test data never touches an existing user database.
$testPath = Join-Path ([IO.Path]::GetTempPath()) ('Tai-WinUI-smoke-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $testPath | Out-Null
Get-ChildItem -LiteralPath $publishPath | Where-Object {
    $_.Name -notin @('Data', 'Log', 'startup-smoke.ok')
} | Copy-Item -Destination $testPath -Recurse

$process = Start-Process -FilePath (Join-Path $testPath 'Tai.WinUI.exe') `
    -ArgumentList '--smoke-test' -WorkingDirectory $testPath -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(45000)) {
    $process.Kill()
    throw "Startup test timed out. Diagnostics retained in $testPath"
}
$logPath = Join-Path $testPath 'Log/startup.log'
if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath }
if ($process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $testPath 'startup-smoke.ok')) -or
    (Test-Path -LiteralPath $logPath)) {
    throw "Startup test failed (exit $($process.ExitCode)). Diagnostics retained in $testPath"
}
Get-Content -LiteralPath (Join-Path $testPath 'startup-smoke.ok')
Write-Output "Verified publish resources and startup. Test copy: $testPath"
