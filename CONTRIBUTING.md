# Contributing to Tai

Thank you for helping improve Tai. The maintained product is the WinUI 3 desktop application and its shared monitoring core.

## Development environment

- Windows 10 version 1809 or later.
- .NET 8 SDK.
- Visual Studio 2022 17.10 or later with Windows desktop development support.
- Windows App SDK `1.6.240829007`.

Build the maintained application with:

```powershell
dotnet restore Tai.WinUI/Tai.WinUI.csproj -r win-x64
dotnet build Tai.WinUI/Tai.WinUI.csproj -c Release -p:Platform=x64 --no-restore
```

For a release-style validation, publish the self-contained application and run:

```powershell
powershell -ExecutionPolicy Bypass `
  -File scripts/Test-WinUIStartup.ps1 `
  -PublishDirectory "Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish"
```

## Change guidelines

- Keep new desktop features in `Tai.WinUI`.
- Put shared monitoring, database, and configuration changes in `Core`.
- Keep browser extension changes under `WebExtensions/Chrome`.
- Do not commit `bin`, `obj`, `artifacts`, local databases, logs, or generated design-review files.
- Update the privacy documentation when data handling or permissions change.
- Keep version changes consistent across the WinUI project, browser extension manifest, changelog, and release tag.

## Pull requests

Describe the user-visible behavior, affected areas, and verification performed. For UI changes, include screenshots when they make the change easier to review. Pull requests should pass the Windows build and startup smoke test before merge.

## Releases

Releases are created by pushing a semantic version tag such as `v1.0.0`. The release workflow builds the self-contained WinUI package, validates startup, packages the browser extension, and uploads both archives to GitHub Releases.
