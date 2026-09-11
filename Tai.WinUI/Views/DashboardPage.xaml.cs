using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Tai.WinUI.ViewModels;

namespace Tai.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        DataContext = new MainViewModel(App.Services.GetService<Tai.WinUI.Services.IUsageDataProvider>());
    }

    private void ViewAll_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(DetailsPage));
    }
}
