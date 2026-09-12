using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Tai.WinUI.ViewModels;

namespace Tai.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly MainViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(App.Services.GetService<Tai.WinUI.Services.IUsageDataProvider>());
        DataContext = _viewModel;
        Loaded += (_, _) => _ = _viewModel.LoadDashboardAsync();
    }

    private void ViewAll_Click(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(DetailsPage));
    }
}
