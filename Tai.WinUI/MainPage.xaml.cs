using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Core.Servicers.Interfaces;
using Tai.WinUI.Views;

namespace Tai.WinUI;

public sealed partial class MainPage : Page
{
    private bool _initializationStarted;
    private readonly Dictionary<string, (Type Page, string Title)> _routes = new()
    {
        ["Dashboard"] = (typeof(DashboardPage), "概览"),
        ["Statistics"] = (typeof(StatisticsPage), "统计"),
        ["Details"] = (typeof(DetailsPage), "详细"),
        ["Categories"] = (typeof(CategoriesPage), "分类"),
        ["Settings"] = (typeof(SettingsPage), "设置")
    };

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
        SizeChanged += (_, _) => ApplyResponsiveNavigation();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveNavigation();
        if (_initializationStarted) return;
        _initializationStarted = true;

        try
        {
            RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
            if (ContentFrame.CurrentSourcePageType == null)
            {
                ContentFrame.Navigate(typeof(DashboardPage));
            }

            await App.CoreReady;
            var configuredIndex = Math.Clamp(
                App.Services.GetRequiredService<IAppConfig>().GetConfig()?.General?.StartPage ?? 0,
                0,
                3);
            RootNavigation.SelectedItem = RootNavigation.MenuItems[configuredIndex];
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            ContentFrame.Content = new TextBlock
            {
                Text = "概览页面加载失败，请查看 Log/startup.log。",
                Margin = new Thickness(32),
                FontSize = 16
            };
        }
    }

    private void ApplyResponsiveNavigation()
    {
        if (RootNavigation == null || PageHeader == null || ActualWidth <= 0) return;
        if (ActualWidth >= 900)
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Left;
            RootNavigation.IsPaneOpen = true;
            RootNavigation.IsPaneToggleButtonVisible = false;
            PageHeader.Margin = new Thickness(24, 20, 24, 12);
            PageHeader.Padding = new Thickness(0, 0, 0, 16);
        }
        else if (ActualWidth >= 640)
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftCompact;
            RootNavigation.IsPaneOpen = false;
            RootNavigation.IsPaneToggleButtonVisible = true;
            PageHeader.Margin = new Thickness(20, 16, 20, 10);
            PageHeader.Padding = new Thickness(0, 0, 0, 16);
        }
        else
        {
            RootNavigation.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;
            RootNavigation.IsPaneOpen = false;
            RootNavigation.IsPaneToggleButtonVisible = true;
            PageHeader.Margin = new Thickness(12, 12, 12, 8);
            PageHeader.Padding = new Thickness(44, 0, 0, 16);
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string route && _routes.TryGetValue(route, out var page))
        {
            PageTitle.Text = page.Title;
            if (ContentFrame.CurrentSourcePageType != page.Page)
            {
                ContentFrame.Navigate(page.Page);
            }
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        var route = e.SourcePageType == typeof(AppDetailPage) || e.SourcePageType == typeof(WebsiteDetailPage)
            ? "Details"
            : _routes.FirstOrDefault(pair => pair.Value.Page == e.SourcePageType).Key;
        if (string.IsNullOrEmpty(route)) return;

        var item = RootNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .Concat(RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
            .FirstOrDefault(candidate => string.Equals(candidate.Tag as string, route, StringComparison.Ordinal));
        if (item != null && !ReferenceEquals(RootNavigation.SelectedItem, item))
        {
            RootNavigation.SelectedItem = item;
        }
    }
}
