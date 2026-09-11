using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Tai.WinUI.Infrastructure;
using Tai.WinUI.Services;

namespace Tai.WinUI.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsageDataProvider? _dataProvider;

    public MainViewModel(IUsageDataProvider? dataProvider = null)
    {
        _dataProvider = dataProvider;
        Apps = new ObservableCollection<UsageItem>
        {
            new("Visual Studio", "8小时13分", 82, "&#xE943;", "#7B61FF", "开发"),
            new("Google Chrome", "6小时31分", 65, "&#xE774;", "#3B82F6", "浏览"),
            new("Microsoft Edge", "4小时30分", 46, "&#xE774;", "#0EA5A4", "浏览"),
            new("Windows Terminal", "2小时08分", 23, "&#xE756;", "#334155", "工具")
        };

        Websites = new ObservableCollection<UsageItem>
        {
            new("GitHub", "4小时23分", 72, "&#xE77B;", "#24292F", "开发"),
            new("YouTube", "2小时15分", 41, "&#xE714;", "#E5484D", "娱乐"),
            new("V2EX", "1小时25分", 28, "&#xE8FD;", "#12966F", "社区")
        };

        WeeklyUsage = new ObservableCollection<WeeklyUsageItem>
        {
            new("周一", 38, "3小时48分"),
            new("周二", 62, "6小时12分"),
            new("周三", 46, "4小时36分"),
            new("周四", 78, "7小时48分"),
            new("周五", 66, "6小时36分"),
            new("周六", 31, "3小时06分"),
            new("周日", 54, "5小时24分")
        };

        Categories = new ObservableCollection<CategoryUsage>
        {
            new("开发", "12小时40分", 68, "#4969D8"),
            new("浏览", "8小时21分", 45, "#12966F"),
            new("娱乐", "3小时48分", 21, "#C77912"),
            new("工具", "2小时17分", 12, "#8B5CF6")
        };

        RefreshCommand = new RelayCommand(_ => _ = LoadFromProviderAsync());

        if (_dataProvider != null)
        {
            _ = LoadFromProviderAsync();
        }
    }

    public ObservableCollection<UsageItem> Apps { get; }
    public ObservableCollection<UsageItem> Websites { get; }
    public ObservableCollection<WeeklyUsageItem> WeeklyUsage { get; }
    public ObservableCollection<CategoryUsage> Categories { get; }
    public RelayCommand RefreshCommand { get; }

    private string _todayUsage = "8小时42分";
    public string TodayUsage { get => _todayUsage; private set { _todayUsage = value; OnPropertyChanged(); } }
    private string _websiteUsage = "5小时16分";
    public string WebsiteUsage { get => _websiteUsage; private set { _websiteUsage = value; OnPropertyChanged(); } }
    private string _appCountText = "23 个";
    public string AppCountText { get => _appCountText; private set { _appCountText = value; OnPropertyChanged(); } }
    private string _websiteCountText = "38 个";
    public string WebsiteCountText { get => _websiteCountText; private set { _websiteCountText = value; OnPropertyChanged(); } }
    private string _longestAppName = "Visual Studio";
    public string LongestAppName { get => _longestAppName; private set { _longestAppName = value; OnPropertyChanged(); } }
    private string _longestAppDuration = "8小时13分";
    public string LongestAppDuration { get => _longestAppDuration; private set { _longestAppDuration = value; OnPropertyChanged(); } }

    private async Task LoadFromProviderAsync()
    {
        try
        {
            var snapshot = await _dataProvider!.GetTodayAsync();
            Apps.Clear();
            foreach (var item in snapshot.Apps) Apps.Add(item);
            Websites.Clear();
            foreach (var item in snapshot.Websites) Websites.Add(item);
            TodayUsage = snapshot.TotalAppSeconds > 0 ? Core.Librarys.Time.ToString(snapshot.TotalAppSeconds) : "0分钟";
            WebsiteUsage = snapshot.TotalWebSeconds > 0 ? Core.Librarys.Time.ToString(snapshot.TotalWebSeconds) : "0分钟";
            AppCountText = $"{snapshot.AppCount} 个";
            WebsiteCountText = $"{snapshot.WebsiteCount} 个";
            LongestAppName = snapshot.LongestAppName;
            LongestAppDuration = snapshot.LongestAppDuration;
            LastUpdated = $"{DateTime.Now:HH:mm} 更新";
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            LastUpdated = "数据读取失败";
        }
    }

    private string _lastUpdated = "今天 18:42 更新";
    public string LastUpdated
    {
        get => _lastUpdated;
        set
        {
            if (_lastUpdated == value) return;
            _lastUpdated = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class UsageItem
{
    public UsageItem(string name, string duration, int percent, string glyph, string accent, string category)
    {
        Name = name;
        Duration = duration;
        Percent = percent;
        Glyph = glyph;
        AccentHex = accent;
        Category = category;
    }

    public string Name { get; }
    public string Duration { get; }
    public int Percent { get; }
    public string Glyph { get; }
    public string AccentHex { get; }
    // Snapshots are built on a worker thread; create WinUI objects when the UI binds them.
    private SolidColorBrush? _accent;
    public SolidColorBrush Accent => _accent ??= new SolidColorBrush(ParseColor(AccentHex));
    public string Category { get; }

    private static Windows.UI.Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return ColorHelper.FromArgb(255, r, g, b);
    }
}

public sealed record WeeklyUsageItem(string Day, double Height, string Duration);
public sealed class CategoryUsage
{
    public CategoryUsage(string name, string duration, int percent, string accent)
    {
        Name = name;
        Duration = duration;
        Percent = percent;
        AccentHex = accent;
        Accent = new SolidColorBrush(ParseColor(accent));
    }

    public string Name { get; }
    public string Duration { get; }
    public int Percent { get; }
    public string PercentText => $"{Percent}%";
    public string AccentHex { get; }
    public SolidColorBrush Accent { get; }

    private static Windows.UI.Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        return ColorHelper.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }
}
