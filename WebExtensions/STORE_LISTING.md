# Tai Sentry Store Listing

Tai Sentry is the companion browser extension for the Tai WinUI desktop app. It is an independent fork companion and is not an official release of the original Tai project.

## Chrome Web Store

- Name: `Tai Sentry`
- Short description: `Track active website usage locally with the Tai Windows desktop app.`
- Category: Productivity
- Default language: English
- Website: `https://github.com/ethandong16/Tai`

### Detailed description

> Tai Sentry connects a Chromium browser to the Tai Windows desktop app. When the browser is in the foreground, it records the active tab URL, title, favicon URL, and time spent, then sends timing data to Tai on the same computer. It does not require an account and does not upload browsing data to a developer server. Install and run Tai first, then enable Website tracking in Settings.

### Single purpose

> Send active-tab usage time to Tai on the same computer so Tai can display website usage statistics.

### Permission justification

> The `tabs` permission is required to identify the active tab and tab changes and to read the URL, title, and favicon URL needed to measure foreground website time. The extension does not read page content, forms, passwords, cookies, downloads, or browser account data.

### Data disclosure

- Browsing history: yes, active-tab URL, title, and time spent.
- Website content: no.
- Authentication, identity, financial, health, location, or communication data: no.
- Use: core functionality.
- Sale, advertising, or credit evaluation: no.
- Transfer: only to Tai on the local computer.

## Microsoft Edge Add-ons

The same ZIP package can be submitted to Microsoft Edge Add-ons. Keep the product name, description, permission explanations, and data disclosures consistent with the Chrome listing.

## Release checklist

1. Run `powershell -ExecutionPolicy Bypass -File scripts/Pack-BrowserExtension.ps1`.
2. Upload the generated `artifacts/Tai-WinUI3-Browser-Extension-<version>.zip`, not the repository.
3. Include screenshots that clearly show website statistics in Tai.
4. State that the extension is an independent fork and is not the original Tai project's official extension.
5. In the desktop app, direct users to the reviewed [Chrome Web Store listing](https://chromewebstore.google.com/detail/tai-sentry/fmjgafoilnpanbpgdkljkjfjgainboim). Do not provide manual unpacked-extension installation.
6. Increment `manifest.json`'s version for future updates and update the same store listing so the extension ID remains stable.
