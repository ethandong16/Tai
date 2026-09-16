using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Tai.WinUI.ViewModels;
using Core.Servicers.Interfaces;

namespace Tai.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly MainViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        var itemCount = Math.Max(1,
            App.Services.GetService<IAppConfig>()?.GetConfig()?.General?.IndexPageFrequentUseNum ?? 4);
        _viewModel = new MainViewModel(
            App.Services.GetService<Tai.WinUI.Services.IUsageDataProvider>(),
            dashboardTake: itemCount);
        DataContext = _viewModel;
        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadDashboardAsync();
        UpdateWebsiteState();
    }

    private void ViewAll_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(DetailsPage));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadDashboardAsync();
        UpdateWebsiteState();
    }

    private void UpdateWebsiteState()
    {
        var hasWebsites = _viewModel.Websites.Count > 0;
        WebsiteList.Visibility = hasWebsites ? Visibility.Visible : Visibility.Collapsed;
        WebsiteEmptyState.Visibility = hasWebsites ? Visibility.Collapsed : Visibility.Visible;
        WebsiteViewButton.Visibility = hasWebsites ? Visibility.Visible : Visibility.Collapsed;
        if (hasWebsites) return;

        if (_viewModel.LastUpdated.StartsWith("数据读取失败", StringComparison.Ordinal))
        {
            WebsiteStateTitle.Text = "网站数据暂时无法读取";
            WebsiteStateDescription.Text = "Tai 已保留上次结果，请稍后刷新；如果问题持续，请检查数据目录。";
            WebsiteStateAction.Content = "检查设置";
            return;
        }

        var webEnabled = App.Services.GetService<IAppConfig>()?.GetConfig()?.General?.IsWebEnabled == true;
        WebsiteStateTitle.Text = webEnabled ? "今天还没有网站记录" : "网站统计尚未开启";
        WebsiteStateDescription.Text = webEnabled
            ? "如果你刚刚使用过浏览器，请检查 Chrome 或 Edge 插件是否已连接。"
            : "开启网站统计并连接浏览器插件后，这里会显示今日浏览时长。";
        WebsiteStateAction.Content = webEnabled ? "检查网站统计设置" : "开启网站统计";
    }

    private void WebsiteSettings_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SettingsPage));
    }
}
