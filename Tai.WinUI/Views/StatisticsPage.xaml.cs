using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using Tai.WinUI.Services;
using Tai.WinUI.ViewModels;

namespace Tai.WinUI.Views;

public sealed partial class StatisticsPage : Page
{
    public StatisticsPage()
    {
        InitializeComponent();
        DataContext = new MainViewModel(App.Services.GetService<IUsageDataProvider>());
    }
}
