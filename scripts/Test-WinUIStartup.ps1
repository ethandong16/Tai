param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,
    [ValidateSet('auto', 'zh-CN', 'en-US')]
    [string]$Language = 'auto'
)

$ErrorActionPreference = 'Stop'
$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
foreach ($file in @('Tai.WinUI.exe', 'resources.pri', 'default-categories.json')) {
    $item = Get-Item -LiteralPath (Join-Path $publishPath $file)
    if ($item.Length -eq 0) { throw "Empty publish file: $file" }
}

$languageResources = Get-ChildItem -LiteralPath $publishPath -Recurse -File | Where-Object {
    $_.DirectoryName -ne $publishPath -and
    ($_.Name -like '*.resources.dll' -or $_.Name -like '*.dll.mui')
}
foreach ($resource in $languageResources) {
    $cultureName = Split-Path -Path $resource.DirectoryName -Leaf
    if ($cultureName -notmatch '^(en|zh|ja)(-|$)') {
        throw "Published dependency has an unwanted language resource: $($resource.FullName)"
    }
}

$builtInKeys = @(
    'browsing', 'office', 'development', 'communication', 'design',
    'learning', 'media', 'gaming', 'utilities'
)
$catalog = Get-Content -LiteralPath (Join-Path $publishPath 'default-categories.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($catalog.version -ne 1 -or -not $catalog.categories) {
    throw 'Published default-categories.json has an invalid catalog structure.'
}
$catalogKeys = @($catalog.categories | ForEach-Object { $_.key })
foreach ($key in $builtInKeys) {
    if ($catalogKeys -notcontains $key) {
        throw "Published catalog is missing built-in category: $key"
    }
}

# Run against a fresh copy so test data never touches an existing user database.
$testPath = Join-Path ([IO.Path]::GetTempPath()) ('Tai-WinUI-smoke-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $testPath | Out-Null
Get-ChildItem -LiteralPath $publishPath | Where-Object {
    $_.Name -notin @('Data', 'Log', 'startup-smoke.ok')
} | Copy-Item -Destination $testPath -Recurse
if ($Language -ne 'auto') {
    $dataPath = Join-Path $testPath 'Data'
    New-Item -ItemType Directory -Path $dataPath | Out-Null
    $configJson = @{ General = @{ Language = $Language }; Behavior = @{}; Links = @() } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $dataPath 'AppConfig.json'), $configJson,
        [Text.UTF8Encoding]::new($false))
}

# This smoke test renders every page at four window sizes and captures several
# screenshots; allow slower hosted Windows runners enough time to finish.
$process = Start-Process -FilePath (Join-Path $testPath 'Tai.WinUI.exe') `
    -ArgumentList '--smoke-test' -WorkingDirectory $testPath -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(90000)) {
    $process.Kill()
    throw "Startup test timed out. Diagnostics retained in $testPath"
}
$logPath = Join-Path $testPath 'Log/startup.log'
if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath }
if ($process.ExitCode -ne 0 -or -not (Test-Path (Join-Path $testPath 'startup-smoke.ok')) -or
    (Test-Path -LiteralPath $logPath)) {
    throw "Startup test failed (exit $($process.ExitCode)). Diagnostics retained in $testPath"
}
$config = Get-Content -LiteralPath (Join-Path $testPath 'Data/AppConfig.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($key in $builtInKeys) {
    $mapping = $config.General.DefaultCategoryIds.PSObject.Properties[$key]
    if ($null -eq $mapping -or [int]$mapping.Value -le 0) {
        throw "Startup did not map built-in category: $key. Diagnostics retained in $testPath"
    }
}
Get-Content -LiteralPath (Join-Path $testPath 'startup-smoke.ok')
Write-Output "Verified publish resources and startup. Test copy: $testPath"
