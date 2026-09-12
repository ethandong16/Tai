using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tai.WinUI.Services;

namespace Tai.WinUI.Views;

public sealed partial class WebsiteDetailPage : Page
{
    public WebsiteDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataContext = e.Parameter is DetailRow row
            ? row
            : new DetailRow(0, "网站", "0分钟", 0, 0, AppIconResolver.DefaultIconPath, "#12966F", "未分类", "网站");
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true) Frame.GoBack();
    }
}
