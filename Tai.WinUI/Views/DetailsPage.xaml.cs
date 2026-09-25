using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tai.WinUI.Services;
using Tai.WinUI.ViewModels;

namespace Tai.WinUI.Views;

public sealed partial class DetailsPage : Page
{
    private readonly IUsageDataProvider? _provider;
    private readonly List<DetailRow> _allRows = new();
    private CancellationTokenSource? _loadCancellation;
    private UsagePeriod _period = UsagePeriod.Day;
    private bool _ready;

    public ObservableCollection<DetailRow> Rows { get; } = new();

    public DetailsPage()
    {
        _provider = App.Services.GetService<IUsageDataProvider>();
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        DatePicker.Date = DateTimeOffset.Now;
        Loaded += DetailsPage_Loaded;
        Unloaded += (_, _) => _loadCancellation?.Cancel();
    }

    private void DetailsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_ready) return;
        _ready = true;
        _ = LoadRowsAsync();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        TypeFilter.SelectedIndex = (e.Parameter as string) switch { "应用" => 1, "网站" => 2, _ => 0 };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_ready) ApplyTypeFilter();
    }

    private async Task LoadRowsAsync()
    {
        if (_provider == null) return;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        var cancellation = _loadCancellation = new CancellationTokenSource();
        LoadStatusText.Text = "正在读取...";

        try
        {
            var date = DatePicker.Date?.DateTime ?? DateTime.Today;
            var snapshot = await _provider.GetAsync(_period, date, take: 0, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            var nextRows = snapshot.Apps.Select(item => new DetailRow(item, snapshot.RangeText))
                .Concat(snapshot.Websites.Select(item => new DetailRow(item, snapshot.RangeText)))
                .OrderByDescending(item => item.Seconds)
                .ToList();
            _allRows.Clear();
            _allRows.AddRange(nextRows);
            RangeText.Text = snapshot.RangeText;
            ApplyTypeFilter();
            LoadStatusText.Text = $"{DateTime.Now:HH:mm} 更新";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            LoadStatusText.Text = "读取失败，已保留上次结果";
        }
    }

    private void PeriodButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked || clicked.Tag is not string value) return;
        SelectPeriod(clicked, value);
        if (_ready) _ = LoadRowsAsync();
    }

    private void SelectPeriod(ToggleButton clicked, string value)
    {
        foreach (var button in new[] { DayButton, WeekButton, MonthButton, YearButton })
            button.IsChecked = ReferenceEquals(button, clicked);
        if (!Enum.TryParse(value, out _period)) _period = UsagePeriod.Day;
    }

    private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_ready && args.NewDate.HasValue) _ = LoadRowsAsync();
    }

    private void RowsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DetailRow row)
            Frame?.Navigate(row.Kind == "网站" ? typeof(WebsiteDetailPage) : typeof(AppDetailPage), row);
    }

    private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) ApplyTypeFilter();
    }

    private void ApplyTypeFilter()
    {
        var selected = (TypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filtered = string.IsNullOrWhiteSpace(selected) || selected == "全部"
            ? _allRows
            : _allRows.Where(row => row.Kind == selected).ToList();

        Rows.Clear();
        var search = SearchBox.Text.Trim();
        foreach (var row in filtered.Where(row => string.IsNullOrEmpty(search)
                     || row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                     || row.Category.Contains(search, StringComparison.OrdinalIgnoreCase))) Rows.Add(row);
        ResultCountText.Text = Rows.Count == 0 ? "此时间范围内暂无记录" : $"共 {Rows.Count} 条记录";
    }

    internal int CheckedPeriodCount =>
        new[] { DayButton, WeekButton, MonthButton, YearButton }.Count(button => button.IsChecked == true);

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

public sealed class DetailRow
{
    public DetailRow(UsageItem item, string rangeText)
        : this(item.Id, item.Name, item.Duration, item.Seconds, item.Percent, item.IconPath, item.AccentHex, item.Category, item.Kind, rangeText)
    {
    }

    public DetailRow(
        int id,
        string name,
        string duration,
        int seconds,
        int percent,
        string iconPath,
        string accent,
        string category,
        string kind,
        string rangeText = "所选范围")
    {
        Id = id;
        Name = name;
        Duration = duration;
        Seconds = seconds;
        Percent = percent;
        IconPath = iconPath;
        AccentHex = accent;
        AccentBrush = new SolidColorBrush(ParseColor(accent));
        Category = category;
        Kind = kind;
        RangeText = rangeText;
    }

    public int Id { get; }
    public string Name { get; }
    public string Duration { get; }
    public int Seconds { get; }
    public int Percent { get; }
    public string PercentText => Seconds > 0 && Percent == 0 ? "<1%" : $"{Percent}%";
    public string IconPath { get; }
    public string AccentHex { get; }
    public SolidColorBrush AccentBrush { get; }
    public string Category { get; }
    public string Kind { get; }
    public string RangeText { get; }
    public ImageSource Icon => new BitmapImage(new Uri(IconPath, UriKind.Absolute));

    private static Windows.UI.Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        if (hex.Length != 6) return ColorHelper.FromArgb(255, 73, 105, 216);
        return ColorHelper.FromArgb(255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }
}
