using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using WinRT.Interop;

namespace Tai.WinUI;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        var titleBar = new Grid
        {
            Height = 32,
            Padding = new Thickness(16, 0, 16, 0),
            Background = new SolidColorBrush(ColorHelper.FromArgb(0, 246, 247, 251))
        };
        titleBar.Children.Add(new TextBlock
        {
            Text = "Tai   时间统计",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 24, 33, 47))
        });

        var rootFrame = new Frame();
        var root = new Grid { Background = new SolidColorBrush(ColorHelper.FromArgb(255, 246, 247, 251)) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(titleBar, 0);
        Grid.SetRow(rootFrame, 1);
        root.Children.Add(titleBar);
        root.Children.Add(rootFrame);
        Content = root;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "Tai";
        appWindow.Resize(new Windows.Graphics.SizeInt32(1240, 820));

        try
        {
            rootFrame.Navigate(typeof(MainPage));
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            rootFrame.Content = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "Tai 界面加载失败，请查看 Log/startup.log。",
                Margin = new Microsoft.UI.Xaml.Thickness(32),
                FontSize = 16
            };
        }

        Closed += (_, _) => App.Services.GetService<IMain>()?.Exit();
    }
}
