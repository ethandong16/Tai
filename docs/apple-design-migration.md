# Apple Design to WinUI 3

2026-09-21: Migrated the approved standalone web design demo to the native WinUI 3 interface.

## Migrated areas

- Acrylic sidebar, Tai branding, page titles, 16 px content surfaces, and light/dark color themes.
- Overview usage summary, trend and category columns, application and website lists, with a vertical layout for narrow windows.
- Native usage trend chart with keyboard focus and duration details; future periods are not rendered as zero usage.
- Native category ring chart based on real category data, including an empty state and an accessibility summary.
- Native period selector, record surfaces, category management, settings, and detail cards.
- Record search by name and category; direct application and website details from the overview; source filters preserved when opening the full list.
- Navigation synchronization fixes that avoid duplicate navigation and restore the correct title when returning to records.

## Native adaptation

The Windows title bar, original Tai icon, five settings sections, data collection, database, category rules, and import/export behavior are preserved. The application does not use WebView, a web runtime, or demo data.

The overview category breakdown uses the combined application and website total, and states that scope explicitly. The main overview duration counts applications only and does not double-count website time. WinUI and Windows manage Acrylic fallback behavior and standard control interaction. Main navigation does not introduce large movement animations.

Text uses the higher-contrast blue `#2463CC` and charts use `#628AF0`. Dark mode uses `#8EB6FF` and `#346FD1` respectively. Existing text and button contrast checks remain enabled.

## Build and verification

```powershell
dotnet publish Tai.WinUI/Tai.WinUI.csproj -c Release -p:Platform=x64 -r win-x64 -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:EnableMsixTooling=true --no-restore -o artifacts/apple-design-winui
./scripts/Test-WinUIStartup.ps1 -PublishDirectory artifacts/apple-design-winui
```

The startup check runs in a new temporary copy with an empty database. It covers all seven pages, 1240 / 920 / 700 / 480 window widths, themes, contrast, date ranges, period selection, detail navigation, source filtering, title restoration, icons, and database queries. Test screenshots and the startup marker remain in the temporary test copy.

The executable is `artifacts/apple-design-winui/Tai.WinUI.exe` and must remain with its published dependencies. The build may report existing NU1701 compatibility warnings for SQLite.Linq and WebSocketSharp.
