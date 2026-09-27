# Tai

[![Build Tai WinUI](https://github.com/ethandong16/Tai/actions/workflows/winui-build.yml/badge.svg?branch=master)](https://github.com/ethandong16/Tai/actions/workflows/winui-build.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Tai is a local Windows usage tracker rebuilt around a WinUI 3 desktop interface. It records foreground application usage and, with the companion Chromium extension, active website usage. Data is stored locally by default; no account or cloud service is required.

This repository is an independent fork and WinUI 3 edition of Tai. It is not an official release of the original Tai project. The original MIT license and attribution are preserved in [`LICENSE`](LICENSE).

## Download

Download the latest package from [GitHub Releases](https://github.com/ethandong16/Tai/releases).

For the current release, download:

- `Tai-WinUI-1.1.0-win-x64.zip` for the self-contained desktop application.

The desktop package is unpackaged and portable. Extract it and run `Tai.WinUI.exe`.

### Requirements

- Windows 10 version 1809 (build 17763) or later.
- 64-bit Windows.
- Administrator permissions may be required to observe some elevated applications.

## Features

- Track foreground application usage time.
- View daily, weekly, monthly, and yearly trends, rankings, and category breakdowns.
- Track active website time in Chrome, Microsoft Edge, and other Chromium browsers.
- Configure application categories, process matching, allowlists, URL filters, and regular expressions.
- Monitor sleep and idle states to reduce inaccurate records.
- Import and export configuration and database data.
- Export statistics as `.xlsx` or `.csv`.
- Use light, dark, or Windows-synchronized themes.
- Keep statistics in a local SQLite database.

## Browser extension

Install the companion extension from the [Chrome Web Store](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim). It works with Chrome and Chromium-based browsers that support Chrome Web Store extensions.

1. Start Tai and enable website tracking in Settings.
2. Open the [Tai Sentry Chrome Web Store page](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim).
3. Install the extension from the store, then return to Tai.

The extension reads the active tab URL, title, favicon URL, and timing information through the `tabs` permission. It sends data only to Tai on the same computer through `ws://127.0.0.1:8908/TaiWebSentry`. It does not read page contents, forms, passwords, cookies, downloads, or browser account data. See the full [browser extension privacy policy](docs/browser-extension-privacy.md).

The desktop application no longer bundles or installs an unpacked extension. Store listing materials and the extension source remain available in [`WebExtensions`](WebExtensions) for maintenance.

## Build from source

### Environment

- .NET 8 SDK.
- Visual Studio 2022 17.10 or later with a Windows desktop workload, or an equivalent MSBuild environment.
- Windows App SDK `1.6.240829007`.
- Windows 10 SDK `10.0.19041` or a compatible version.

The maintained build target is `Tai.WinUI/Tai.WinUI.csproj`. The shared implementation is compiled through `Core.Modern/Core.Modern.csproj`.

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

Run the published startup check:

```powershell
powershell -ExecutionPolicy Bypass `
  -File scripts/Test-WinUIStartup.ps1 `
  -PublishDirectory "Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish"
```

Create the desktop release archive locally:

```powershell
./scripts/Pack-WinUIRelease.ps1 `
  -PublishDirectory "Tai.WinUI/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/publish" `
  -OutputDirectory artifacts `
  -Version 1.1.0
```

Every `v*.*.*` tag runs the Windows build, startup smoke test, archive packaging, and GitHub Release workflow. See [`CONTRIBUTING.md`](CONTRIBUTING.md) for development rules.

## Project structure

| Path | Purpose |
| --- | --- |
| `Tai.WinUI` | Maintained WinUI 3 desktop application |
| `Core` | Shared monitoring, database, configuration, and website services |
| `Core.Modern` | .NET 8 Windows build entry for the shared core |
| `WebExtensions/Chrome` | Chromium website tracking extension |
| `scripts` | Build, smoke-test, and release packaging scripts |
| `docs` | Privacy and product documentation |

## Local data and privacy

Tai stores its local data in the application directory by default:

- Statistics database: `Data/data.db`
- Configuration: `Data/AppConfig.json`
- Logs: `Log/startup.log`

Tai has no account system, cloud synchronization, or telemetry service. The browser extension communicates with the local Tai process only. Tai may request website favicons and cache them locally for display.

Delete the application directory to uninstall. Back up the `Data` directory before removal if historical records are important.

## Contributing

New features and fixes should target the WinUI 3 application and shared core. Generated files, local databases, build output, and design review artifacts should not be committed. See [`CONTRIBUTING.md`](CONTRIBUTING.md).

Please use [Issues](https://github.com/ethandong16/Tai/issues) for reproducible bugs and [Pull Requests](https://github.com/ethandong16/Tai/pulls) for proposed changes.

## English summary

Tai is a local Windows time tracker focused on application and website usage. The maintained desktop client is built with WinUI 3 and targets `win-x64`. The browser companion connects to the desktop app over the local loopback WebSocket endpoint and does not upload browsing data to a remote service.

Install the desktop ZIP from [Releases](https://github.com/ethandong16/Tai/releases), then install [Tai Sentry from the Chrome Web Store](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim). Build instructions, privacy details, and release scripts are documented above and in the linked project files.

This is an independent fork and is not an official release of the original Tai project. Tai is distributed under the [MIT License](LICENSE).
