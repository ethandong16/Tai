using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tai.WinUI.Infrastructure;
using Tai.WinUI.Services;

namespace Tai.WinUI.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsageDataProvider? _dataProvider;
    private readonly bool _dashboardMode;
    private CancellationTokenSource? _loadCancellation;
    private UsagePeriod _period = UsagePeriod.Day;
    private DateTime _anchorDate = DateTime.Today;

    public MainViewModel(IUsageDataProvider? dataProvider = null, bool dashboardMode = true)
    {
        _dataProvider = dataProvider;
        _dashboardMode = dashboardMode;
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync());
    }

    public ObservableCollection<UsageItem> Apps { get; } = new();
    public ObservableCollection<UsageItem> Websites { get; } = new();
    public ObservableCollection<TrendPoint> Trend { get; } = new();
    public ObservableCollection<CategoryUsage> Categories { get; } = new();
    public RelayCommand RefreshCommand { get; }

    private string _appUsage = "0分钟";
    public string AppUsage { get => _appUsage; private set => SetField(ref _appUsage, value); }
    public string TodayUsage => AppUsage;

    private string _websiteUsage = "0分钟";
    public string WebsiteUsage { get => _websiteUsage; private set => SetField(ref _websiteUsage, value); }

    private string _appCountText = "0 个";
    public string AppCountText { get => _appCountText; private set => SetField(ref _appCountText, value); }

    private string _websiteCountText = "0 个";
    public string WebsiteCountText { get => _websiteCountText; private set => SetField(ref _websiteCountText, value); }

    private string _longestAppName = "暂无数据";
    public string LongestAppName { get => _longestAppName; private set => SetField(ref _longestAppName, value); }

    private string _longestAppDuration = "0分钟";
    public string LongestAppDuration { get => _longestAppDuration; private set => SetField(ref _longestAppDuration, value); }

    private string _rangeText = string.Empty;
    public string RangeText { get => _rangeText; private set => SetField(ref _rangeText, value); }

    private string _peakLabel = "暂无数据";
    public string PeakLabel { get => _peakLabel; private set => SetField(ref _peakLabel, value); }

    private string _peakDuration = "0分钟";
    public string PeakDuration { get => _peakDuration; private set => SetField(ref _peakDuration, value); }

    private string _lastUpdated = "等待更新";
    public string LastUpdated { get => _lastUpdated; private set => SetField(ref _lastUpdated, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }

    private bool _hasData;
    public bool HasData { get => _hasData; private set => SetField(ref _hasData, value); }

    public async Task LoadDashboardAsync()
    {
        if (_dataProvider == null) return;
        var cancellation = BeginLoad();
        try
        {
            var today = await _dataProvider.GetTodayAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var week = await _dataProvider.GetAsync(UsagePeriod.Week, DateTime.Today, 8, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            ApplySnapshot(today, includeTrend: false);
            Replace(Trend, week.Trend);
            RangeText = week.RangeText;
            LastUpdated = $"{DateTime.Now:HH:mm} 更新";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            LastUpdated = "数据读取失败，已保留上次结果";
        }
        finally
        {
            CompleteLoad(cancellation);
        }
    }

    public async Task LoadPeriodAsync(UsagePeriod period, DateTime anchorDate)
    {
        _period = period;
        _anchorDate = anchorDate.Date;
        if (_dataProvider == null) return;
        var cancellation = BeginLoad();
        try
        {
            var snapshot = await _dataProvider.GetAsync(period, anchorDate, 8, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            ApplySnapshot(snapshot, includeTrend: true);
            LastUpdated = $"{DateTime.Now:HH:mm} 更新";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            LastUpdated = "数据读取失败，已保留上次结果";
        }
        finally
        {
            CompleteLoad(cancellation);
        }
    }

    private Task RefreshAsync() => _dashboardMode
        ? LoadDashboardAsync()
        : LoadPeriodAsync(_period, _anchorDate);

    private CancellationTokenSource BeginLoad()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        IsLoading = true;
        return _loadCancellation;
    }

    private void CompleteLoad(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_loadCancellation, cancellation)) return;
        IsLoading = false;
    }

    private void ApplySnapshot(UsageSnapshot snapshot, bool includeTrend)
    {
        Replace(Apps, snapshot.Apps);
        Replace(Websites, snapshot.Websites);
        Replace(Categories, snapshot.Categories);
        if (includeTrend) Replace(Trend, snapshot.Trend);
        AppUsage = Format(snapshot.TotalAppSeconds);
        OnPropertyChanged(nameof(TodayUsage));
        WebsiteUsage = Format(snapshot.TotalWebSeconds);
        AppCountText = $"{snapshot.AppCount} 个";
        WebsiteCountText = $"{snapshot.WebsiteCount} 个";
        LongestAppName = snapshot.LongestAppName;
        LongestAppDuration = snapshot.LongestAppDuration;
        RangeText = snapshot.RangeText;
        PeakLabel = snapshot.PeakLabel;
        PeakDuration = snapshot.PeakDuration;
        HasData = snapshot.TotalAppSeconds + snapshot.TotalWebSeconds > 0;
    }

    private static string Format(int seconds) => seconds > 0 ? Core.Librarys.Time.ToString(seconds) : "0分钟";

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class UsageItem
{
    public UsageItem(
        int id,
        string name,
        string duration,
        int seconds,
        int percent,
        string iconPath,
        string accent,
        string category,
        string kind)
    {
        Id = id;
        Name = name;
        Duration = duration;
        Seconds = seconds;
        Percent = percent;
        IconPath = iconPath;
        AccentHex = accent;
        Category = category;
        Kind = kind;
    }

    public int Id { get; }
    public string Name { get; }
    public string Duration { get; }
    public int Seconds { get; }
    public int Percent { get; }
    public string PercentText => $"{Percent}%";
    public string IconPath { get; }
    public string AccentHex { get; }
    public string Category { get; }
    public string Kind { get; }

    private SolidColorBrush? _accent;
    public SolidColorBrush Accent => _accent ??= new SolidColorBrush(ParseColor(AccentHex));

    private ImageSource? _icon;
    public ImageSource Icon => _icon ??= CreateImage(IconPath);

    private static ImageSource CreateImage(string path)
    {
        try
        {
            return new BitmapImage(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            return new BitmapImage(new Uri(AppIconResolver.DefaultIconPath, UriKind.Absolute));
        }
    }

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

public sealed class CategoryUsage
{
    public CategoryUsage(string name, string duration, int percent, string accent)
    {
        Name = name;
        Duration = duration;
        Percent = percent;
        AccentHex = accent;
    }

    public string Name { get; }
    public string Duration { get; }
    public int Percent { get; }
    public string PercentText => $"{Percent}%";
    public string AccentHex { get; }
    private SolidColorBrush? _accent;
    public SolidColorBrush Accent => _accent ??= new SolidColorBrush(ParseColor(AccentHex));

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
