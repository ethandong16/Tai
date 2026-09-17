param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts")
)

$ErrorActionPreference = "Stop"
$extensionDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\WebExtensions\Chrome"))
$manifestPath = Join-Path $extensionDirectory "manifest.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

$requiredFiles = @(
    "manifest.json",
    "service-worker.js",
    "icon48.png",
    "icon128.png",
    "icons\socket-active.png",
    "icons\socket-inactive.png",
    "LICENSE"
)

foreach ($relativePath in $requiredFiles) {
    $sourcePath = Join-Path $extensionDirectory $relativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Missing browser extension file: $relativePath"
    }
}

if ($manifest.manifest_version -ne 3) {
    throw "Chrome Web Store submissions must use Manifest V3."
}
if ($manifest.permissions -notcontains "tabs") {
    throw "The extension requires the tabs permission for its declared function."
}
if ($manifest.version -notmatch '^\d+(\.\d+){0,3}$') {
    throw "Invalid extension version: $($manifest.version)"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$archivePath = Join-Path $resolvedOutput "Tai-WinUI3-Browser-Extension-$($manifest.version).zip"
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("tai-extension-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
    foreach ($relativePath in $requiredFiles) {
        $destinationPath = Join-Path $stagingDirectory $relativePath
        $destinationParent = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $extensionDirectory $relativePath) -Destination $destinationPath
    }
    Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $archivePath -Force
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Output $archivePath
