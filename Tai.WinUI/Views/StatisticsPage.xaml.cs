using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        DataContext = _viewModel;
        DatePicker.Date = DateTimeOffset.Now;
        Loaded += StatisticsPage_Loaded;
    }

    private void StatisticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        _ready = true;
        _ = ReloadAsync();
    }

    private void PeriodButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not string value) return;
        SelectPeriod(clicked, value);
        if (_ready) _ = ReloadAsync();
    }

    private void SelectPeriod(ToggleButton clicked, string value)
    {
        var buttons = new[] { DayButton, WeekButton, MonthButton, YearButton };
        foreach (var button in buttons) button.IsChecked = ReferenceEquals(button, clicked);
        if (!Enum.TryParse(value, out _period)) _period = UsagePeriod.Day;
    }

    private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_ready && args.NewDate.HasValue) _ = ReloadAsync();
    }

    private Task ReloadAsync()
    {
        var date = DatePicker.Date?.DateTime ?? DateTime.Today;
        return _viewModel.LoadPeriodAsync(_period, date);
    }

    internal int CheckedPeriodCount =>
        new[] { DayButton, WeekButton, MonthButton, YearButton }.Count(button => button.IsChecked == true);

    internal int TrendPointCount => _viewModel.Trend.Count;

    internal void SelectPeriodForSmokeTest(UsagePeriod period)
    {
        var button = period switch
        {
            UsagePeriod.Week => WeekButton,
            UsagePeriod.Month => MonthButton,
            UsagePeriod.Year => YearButton,
            _ => DayButton
        };
        SelectPeriod(button, period.ToString());
    }
}
