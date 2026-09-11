using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tai.WinUI.Views;

namespace Tai.WinUI;

public sealed partial class MainPage : Page
{
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
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
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
