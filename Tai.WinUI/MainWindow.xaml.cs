using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using WinRT.Interop;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Tai.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly double _rasterizationScale;
    private readonly Windows.Graphics.RectInt32 _workArea;
    private readonly bool _isSmokeTest;

    public MainWindow()
    {
        InitializeComponent();
        if (Content is FrameworkElement root)
            root.ActualThemeChanged += Root_ActualThemeChanged;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _isSmokeTest = Environment.GetCommandLineArgs().Contains("--smoke-test");
        _rasterizationScale = Math.Max(1d, GetDpiForWindow(hwnd) / 96d);
        var workArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        _workArea = workArea;
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
        ValidateUsageRanges();
        await ValidateUsageDataAsync();
        ValidateIconFallbacks();
        await ValidateThemesAsync();
        Type[] pages = [typeof(Views.DashboardPage), typeof(Views.StatisticsPage),
            typeof(Views.DetailsPage), typeof(Views.CategoriesPage), typeof(Views.SettingsPage),
            typeof(Views.AppDetailPage), typeof(Views.WebsiteDetailPage)];
        (int Width, int Height)[] windowSizes =
        [
            (1240, 900),
            (920, 640),
            (700, 640),
            (480, 640)
        ];

        foreach (var windowSize in windowSizes)
        {
            // Clear the previous page before resizing so it cannot impose a larger
            // native minimum width while the next viewport is prepared.
            RootFrame.Content = null;
            ResizeForEffectiveSize(windowSize.Width, windowSize.Height, constrainToWorkArea: false);
            await Task.Delay(250);
            await WaitForSmokeWindowSizeAsync(windowSize.Width);

            if (!RootFrame.Navigate(typeof(MainPage))) throw new InvalidOperationException("Cannot navigate to MainPage");
            await Task.Delay(250);
            RootFrame.UpdateLayout();
            if (RootFrame.Content is not MainPage shell) throw new InvalidOperationException("MainPage did not load");
            ValidateShellLayout(shell);
            await CaptureSmokeScreenshotAsync(shell, windowSize.Width);
            if (windowSize.Width == 1240)
                await ValidateDashboardNavigationAsync(shell);

            foreach (var page in pages)
            {
                if (page == typeof(Views.CategoriesPage))
                {
                    SeedCategorySmokeApp();
                    await SeedCategorySmokeWebsiteAsync();
                }
                if (!RootFrame.Navigate(page)) throw new InvalidOperationException($"Cannot navigate to {page.Name}");
                // Allow adaptive states, bindings and layout to settle before checking the next page.
                await Task.Delay(250);
                RootFrame.UpdateLayout();
                if (RootFrame.Content is not FrameworkElement content) throw new InvalidOperationException($"{page.Name} did not load");
                ValidatePageLayout(content);
                if (content is Views.CategoriesPage categoriesPage)
                {
                    await categoriesPage.LoadDataTask;
                    await ValidateCategoriesPageAsync(categoriesPage);
                    await Task.Delay(80);
                    RootFrame.UpdateLayout();
                }
                if ((windowSize.Width == 1240 || (windowSize.Width == 480 &&
                    (page == typeof(Views.SettingsPage) || page == typeof(Views.CategoriesPage))))
                    && Content is FrameworkElement screenshotRoot)
                    await CaptureSmokeScreenshotAsync(screenshotRoot, windowSize.Width, page.Name);
                if (content is Views.StatisticsPage statisticsPage)
                    ValidateStatisticsPage(statisticsPage);
                if (content is Views.DetailsPage detailsPage)
                    ValidateDetailsPage(detailsPage);
                if (content is Views.SettingsPage settingsPage)
                    ValidateSettingsPage(settingsPage);
            }
        }

        ResizeForEffectiveSize(1240, 820, constrainToWorkArea: true);
    }

    private async Task WaitForSmokeWindowSizeAsync(int expectedWidth)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (Content is FrameworkElement root)
            {
                root.UpdateLayout();
                var width = root.XamlRoot.Size.Width;
                // The client area excludes the window's resize borders.
                if (width <= expectedWidth && width >= expectedWidth - 32) return;
            }
            await Task.Delay(50);
        }
        throw new InvalidOperationException($"The window did not render the requested {expectedWidth}px width.");
    }

    private static async Task ValidateDashboardNavigationAsync(MainPage shell)
    {
        if (shell.FindName("ContentFrame") is not Frame frame)
            throw new InvalidOperationException("Shell content frame was not created.");
        var item = new ViewModels.UsageItem(0, "Navigation smoke test", "1分钟", 60, 100,
            Services.AppIconResolver.DefaultIconPath, "#3AAFD0", "未分类", "应用");
        frame.Navigate(typeof(Views.AppDetailPage), new Views.DetailRow(item, "Smoke test"));
        await Task.Delay(80);
        if (frame.Content is not Views.AppDetailPage)
            throw new InvalidOperationException("Selecting a dashboard row redirected away from its detail page.");
        frame.Navigate(typeof(Views.DetailsPage), "网站");
        if (frame.Content is not Views.DetailsPage details
            || details.FindName("TypeFilter") is not ComboBox { SelectedIndex: 2 })
            throw new InvalidOperationException("Website view-all did not preserve its source filter.");
        if (shell.FindName("PageTitle") is not TextBlock pageTitle ||
            pageTitle.Text != Services.L.Text("详细记录"))
            throw new InvalidOperationException("Returning from a detail page did not restore the page title.");
        frame.Navigate(typeof(Views.DashboardPage));
    }

    private void ResizeForEffectiveSize(int width, int height, bool constrainToWorkArea)
    {
        var physicalWidth = (int)Math.Round(width * _rasterizationScale);
        var physicalHeight = (int)Math.Round(height * _rasterizationScale);
        if (constrainToWorkArea)
        {
            physicalWidth = Math.Min(physicalWidth, (int)Math.Round(_workArea.Width * 0.95));
            physicalHeight = Math.Min(physicalHeight, (int)Math.Round(_workArea.Height * 0.9));
        }
        else
        {
            physicalHeight = Math.Min(physicalHeight, _workArea.Height);
        }

        _appWindow.Resize(new Windows.Graphics.SizeInt32(physicalWidth, physicalHeight));
        if (constrainToWorkArea)
        {
            var x = _workArea.X + Math.Max(0, (_workArea.Width - physicalWidth) / 2);
            var y = _workArea.Y + Math.Max(0, (_workArea.Height - physicalHeight) / 2);
            _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
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

        if (Content is not FrameworkElement root) return;
        root.RequestedTheme = general.Theme switch
        {
            0 => ElementTheme.Light,
            1 => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        var dark = general.Theme == 1 || general.Theme == 2 && root.ActualTheme == ElementTheme.Dark;
        ApplyResolvedAppearance(dark);
    }

    private void Root_ActualThemeChanged(FrameworkElement sender, object args)
    {
        var general = App.Services.GetService<IAppConfig>()?.GetConfig()?.General;
        if (general == null) return;
        var dark = general.Theme == 1 || general.Theme == 2 && sender.ActualTheme == ElementTheme.Dark;
        ApplyResolvedAppearance(dark);
    }

    private void ApplyResolvedAppearance(bool dark)
    {
        var pageBackground = ParseColor(dark ? "#18252B" : "#F4F8FA", Colors.Transparent);
        var cardBackground = ParseColor(dark ? "#1D2930" : "#FFFFFF", Colors.Transparent);
        var mutedBackground = ParseColor(dark ? "#263C44" : "#EAF4F6", Colors.Transparent);
        var primaryText = ParseColor(dark ? "#EFFCFF" : "#20343B", Colors.Transparent);
        var secondaryText = ParseColor(dark ? "#A1BBC4" : "#63757C", Colors.Transparent);
        var divider = ParseColor(dark ? "#38515C" : "#D8EDF2", Colors.Transparent);
        var accent = ParseColor(dark ? "#7DD3FC" : "#155E75", Colors.Transparent);
        var accentFill = ParseColor(dark ? "#207F9D" : "#155E75", Colors.Transparent);
        var accentSoft = ParseColor(dark ? "#1F424C" : "#E7F6FA", Colors.Transparent);
        var accentHover = ParseColor(dark ? "#2A8AA7" : "#0E7490", Colors.Transparent);
        var accentPressed = ParseColor(dark ? "#176A82" : "#0B5C70", Colors.Transparent);
        var accentDisabled = ParseColor(dark ? "#3A636D" : "#9BC6D1", Colors.Transparent);
        var buttonHover = ParseColor(dark ? "#2E4A54" : "#E2F1F4", Colors.Transparent);
        var buttonPressed = ParseColor(dark ? "#3A5B66" : "#CDE6EB", Colors.Transparent);

        SetBrushColor("TaiSidebarBrush", ParseColor(dark ? "#20343B" : "#E8F3F6", Colors.Transparent));
        SetBrushColor("TaiChartBrush", ParseColor(dark ? "#67E8F9" : "#3AAFD0", Colors.Transparent));
        if (Application.Current.Resources["TaiSidebarMaterial"] is AcrylicBrush material)
        {
            material.TintColor = ParseColor(dark ? "#20343B" : "#E8F3F6", Colors.Transparent);
            material.FallbackColor = material.TintColor;
        }
        SetBrushColor("TaiPageBackgroundBrush", pageBackground);
        SetBrushColor("TaiCardBackgroundBrush", cardBackground);
        SetBrushColor("TaiCardMutedBrush", mutedBackground);
        SetBrushColor("TaiPrimaryTextBrush", primaryText);
        SetBrushColor("TaiSecondaryTextBrush", secondaryText);
        SetBrushColor("TaiDividerBrush", divider);
        SetBrushColor("TaiAccentBrush", accent);
        SetBrushColor("TaiAccentFillBrush", accentFill);
        SetBrushColor("TaiAccentSoftBrush", accentSoft);
        SetBrushColor("TaiButtonHoverBrush", buttonHover);
        SetBrushColor("TaiButtonPressedBrush", buttonPressed);
        SetBrushColor("ToggleButtonBackgroundChecked", cardBackground);
        SetBrushColor("ToggleButtonBackgroundCheckedPointerOver", buttonHover);
        SetBrushColor("ToggleButtonBackgroundCheckedPressed", buttonPressed);
        SetBrushColor("ToggleButtonBackgroundCheckedDisabled", mutedBackground);
        SetBrushColor("ToggleSwitchFillOn", accentFill);
        SetBrushColor("ToggleSwitchFillOnPointerOver", accentHover);
        SetBrushColor("ToggleSwitchFillOnPressed", accentPressed);
        SetBrushColor("ToggleSwitchFillOnDisabled", accentDisabled);
        SetBrushColor("NavigationViewSelectionIndicatorForeground", accent);
        SetBrushColor("TaiInfoBrush", ParseColor(dark ? "#7DD3FC" : "#176B86", Colors.Transparent));
        SetBrushColor("TaiInfoSoftBrush", ParseColor(dark ? "#21424D" : "#E6F6FA", Colors.Transparent));
        SetBrushColor("TaiWarningBrush", ParseColor(dark ? "#F4B860" : "#9A5B08", Colors.Transparent));
        SetBrushColor("TaiWarningSoftBrush", ParseColor(dark ? "#43351F" : "#FFF3DD", Colors.Transparent));
        SetBrushColor("TaiDangerBrush", ParseColor(dark ? "#F18A9C" : "#B8324B", Colors.Transparent));
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

    private static Windows.UI.Color GetBrushColor(string key)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush) return brush.Color;
        throw new InvalidOperationException($"Theme brush {key} was not found.");
    }

    private static double ContrastRatio(Windows.UI.Color first, Windows.UI.Color second)
    {
        var lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Windows.UI.Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static void RequireContrast(string name, Windows.UI.Color foreground, Windows.UI.Color background, double minimum)
    {
        var ratio = ContrastRatio(foreground, background);
        if (ratio + 0.001 < minimum)
            throw new InvalidOperationException($"{name} contrast is {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static void ConfigureTitleBar(AppWindowTitleBar titleBar, bool dark)
    {
        var background = dark ? ColorHelper.FromArgb(255, 29, 41, 48) : ColorHelper.FromArgb(255, 255, 255, 255);
        var inactiveBackground = dark ? ColorHelper.FromArgb(255, 24, 37, 43) : ColorHelper.FromArgb(255, 244, 248, 250);
        var foreground = dark ? ColorHelper.FromArgb(255, 239, 252, 255) : ColorHelper.FromArgb(255, 32, 52, 59);
        var inactiveForeground = dark ? ColorHelper.FromArgb(255, 161, 187, 196) : ColorHelper.FromArgb(255, 99, 117, 124);
        var hoverBackground = dark ? ColorHelper.FromArgb(255, 46, 74, 84) : ColorHelper.FromArgb(255, 226, 241, 244);
        var pressedBackground = dark ? ColorHelper.FromArgb(255, 58, 91, 102) : ColorHelper.FromArgb(255, 205, 230, 235);

        titleBar.BackgroundColor = background;
        titleBar.InactiveBackgroundColor = inactiveBackground;
        titleBar.ButtonBackgroundColor = background;
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
        if (navigation.MenuItems[0] is not NavigationViewItem firstItem ||
            firstItem.Content?.ToString() != Services.L.Text("概览"))
            throw new InvalidOperationException("Navigation labels did not use the selected language.");
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
        if (page.FindName("SettingsTabs") is not SelectorBar { Items.Count: 5 } selectorBar)
            throw new InvalidOperationException("The five settings sections were not created.");
        var sections = new[] { "GeneralSection", "LinksSection", "RulesSection", "DataSection", "AboutSection" };
        for (var index = 0; index < selectorBar.Items.Count; index++)
        {
            selectorBar.SelectedItem = selectorBar.Items[index] as SelectorBarItem;
            for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
            {
                if (page.FindName(sections[sectionIndex]) is not UIElement section ||
                    section.Visibility != (sectionIndex == index ? Visibility.Visible : Visibility.Collapsed))
                    throw new InvalidOperationException("Settings section selection did not update content.");
            }
        }
        selectorBar.SelectedItem = selectorBar.Items[0] as SelectorBarItem;
        if (page.FindName("SettingsLogo") is not Image { Source: Microsoft.UI.Xaml.Media.Imaging.BitmapImage image }
            || !image.UriSource.AbsoluteUri.EndsWith("/Resources/Icons/tai.png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The original Tai settings icon is not configured.");

        if (page.FindName("StartPagePicker") is not ComboBox startPagePicker) return;
        var narrow = page.ActualWidth < 820;
        var expectedRow = narrow ? 1 : 0;
        var expectedColumn = narrow ? 0 : 1;
        if (Grid.GetRow(startPagePicker) != expectedRow || Grid.GetColumn(startPagePicker) != expectedColumn)
            throw new InvalidOperationException($"Settings controls did not reflow at {page.ActualWidth:F0} effective pixels.");
        if (page.FindName("CategoryIntervalPicker") is not ComboBox categoryIntervalPicker ||
            Grid.GetRow(categoryIntervalPicker) != expectedRow ||
            Grid.GetColumn(categoryIntervalPicker) != expectedColumn)
            throw new InvalidOperationException("The category update interval control was not laid out correctly.");
        if (page.FindName("LanguagePicker") is not ComboBox languagePicker ||
            Grid.GetRow(languagePicker) != expectedRow ||
            Grid.GetColumn(languagePicker) != expectedColumn ||
            languagePicker.SelectedIndex != (App.Services.GetRequiredService<IAppConfig>().GetConfig().General.Language switch
            {
                "zh-CN" => 1, "en-US" => 2, _ => 0
            }))
            throw new InvalidOperationException("The language setting did not load or reflow correctly.");
    }

    private static void SeedCategorySmokeApp()
    {
        var appData = App.Services.GetRequiredService<IAppData>();
        if (appData.GetApp("TaiCategorySmokeTest") != null) return;
        var category = App.Services.GetRequiredService<ICategorys>().GetCategories()
            .OrderBy(item => item.Name).First();
        appData.AddApp(new Core.Models.AppModel
        {
            Name = "TaiCategorySmokeTest",
            CategoryID = category.ID,
            File = string.Empty,
            IconFile = string.Empty,
            Description = "分类测试应用"
        });
    }

    private static async Task SeedCategorySmokeWebsiteAsync()
    {
        var webData = App.Services.GetRequiredService<IWebData>();
        const string domain = "tai-smoke.github.com";
        if (webData.GetWebSite(domain) == null)
            webData.AddUrlBrowseTime(new Core.Models.WebPage.Site
            {
                Title = "分类测试网站",
                Url = "https://tai-smoke.github.com/"
            }, 60);

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var site = webData.GetWebSite(domain);
            var category = site == null ? null : webData.GetWebSiteCategory(site.CategoryID);
            if (category?.Name == "开发技术") return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException("A known website was not classified when first recorded.");
    }

    private static async Task ValidateCategoriesPageAsync(Views.CategoriesPage page)
    {
        if (page.FindName("FetchCategoriesButton") is not Button ||
            page.FindName("CategoryActions") is not StackPanel actions ||
            page.FindName("CategoryList") is not ListView categoryList ||
            page.FindName("CategoryMode") is not SelectorBar mode ||
            page.FindName("AppsMode") is not SelectorBarItem appsMode)
            throw new InvalidOperationException("The category update action was not created.");
        mode.SelectedItem = appsMode;
        page.UpdateLayout();

        var expectedRow = page.ActualWidth < 820 ? 1 : 0;
        if (Grid.GetRow(actions) != expectedRow)
            throw new InvalidOperationException("Category actions did not reflow with the window.");

        if (page.Categories.Count < 2)
            throw new InvalidOperationException("The category list did not load.");
        var target = page.Categories[0];
        categoryList.SelectedItem = page.Categories[1];
        categoryList.SelectedItem = target;
        if (!page.CategoryApps.Any(app => app.ProcessName == "TaiCategorySmokeTest"))
            throw new InvalidOperationException("Selecting a category did not display its applications.");

        if (page.FindName("RuleSection") is not Border appDetail ||
            page.FindName("SelectedCategoryNameText") is not TextBlock appDetailTitle)
            throw new InvalidOperationException("The application category detail was not created.");
        await ValidateCategoryScrollAsync(page, categoryList, appDetail, appDetailTitle,
            page.Categories.Last(), page.Categories.Last().DisplayName);
        categoryList.SelectedItem = target;
        if (FindVisualChild<ScrollViewer>(categoryList) is { } appScroll)
            appScroll.ChangeView(null, 0, null, disableAnimation: true);
        await Task.Delay(80);
        await CaptureSmokeScreenshotAsync(page.XamlRoot.Content as FrameworkElement ?? page,
            (int)page.ActualWidth, "CategoriesPage-apps");

        if (page.FindName("WebsitesMode") is not SelectorBarItem websitesMode ||
            page.FindName("WebsiteCategoryList") is not ListView websiteCategoryList ||
            page.FindName("WebsiteDetailSection") is not Border websiteDetail ||
            page.FindName("SelectedWebsiteCategoryNameText") is not TextBlock websiteDetailTitle)
            throw new InvalidOperationException("The website category view was not created.");
        mode.SelectedItem = websitesMode;
        await page.LoadDataTask;
        var websiteCategory = page.WebsiteCategories.FirstOrDefault(item => item.Name == "开发技术");
        if (websiteCategory == null)
            throw new InvalidOperationException("Default website categories were not loaded.");
        websiteCategoryList.SelectedItem = websiteCategory;
        if (!page.CategoryWebsites.Any(site => site.Domain == "tai-smoke.github.com"))
            throw new InvalidOperationException("Selecting a website category did not display its websites.");
        if (Grid.GetRow(websiteDetail) != expectedRow)
            throw new InvalidOperationException("Website categories did not reflow with the window.");
        await ValidateCategoryScrollAsync(page, websiteCategoryList, websiteDetail, websiteDetailTitle,
            page.WebsiteCategories.Last(), page.WebsiteCategories.Last().DisplayName);
        websiteCategoryList.SelectedItem = websiteCategory;
    }

    private static async Task ValidateCategoryScrollAsync(FrameworkElement page, ListView list,
        FrameworkElement detail, TextBlock detailTitle, object lastCategory, string expectedTitle)
    {
        page.UpdateLayout();
        var scroll = FindVisualChild<ScrollViewer>(list);
        if (scroll == null || scroll.ViewportHeight <= 0 || scroll.ViewportHeight > page.ActualHeight ||
            (list.Name == "CategoryList" && scroll.ScrollableHeight <= 0))
            throw new InvalidOperationException("Category lists must have their own constrained scrolling viewport.");
        var canScroll = scroll.ScrollableHeight > 0;
        var before = detailTitle.TransformToVisual(page).TransformPoint(new Windows.Foundation.Point());
        scroll.ChangeView(null, scroll.ScrollableHeight, null, disableAnimation: true);
        await Task.Delay(80);
        list.SelectedItem = lastCategory;
        page.UpdateLayout();
        var after = detailTitle.TransformToVisual(page).TransformPoint(new Windows.Foundation.Point());
        var detailBottom = detail.TransformToVisual(page).TransformPoint(new Windows.Foundation.Point(0, detail.ActualHeight));
        if ((canScroll && scroll.VerticalOffset <= 0) || Math.Abs(after.Y - before.Y) > 0.1 ||
            after.Y < 0 || detailBottom.Y > page.ActualHeight + 0.1 || detailTitle.Text != expectedTitle)
            throw new InvalidOperationException($"{list.Name}: scrolling and selecting the last category must keep its detail visible and stationary. " +
                $"Offset={scroll.VerticalOffset:F1}, title Y={before.Y:F1}/{after.Y:F1}, bottom={detailBottom.Y:F1}/{page.ActualHeight:F1}, title='{detailTitle.Text}', expected='{expectedTitle}'.");
    }

    private static T? FindVisualChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T result) return result;
            if (FindVisualChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }

    private static async Task CaptureSmokeScreenshotAsync(FrameworkElement element, int effectiveWidth, string? name = null)
    {
        RenderTargetBitmap? bitmap = null;
        byte[]? pixels = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            element.UpdateLayout();
            bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(element);
            if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                pixels = (await bitmap.GetPixelsAsync()).ToArray();
                if (pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0)) break;
            }
            await Task.Delay(250);
        }
        if (bitmap == null || pixels == null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0
            || !pixels.Where((_, index) => index % 4 == 3).Any(alpha => alpha > 0))
            throw new InvalidOperationException($"The {effectiveWidth}px responsive layout rendered as a blank image.");

        var folder = await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
        var suffix = name == null ? string.Empty : $"-{name}";
        var file = await folder.CreateFileAsync($"layout-{effectiveWidth}{suffix}.png", CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels);
        await encoder.FlushAsync();
    }

    private static void ValidateStatisticsPage(Views.StatisticsPage page)
    {
        foreach (var period in Enum.GetValues<Services.UsagePeriod>().Reverse())
            page.SelectPeriodForSmokeTest(period);
        foreach (var period in Enum.GetValues<Services.UsagePeriod>())
        {
            page.SelectPeriodForSmokeTest(period);
            page.SelectPeriodForSmokeTest(period);
            if (page.CheckedPeriodCount != 1 || page.SelectedPeriod != period)
                throw new InvalidOperationException("Statistics period selector must keep exactly one option selected.");
            ValidatePeriodVisuals(page);
        }
    }

    private static void ValidateDetailsPage(Views.DetailsPage page)
    {
        foreach (var period in Enum.GetValues<Services.UsagePeriod>().Reverse())
            page.SelectPeriodForSmokeTest(period);
        foreach (var period in Enum.GetValues<Services.UsagePeriod>())
        {
            page.SelectPeriodForSmokeTest(period);
            page.SelectPeriodForSmokeTest(period);
            if (page.CheckedPeriodCount != 1 || page.SelectedPeriod != period)
                throw new InvalidOperationException("Details period selector must keep exactly one option selected.");
            ValidatePeriodVisuals(page);
        }
    }

    private static void ValidatePeriodVisuals(Page page)
    {
        foreach (var name in new[] { "DayButton", "WeekButton", "MonthButton", "YearButton" })
        {
            if (page.FindName(name) is not RadioButton button)
                throw new InvalidOperationException("Period options must use native single selection controls.");
            button.ApplyTemplate();
            if (VisualTreeHelper.GetChild(button, 0) is not Grid root ||
                root.FindName("CheckedSurface") is not Border selectedSurface)
                throw new InvalidOperationException("The period button template did not load.");
            var expected = button.IsChecked == true ? 1d : 0d;
            if (selectedSurface.Opacity != expected)
                throw new InvalidOperationException("Period button selection is not reflected by its visual state.");
            foreach (var state in new[] { "PointerOver", "Pressed", "Disabled", "Normal" })
            {
                if (!VisualStateManager.GoToState(button, state, useTransitions: false) ||
                    selectedSurface.Opacity != expected)
                    throw new InvalidOperationException("Hover and press feedback must preserve the selected period.");
                var expectedOpacity = state == "Disabled" ? 0.4d : 1d;
                if (Math.Abs(root.Opacity - expectedOpacity) > 0.001)
                    throw new InvalidOperationException("Period buttons must show disabled feedback and restore their appearance when enabled.");
            }
        }
    }

    private static void ValidateUsageRanges()
    {
        var day = Services.CoreUsageDataProvider.GetDateRange(Services.UsagePeriod.Day, new DateTime(2026, 1, 1));
        var week = Services.CoreUsageDataProvider.GetDateRange(Services.UsagePeriod.Week, new DateTime(2026, 1, 1));
        var month = Services.CoreUsageDataProvider.GetDateRange(Services.UsagePeriod.Month, new DateTime(2024, 2, 20));
        var year = Services.CoreUsageDataProvider.GetDateRange(Services.UsagePeriod.Year, new DateTime(2026, 9, 12));
        if (day.Start != day.End || day.Start != new DateTime(2026, 1, 1))
            throw new InvalidOperationException("Day range calculation failed.");
        if (week.Start != new DateTime(2025, 12, 29) || week.End != new DateTime(2026, 1, 4))
            throw new InvalidOperationException("Cross-year week range calculation failed.");
        if (month.Start != new DateTime(2024, 2, 1) || month.End != new DateTime(2024, 2, 29))
            throw new InvalidOperationException("Month range calculation failed.");
        if (year.Start != new DateTime(2026, 1, 1) || year.End != new DateTime(2026, 12, 31))
            throw new InvalidOperationException("Year range calculation failed.");
        if (Services.CoreUsageDataProvider.CalculateSharePercent(159, 256) != 62
            || Services.CoreUsageDataProvider.CalculateSharePercent(97, 256) != 38)
            throw new InvalidOperationException("Usage share calculation failed.");
        var reference = new DateTime(2026, 9, 16, 19, 30, 0);
        if (Services.CoreUsageDataProvider.IsFuturePoint(Services.UsagePeriod.Day, reference.Date, 19, reference)
            || !Services.CoreUsageDataProvider.IsFuturePoint(Services.UsagePeriod.Day, reference.Date, 20, reference))
            throw new InvalidOperationException("Future trend point calculation failed.");
    }

    private static async Task ValidateUsageDataAsync()
    {
        var provider = App.Services.GetRequiredService<Services.IUsageDataProvider>();
        var date = DateTime.Today;
        var expected = new Dictionary<Services.UsagePeriod, int>
        {
            [Services.UsagePeriod.Day] = 24,
            [Services.UsagePeriod.Week] = 7,
            [Services.UsagePeriod.Month] = DateTime.DaysInMonth(date.Year, date.Month),
            [Services.UsagePeriod.Year] = 12
        };
        foreach (var pair in expected)
        {
            var snapshot = await provider.GetAsync(pair.Key, date, 2);
            if (snapshot.Trend.Count != pair.Value)
                throw new InvalidOperationException($"{pair.Key} trend expected {pair.Value} points, actual {snapshot.Trend.Count}.");
            if (!provider.TryGetCached(pair.Key, date, 2, out var cached)
                || cached.Trend.Count != pair.Value)
                throw new InvalidOperationException($"{pair.Key} snapshot was not retained in the usage cache.");
            if (snapshot.Apps.Concat(snapshot.Websites).Any(item => !File.Exists(item.IconPath)))
                throw new InvalidOperationException($"{pair.Key} contains an unresolved icon path.");
        }
    }

    private static void ValidateIconFallbacks()
    {
        if (!File.Exists(Services.AppIconResolver.DefaultIconPath))
            throw new InvalidOperationException("The original fallback icon was not published.");

        var corruptPath = Path.Combine(Path.GetTempPath(), $"tai-corrupt-icon-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllText(corruptPath, "not an image");
            var fallback = Services.AppIconResolver.Resolve(corruptPath, @"Z:\missing\app.exe", "Missing", "Missing");
            if (!string.Equals(fallback, Services.AppIconResolver.DefaultIconPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Corrupt icon fallback failed.");

            var executable = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
            {
                var extracted = Services.AppIconResolver.Resolve(null, executable, "TaiSmoke", "Tai");
                if (!File.Exists(extracted))
                    throw new InvalidOperationException("Executable icon extraction failed.");
            }
        }
        finally
        {
            if (File.Exists(corruptPath)) File.Delete(corruptPath);
        }
    }

    private async Task ValidateThemesAsync()
    {
        var general = App.Services.GetRequiredService<IAppConfig>().GetConfig().General;
        var originalTheme = general.Theme;
        try
        {
            foreach (var theme in new[] { 0, 1 })
            {
                general.Theme = theme;
                ApplyAppearance();
                await Task.Delay(350);
                ValidateTitleBar();
                ValidateResolvedPalette(theme == 1);
                await ValidatePointerFeedbackAsync(theme == 1);
                if (Content is FrameworkElement captureRoot)
                    await CaptureSmokeScreenshotAsync(captureRoot, 1240, theme == 1 ? "dark" : "light");
            }

            general.Theme = 2;
            ApplyAppearance();
            await Task.Delay(80);
            ValidateTitleBar();
            if (Content is FrameworkElement root)
                ValidateResolvedPalette(root.ActualTheme == ElementTheme.Dark);
        }
        finally
        {
            general.Theme = originalTheme;
            ApplyAppearance();
        }
    }

    private async Task ValidatePointerFeedbackAsync(bool dark)
    {
        var previousContent = RootFrame.Content;
        var chart = new Controls.UsageTrendChart
        {
            Width = 280, Height = 220,
            Items = new[]
            {
                new Services.TrendPoint("Mon", 3600),
                new Services.TrendPoint("Tue", 1800),
                new Services.TrendPoint("Wed", 0),
                new Services.TrendPoint("Thu", 0, IsFuture: true)
            }
        };
        var primary = new Button
        {
            Content = Services.L.Text("保存"), Width = 140,
            Style = (Style)Application.Current.Resources["TaiPrimaryButtonStyle"]
        };
        var danger = new Button
        {
            Content = Services.L.Text("删除"), Width = 140,
            Style = (Style)Application.Current.Resources["TaiDangerButtonStyle"]
        };
        var host = new StackPanel
        {
            Width = 340, Spacing = 16, Padding = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources["TaiCardBackgroundBrush"]
        };
        host.Children.Add(chart);
        host.Children.Add(primary);
        host.Children.Add(danger);
        try
        {
            RootFrame.Content = host;
            await Task.Delay(80);
            host.UpdateLayout();
            var bars = ((Canvas)chart.Content).Children.OfType<Button>().ToArray();
            if (bars.Length != 3)
                throw new InvalidOperationException("Trend charts must keep past and zero values, and omit future bars.");
            foreach (var bar in bars.Take(2))
                await ValidateButtonFillAsync(bar, host, "Trend bar", chartBar: true);
            await ValidateButtonFillAsync(primary, host, "Primary button", chartBar: false);
            await ValidateButtonFillAsync(danger, host, "Danger button", chartBar: false);

            foreach (var button in bars.Take(2).Concat(new[] { primary, danger }))
                VisualStateManager.GoToState(button, "PointerOver", useTransitions: false);
            await CaptureSmokeScreenshotAsync(host, 1240, dark ? "hover-dark" : "hover-light");

            var firstBar = bars[0];
            var description = Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(firstBar);
            if (ToolTipService.GetToolTip(firstBar) as string != description ||
                firstBar.Flyout is not Flyout { Content: TextBlock text } || text.Text != description)
                throw new InvalidOperationException("Trend bars must retain their duration tooltip and flyout.");
            var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(firstBar);
            ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(
                Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
            await Task.Delay(60);
            if (!firstBar.Flyout.IsOpen)
                throw new InvalidOperationException("Trend bars must remain clickable after hovering.");
            firstBar.Flyout.Hide();
        }
        finally
        {
            RootFrame.Content = previousContent;
        }
    }

    private static async Task ValidateButtonFillAsync(Button button, FrameworkElement host,
        string description, bool chartBar)
    {
        button.ApplyTemplate();
        var expectedFill = ((SolidColorBrush)button.Background).Color;
        var expectedForeground = ((SolidColorBrush)button.Foreground).Color;
        var width = button.ActualWidth;
        var height = button.ActualHeight;
        Windows.UI.Color? normalFill = null;
        Windows.UI.Color? hoverFill = null;
        Windows.UI.Color? pressedFill = null;
        foreach (var state in new[] { "Normal", "PointerOver", "Pressed", "Normal" })
        {
            if (!VisualStateManager.GoToState(button, state, useTransitions: false))
                throw new InvalidOperationException($"{description} is missing its {state} state.");
            await Task.Delay(100);
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(host);
            var pixels = (await bitmap.GetPixelsAsync()).ToArray();
            var point = button.TransformToVisual(host).TransformPoint(
                new Windows.Foundation.Point(chartBar ? width / 2 : 6, height / 2));
            var x = (int)(point.X * bitmap.PixelWidth / host.ActualWidth);
            var y = (int)(point.Y * bitmap.PixelHeight / host.ActualHeight);
            var offset = (y * bitmap.PixelWidth + x) * 4;
            var actualFill = ColorHelper.FromArgb(pixels[offset + 3], pixels[offset + 2],
                pixels[offset + 1], pixels[offset]);
            if (actualFill.A != 255)
                throw new InvalidOperationException($"{description} lost its fill in {state}: expected {expectedFill}, rendered {actualFill}.");
            if (state == "Normal") normalFill = actualFill;
            if (state == "PointerOver") hoverFill = actualFill;
            if (state == "Pressed") pressedFill = actualFill;
            if (state == "Normal" && (Math.Abs(actualFill.R - expectedFill.R) > 3 ||
                Math.Abs(actualFill.G - expectedFill.G) > 3 ||
                Math.Abs(actualFill.B - expectedFill.B) > 3))
                throw new InvalidOperationException($"{description} has the wrong normal fill: expected {expectedFill}, rendered {actualFill}.");
            if (!chartBar && (FindVisualChild<ContentPresenter>(button)?.Foreground is not SolidColorBrush foreground ||
                foreground.Color != expectedForeground))
                throw new InvalidOperationException($"{description} lost its text color in {state}.");
            if (button.ActualWidth != width || button.ActualHeight != height || !button.UseSystemFocusVisuals)
                throw new InvalidOperationException($"{description} must retain its size and keyboard focus visuals.");
        }
        if (normalFill == hoverFill || normalFill == pressedFill || hoverFill == pressedFill)
            throw new InvalidOperationException($"{description} must visibly change between normal, hover and pressed states.");
    }

    private static void ValidateResolvedPalette(bool dark)
    {
        var page = GetBrushColor("TaiPageBackgroundBrush");
        var card = GetBrushColor("TaiCardBackgroundBrush");
        var primary = GetBrushColor("TaiPrimaryTextBrush");
        var secondary = GetBrushColor("TaiSecondaryTextBrush");
        var accent = GetBrushColor("TaiAccentBrush");
        var accentFill = GetBrushColor("TaiAccentFillBrush");
        var accentSoft = GetBrushColor("TaiAccentSoftBrush");
        var expectedAccent = ParseColor(dark ? "#7DD3FC" : "#155E75", Colors.Transparent);
        var expectedFill = ParseColor(dark ? "#207F9D" : "#155E75", Colors.Transparent);

        if (accent != expectedAccent || accentFill != expectedFill)
            throw new InvalidOperationException($"The {(dark ? "dark" : "light")} fixed accent palette was not applied.");

        RequireContrast("Primary text on card", primary, card, 7.0);
        RequireContrast("Secondary text on page", secondary, page, 4.5);
        RequireContrast("Accent text and chart lines on card", accent, card, 4.5);
        RequireContrast("Accent text on accent soft surface", accent, accentSoft, 4.5);
        RequireContrast("Primary button text", Colors.White, accentFill, 4.5);
        RequireContrast("Accent fill boundary on card", accentFill, card, 3.0);
        RequireContrast("Info text on info surface", GetBrushColor("TaiInfoBrush"), GetBrushColor("TaiInfoSoftBrush"), 4.5);
        RequireContrast("Warning text on warning surface", GetBrushColor("TaiWarningBrush"), GetBrushColor("TaiWarningSoftBrush"), 4.5);
        RequireContrast("Danger text on danger surface", GetBrushColor("TaiDangerBrush"), GetBrushColor("TaiDangerSoftBrush"), 4.5);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
