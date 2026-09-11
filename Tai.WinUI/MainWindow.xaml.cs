using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using Core.Models.Config;
using WinRT.Interop;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;

namespace Tai.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly double _rasterizationScale;
    private readonly Windows.Graphics.SizeInt32 _workAreaSize;
    private readonly bool _isSmokeTest;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _isSmokeTest = Environment.GetCommandLineArgs().Contains("--smoke-test");
        _rasterizationScale = Math.Max(1d, GetDpiForWindow(hwnd) / 96d);
        var workArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        _workAreaSize = new Windows.Graphics.SizeInt32(workArea.Width, workArea.Height);
        _appWindow.Title = "Tai";
        ConfigureTitleBar(_appWindow.TitleBar, false);
        ResizeForEffectiveSize(1240, 820, constrainToWorkArea: true);

        RootFrame.Navigate(typeof(MainPage));

        Closed += (_, _) =>
        {
            SaveWindowSize();
            App.Services.GetService<IMain>()?.Exit();
        };
    }

    public void ShowStartupError()
    {
        StartupError.IsOpen = true;
    }

    internal bool IsPerMonitorDpiAware()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        return GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(hwnd)) == 2;
    }

    internal async Task VerifyPagesAsync()
    {
        ValidateTitleBar();
        Type[] pages = [typeof(Views.DashboardPage), typeof(Views.StatisticsPage),
            typeof(Views.DetailsPage), typeof(Views.CategoriesPage), typeof(Views.SettingsPage),
            typeof(Views.AppDetailPage), typeof(Views.WebsiteDetailPage)];
        (int Width, int Height)[] windowSizes =
        [
            (920, 640),
            (700, 640),
            (480, 640)
        ];

        foreach (var windowSize in windowSizes)
        {
            ResizeForEffectiveSize(windowSize.Width, windowSize.Height, constrainToWorkArea: false);
            await Task.Delay(250);

            if (!RootFrame.Navigate(typeof(MainPage))) throw new InvalidOperationException("Cannot navigate to MainPage");
            await Task.Delay(250);
            RootFrame.UpdateLayout();
            if (RootFrame.Content is not MainPage shell) throw new InvalidOperationException("MainPage did not load");
            ValidateShellLayout(shell);

            foreach (var page in pages)
            {
                if (!RootFrame.Navigate(page)) throw new InvalidOperationException($"Cannot navigate to {page.Name}");
                // Allow adaptive states, bindings and layout to settle before checking the next page.
                await Task.Delay(250);
                RootFrame.UpdateLayout();
                if (RootFrame.Content is not FrameworkElement content) throw new InvalidOperationException($"{page.Name} did not load");
                ValidatePageLayout(content);
                if (content is Views.SettingsPage settingsPage)
                    ValidateSettingsPage(settingsPage);
            }
        }

        ResizeForEffectiveSize(1240, 820, constrainToWorkArea: true);
    }

    private void ResizeForEffectiveSize(int width, int height, bool constrainToWorkArea)
    {
        var physicalWidth = (int)Math.Round(width * _rasterizationScale);
        var physicalHeight = (int)Math.Round(height * _rasterizationScale);
        if (constrainToWorkArea)
        {
            physicalWidth = Math.Min(physicalWidth, (int)Math.Round(_workAreaSize.Width * 0.95));
            physicalHeight = Math.Min(physicalHeight, (int)Math.Round(_workAreaSize.Height * 0.9));
        }
        else
        {
            physicalHeight = Math.Min(physicalHeight, _workAreaSize.Height);
        }

        _appWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
    }

    internal void ApplyStartupSettings()
    {
        ApplyAppearance();
        var general = App.Services.GetService<IAppConfig>()?.GetConfig()?.General;
        if (general?.IsSaveWindowSize == true && general.WindowWidth >= 480 && general.WindowHeight >= 480)
        {
            ResizeForEffectiveSize(
                (int)Math.Round(general.WindowWidth),
                (int)Math.Round(general.WindowHeight),
                constrainToWorkArea: true);
        }
    }

    internal void ApplyAppearance()
    {
        var general = App.Services.GetService<IAppConfig>()?.GetConfig()?.General;
        if (general == null) return;

        var dark = general.Theme == 1;
        if (Content is FrameworkElement root)
            root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;

        var pageBackground = ParseColor(dark ? "#17191D" : "#F6F7FB", Colors.Transparent);
        var cardBackground = ParseColor(dark ? "#22252A" : "#FFFFFF", Colors.Transparent);
        var mutedBackground = ParseColor(dark ? "#2B2F35" : "#F1F4F8", Colors.Transparent);
        var primaryText = ParseColor(dark ? "#F4F5F7" : "#18212F", Colors.Transparent);
        var secondaryText = ParseColor(dark ? "#AAB1BC" : "#667085", Colors.Transparent);
        var divider = ParseColor(dark ? "#3A3F47" : "#E5E8EE", Colors.Transparent);
        var accent = ParseColor(general.ThemeColor, ParseColor("#2B20D9", Colors.Transparent));

        SetBrushColor("TaiPageBackgroundBrush", pageBackground);
        SetBrushColor("TaiCardBackgroundBrush", cardBackground);
        SetBrushColor("TaiCardMutedBrush", mutedBackground);
        SetBrushColor("TaiPrimaryTextBrush", primaryText);
        SetBrushColor("TaiSecondaryTextBrush", secondaryText);
        SetBrushColor("TaiDividerBrush", divider);
        SetBrushColor("TaiAccentBrush", accent);
        SetBrushColor("TaiAccentSoftBrush", Blend(accent, cardBackground, 0.16));
        SetBrushColor("TaiInfoBrush", ParseColor(dark ? "#8EA8FF" : "#4969D8", Colors.Transparent));
        SetBrushColor("TaiInfoSoftBrush", ParseColor(dark ? "#252D4A" : "#E9EDFF", Colors.Transparent));
        SetBrushColor("TaiWarningBrush", ParseColor(dark ? "#F4B860" : "#C77912", Colors.Transparent));
        SetBrushColor("TaiWarningSoftBrush", ParseColor(dark ? "#43351F" : "#FFF3DD", Colors.Transparent));
        SetBrushColor("TaiDangerBrush", ParseColor(dark ? "#F18A9C" : "#D14C62", Colors.Transparent));
        SetBrushColor("TaiDangerSoftBrush", ParseColor(dark ? "#472A31" : "#FBE7EB", Colors.Transparent));
        ConfigureTitleBar(_appWindow.TitleBar, dark);
    }

    private void SaveWindowSize()
    {
        if (_isSmokeTest) return;
        try
        {
            var appConfig = App.Services.GetService<IAppConfig>();
            var general = appConfig?.GetConfig()?.General;
            if (general?.IsSaveWindowSize != true) return;

            general.WindowWidth = _appWindow.Size.Width / _rasterizationScale;
            general.WindowHeight = _appWindow.Size.Height / _rasterizationScale;
            appConfig!.Save();
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
        }
    }

    private static void SetBrushColor(string key, Windows.UI.Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush) brush.Color = color;
    }

    private static Windows.UI.Color ParseColor(string? value, Windows.UI.Color fallback)
    {
        var hex = (value ?? string.Empty).TrimStart('#');
        if (hex.Length == 6
            && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return ColorHelper.FromArgb(255, r, g, b);
        }
        return fallback;
    }

    private static Windows.UI.Color Blend(Windows.UI.Color foreground, Windows.UI.Color background, double amount)
    {
        byte Mix(byte front, byte back) => (byte)Math.Round(front * amount + back * (1 - amount));
        return ColorHelper.FromArgb(255,
            Mix(foreground.R, background.R),
            Mix(foreground.G, background.G),
            Mix(foreground.B, background.B));
    }

    private static void ConfigureTitleBar(AppWindowTitleBar titleBar, bool dark)
    {
        var background = dark ? ColorHelper.FromArgb(255, 34, 37, 42) : ColorHelper.FromArgb(255, 255, 255, 255);
        var inactiveBackground = dark ? ColorHelper.FromArgb(255, 29, 32, 36) : ColorHelper.FromArgb(255, 246, 247, 251);
        var foreground = dark ? ColorHelper.FromArgb(255, 244, 245, 247) : ColorHelper.FromArgb(255, 24, 33, 47);
        var inactiveForeground = dark ? ColorHelper.FromArgb(255, 170, 177, 188) : ColorHelper.FromArgb(255, 102, 112, 133);
        var hoverBackground = dark ? ColorHelper.FromArgb(255, 58, 63, 71) : ColorHelper.FromArgb(255, 229, 233, 240);
        var pressedBackground = dark ? ColorHelper.FromArgb(255, 72, 78, 88) : ColorHelper.FromArgb(255, 211, 217, 227);

        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = inactiveBackground;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hoverBackground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressedBackground;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonInactiveBackgroundColor = inactiveBackground;
    }

    private static void ValidateShellLayout(MainPage shell)
    {
        if (shell.FindName("RootNavigation") is not NavigationView navigation)
        {
            throw new InvalidOperationException("Responsive navigation was not created.");
        }

        var expectedMode = shell.ActualWidth >= 900
            ? NavigationViewPaneDisplayMode.Left
            : shell.ActualWidth >= 640
                ? NavigationViewPaneDisplayMode.LeftCompact
                : NavigationViewPaneDisplayMode.LeftMinimal;
        if (navigation.PaneDisplayMode != expectedMode)
        {
            throw new InvalidOperationException($"Navigation layout mismatch at {shell.ActualWidth:F0} effective pixels: expected {expectedMode}, actual {navigation.PaneDisplayMode}.");
        }
    }

    private static void ValidatePageLayout(FrameworkElement page)
    {
        var layoutName = page is Views.DetailsPage ? "LayoutRoot" : "PageStack";
        if (page.FindName(layoutName) is not FrameworkElement layout)
        {
            throw new InvalidOperationException($"Responsive layout root was not found on {page.GetType().Name}.");
        }

        var expectedLeftMargin = page.ActualWidth >= 820 ? 24d : 12d;
        if (Math.Abs(layout.Margin.Left - expectedLeftMargin) > 0.1)
        {
            throw new InvalidOperationException($"{page.GetType().Name} layout mismatch at {page.ActualWidth:F0} effective pixels: expected left margin {expectedLeftMargin:F0}, actual {layout.Margin.Left:F0}.");
        }
    }

    private void ValidateTitleBar()
    {
        var titleBar = _appWindow.TitleBar;
        if (titleBar.ButtonForegroundColor == titleBar.BackgroundColor)
            throw new InvalidOperationException("Caption buttons do not contrast with the title bar background.");
        if (TitleBarLogo.Source is not Microsoft.UI.Xaml.Media.Imaging.BitmapImage image
            || !image.UriSource.AbsoluteUri.EndsWith("/Resources/Icons/tai.png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The original Tai title bar icon is not configured.");
        if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
            throw new InvalidOperationException("The original Tai title bar icon could not be decoded.");
    }

    private static void ValidateSettingsPage(Views.SettingsPage page)
    {
        if (page.FindName("SettingsTabs") is not TabView { TabItems.Count: 5 })
            throw new InvalidOperationException("The five legacy settings sections were not created.");
        if (page.FindName("SettingsLogo") is not Image { Source: Microsoft.UI.Xaml.Media.Imaging.BitmapImage image }
            || !image.UriSource.AbsoluteUri.EndsWith("/Resources/Icons/tai.png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The original Tai settings icon is not configured.");

        if (page.FindName("StartPagePicker") is not ComboBox startPagePicker) return;
        var narrow = page.ActualWidth < 820;
        var expectedRow = narrow ? 1 : 0;
        var expectedColumn = narrow ? 0 : 1;
        if (Grid.GetRow(startPagePicker) != expectedRow || Grid.GetColumn(startPagePicker) != expectedColumn)
            throw new InvalidOperationException($"Settings controls did not reflow at {page.ActualWidth:F0} effective pixels.");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
