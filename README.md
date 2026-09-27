# Tai

[![Build Tai WinUI](https://github.com/ethandong16/Tai/actions/workflows/winui-build.yml/badge.svg?branch=master)](https://github.com/ethandong16/Tai/actions/workflows/winui-build.yml)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Tai is a lightweight Windows app for understanding how you spend time on your computer. It tracks foreground applications locally and can also track active websites through the Tai Sentry browser extension.

No account is required. Your usage database stays on your computer.
Tai is an independent fork and is not an official release of the original Tai project. The original MIT license and attribution are preserved in [`LICENSE`](LICENSE).

## Download

Download the latest version from [GitHub Releases](https://github.com/ethandong16/Tai/releases).

For version 1.1.0, download [`Tai-WinUI-1.1.0-win-x64.zip`](https://github.com/ethandong16/Tai/releases/download/v1.1.0/Tai-WinUI-1.1.0-win-x64.zip).

Tai is a portable desktop app:

1. Download the ZIP file.
2. Extract it to a folder you control.
3. Run `Tai.WinUI.exe`.

### System requirements

- Windows 10 version 1809 (build 17763) or later.
- 64-bit Windows.
- Administrator permission may be required to observe applications running with elevated permission.

## What Tai tracks

- Foreground application usage time.
- Daily, weekly, monthly, and yearly statistics.
- Frequently used applications and usage trends.
- Website usage through the optional browser extension.
- Application categories and category breakdowns.
- Detailed records with date, period, source, and search filters.

Tai includes default categories for common browsers, office apps, development tools, communication apps, creative tools, learning apps, media players, games, and system utilities. You can rename, delete, or add categories at any time.

## Website tracking

Website tracking is optional. Install [Tai Sentry from the Chrome Web Store](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim), then:

1. Open Tai.
2. Open **Settings**.
3. Enable **Website tracking**.
4. Keep Tai running while you browse.

Tai Sentry supports Chrome and Chromium-based browsers that can install Chrome Web Store extensions. It sends website timing data to Tai on the same computer through the local connection. It does not provide a separate account or cloud sync service.

## Using Tai

### Overview

See today's total application time, the number of applications and websites recorded, the weekly trend, category distribution, and frequently used items.

### Statistics

Switch between day, week, month, and year views to compare trends, rankings, and category usage.

### Detailed records

Filter records by date range and source, search for an application or website, and open an item for more detail.

### Categories

Create your own categories or edit directory matching rules. Tai also assigns recognized applications to its built-in categories automatically. Manual categorization takes priority.

### Settings

Manage startup behavior, theme, sleep monitoring, ignored applications, process allowlists, URL filters, data export, and data cleanup.

## Your data

Tai stores its data locally in the application folder by default:

- `Data/data.db` contains usage records.
- `Data/AppConfig.json` contains settings.
- `Log/startup.log` contains startup diagnostics.

There is no Tai account, advertising service, or cloud synchronization. You can disable website tracking, delete selected records in Settings, or remove the application folder to uninstall Tai. Back up the `Data` folder before deleting it if you need to keep your history.

## Troubleshooting

### An application is missing

Make sure Tai is running and that the application is in the foreground. Some elevated applications require Tai to run with the same or higher permission level.

### Website data is missing

Confirm that Tai Sentry is installed and enabled in your browser, Tai Sentry is connected to the same computer, and **Website tracking** is enabled in Tai.

### The window opens off-screen

Tai restores the window inside the available display area. Use the Windows snap or move commands if a display configuration has changed.

## Links

- [Latest releases](https://github.com/ethandong16/Tai/releases)
- [Tai Sentry on the Chrome Web Store](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim)
- [Report a bug](https://github.com/ethandong16/Tai/issues)
- [Project discussions](https://github.com/ethandong16/Tai/discussions)

Tai is an independent fork and is not an official release of the original Tai project. It is distributed under the [MIT License](LICENSE).
