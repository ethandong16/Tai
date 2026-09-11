using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace Tai.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly double _rasterizationScale;
    private readonly Windows.Graphics.SizeInt32 _workAreaSize;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _rasterizationScale = Math.Max(1d, GetDpiForWindow(hwnd) / 96d);
        var workArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
        _workAreaSize = new Windows.Graphics.SizeInt32(workArea.Width, workArea.Height);
        _appWindow.Title = "Tai";
        ResizeForEffectiveSize(1240, 820, constrainToWorkArea: true);

        RootFrame.Navigate(typeof(MainPage));

        Closed += (_, _) => App.Services.GetService<IMain>()?.Exit();
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
            await Task.Delay(150);

            if (!RootFrame.Navigate(typeof(MainPage))) throw new InvalidOperationException("Cannot navigate to MainPage");
            await Task.Delay(150);
            RootFrame.UpdateLayout();
            if (RootFrame.Content is not MainPage shell) throw new InvalidOperationException("MainPage did not load");
            ValidateShellLayout(shell);

            foreach (var page in pages)
            {
                if (!RootFrame.Navigate(page)) throw new InvalidOperationException($"Cannot navigate to {page.Name}");
                // Allow adaptive states, bindings and layout to settle before checking the next page.
                await Task.Delay(150);
                RootFrame.UpdateLayout();
                if (RootFrame.Content is not FrameworkElement content) throw new InvalidOperationException($"{page.Name} did not load");
                ValidatePageLayout(content);
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
            throw new InvalidOperationException($"Navigation layout mismatch at {shell.ActualWidth:F0} effective pixels.");
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
