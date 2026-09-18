[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts"),

    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Release version must use major.minor.patch format: $Version"
}

$publishPath = (Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
$requiredFiles = @(
    "Tai.WinUI.exe",
    "resources.pri",
    "Resources\Icons\tai.ico",
    "Resources\Icons\defaultIcon.png",
    "WebExtensions\Chrome\manifest.json",
    "WebExtensions\Chrome\service-worker.js"
)

foreach ($relativePath in $requiredFiles) {
    $requiredPath = Join-Path $publishPath $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing required published file: $relativePath"
    }
}

$publishedManifest = Get-Content -LiteralPath (Join-Path $publishPath "WebExtensions\Chrome\manifest.json") -Raw -Encoding UTF8 | ConvertFrom-Json
if ($publishedManifest.version -ne $Version) {
    throw "Published browser extension version $($publishedManifest.version) does not match release version $Version."
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$archivePath = Join-Path $resolvedOutput "Tai-WinUI-$Version-win-x64.zip"
$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("tai-winui-" + [Guid]::NewGuid().ToString("N"))

try {
    New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

    foreach ($item in Get-ChildItem -LiteralPath $publishPath -Force) {
        if ($item.Name -in @("Data", "Log")) {
            continue
        }

        if (-not $item.PSIsContainer -and $item.Extension -eq ".pdb") {
            continue
        }

        Copy-Item -LiteralPath $item.FullName -Destination $stagingDirectory -Recurse -Force
    }

    Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $archivePath -Force
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Output $archivePath
