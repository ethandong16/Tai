using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using WinRT.Interop;

namespace Tai.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "Tai";
        appWindow.Resize(new Windows.Graphics.SizeInt32(1240, 820));

        RootFrame.Navigate(typeof(MainPage));

        Closed += (_, _) => App.Services.GetService<IMain>()?.Exit();
    }

    public void ShowStartupError()
    {
        StartupError.IsOpen = true;
    }

    internal async Task VerifyPagesAsync()
    {
        Type[] pages = [typeof(Views.DashboardPage), typeof(Views.StatisticsPage),
            typeof(Views.DetailsPage), typeof(Views.CategoriesPage), typeof(Views.SettingsPage),
            typeof(Views.AppDetailPage), typeof(Views.WebsiteDetailPage)];
        foreach (var page in pages)
        {
            if (!RootFrame.Navigate(page)) throw new InvalidOperationException($"Cannot navigate to {page.Name}");
            // Allow Loaded, bindings and layout to run before checking the next page.
            await Task.Delay(150);
        }
    }
}
