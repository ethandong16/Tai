# Tai WinUI

`Tai.WinUI` is the maintained Windows desktop client for Tai. It is built with .NET 8, WinUI 3, and the Windows App SDK.

The desktop client provides the overview, statistics, detailed records, categories, settings, and local website tracking workflow described in the root [user guide](../README.md).

## Requirements

- Windows 10 version 1809 (build 17763) or later.
- .NET 8 SDK.
- Windows App SDK `1.6.240829007`.
- x64 build environment.

## Build

```powershell
dotnet restore Tai.WinUI/Tai.WinUI.csproj -r win-x64

dotnet publish Tai.WinUI/Tai.WinUI.csproj `
  -c Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:SelfContained=true `
  -p:WindowsAppSDKSelfContained=true `
  -p:EnableMsixTooling=true `
  --no-restore
```

Published dependency language resources are limited to Chinese, English, and Japanese.

Run the published startup check from the repository root:

```powershell
powershell -ExecutionPolicy Bypass `
  -File scripts/Test-WinUIStartup.ps1 `
  -PublishDirectory "Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish"
```

The shared implementation is compiled through `Core.Modern/Core.Modern.csproj`. Release builds are produced by the GitHub Actions workflow when a semantic version tag is pushed.
