using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tai.WinUI.Services;
using Tai.WinUI.ViewModels;

namespace Tai.WinUI.Views;

public sealed partial class StatisticsPage : Page
{
    private readonly MainViewModel _viewModel;
    private UsagePeriod _period = UsagePeriod.Day;
    private bool _ready;

    public StatisticsPage()
    {
        _viewModel = new MainViewModel(App.Services.GetService<IUsageDataProvider>(), dashboardMode: false);
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DataContext = _viewModel;
        DatePicker.Date = DateTimeOffset.Now;
        Loaded += StatisticsPage_Loaded;
    }

    private void StatisticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        _ready = true;
        _ = ReloadAsync();
        _ = PreloadPeriodsAsync();
    }

    private async Task PreloadPeriodsAsync()
    {
        var provider = App.Services.GetService<IUsageDataProvider>();
        if (provider == null) return;

        try
        {
            await provider.PreloadAsync(DatePicker.Date?.DateTime ?? DateTime.Today);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
        }
    }

    private void PeriodButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string value }
            || !Enum.TryParse<UsagePeriod>(value, out var period) || period == _period) return;
        _period = period;
        if (_ready) _ = ReloadAsync();
    }

    private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_ready && args.NewDate.HasValue)
        {
            _ = ReloadAsync();
            _ = PreloadPeriodsAsync();
        }
    }

    private Task ReloadAsync()
    {
        var date = DatePicker.Date?.DateTime ?? DateTime.Today;
        return _viewModel.LoadPeriodAsync(_period, date);
    }

    internal int CheckedPeriodCount =>
        new[] { DayButton, WeekButton, MonthButton, YearButton }.Count(button => button.IsChecked == true);

    internal int TrendPointCount => _viewModel.Trend.Count;

    internal UsagePeriod SelectedPeriod => _period;

    internal void SelectPeriodForSmokeTest(UsagePeriod period)
    {
        var button = period switch
        {
            UsagePeriod.Week => WeekButton,
            UsagePeriod.Month => MonthButton,
            UsagePeriod.Year => YearButton,
            _ => DayButton
        };
        var peer = new Microsoft.UI.Xaml.Automation.Peers.RadioButtonAutomationPeer(button);
        ((Microsoft.UI.Xaml.Automation.Provider.ISelectionItemProvider)peer).Select();
    }
}
