using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

        try
        {
            RootFrame.Navigate(typeof(MainPage));
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            RootFrame.Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "Tai 界面加载失败，请查看 Log/startup.log。",
                Margin = new Microsoft.UI.Xaml.Thickness(32),
                FontSize = 16
            };
        }

        Closed += (_, _) => App.Services.GetService<IMain>()?.Exit();
    }
}
