using Core.Librarys;
using Core.Models;
using Core.Servicers.Interfaces;

namespace Tai.WinUI.Services;

public enum UsagePeriod
{
    Day,
    Week,
    Month,
    Year
}

public readonly record struct UsageDateRange(DateTime Start, DateTime End)
{
    public string DisplayText => Start.Date == End.Date
        ? Start.ToString("yyyy年M月d日")
        : Start.Year == End.Year
            ? $"{Start:yyyy年M月d日} - {End:M月d日}"
            : $"{Start:yyyy年M月d日} - {End:yyyy年M月d日}";
}

public sealed record TrendPoint(string Label, double Seconds)
{
    public string Duration => FormatDuration(Seconds);

    internal static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "0分钟";
        return Time.ToString((int)Math.Round(seconds));
    }
}

public interface IUsageDataProvider
{
    Task<UsageSnapshot> GetAsync(
        UsagePeriod period,
        DateTime anchorDate,
        int take = 8,
        CancellationToken cancellationToken = default);

    Task<UsageSnapshot> GetTodayAsync(CancellationToken cancellationToken = default);
}

public sealed class UsageSnapshot
{
    public UsagePeriod Period { get; init; }
    public DateTime AnchorDate { get; init; }
    public UsageDateRange DateRange { get; init; }
    public string RangeText => DateRange.DisplayText;
    public IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> Apps { get; init; } = Array.Empty<Tai.WinUI.ViewModels.UsageItem>();
    public IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> Websites { get; init; } = Array.Empty<Tai.WinUI.ViewModels.UsageItem>();
    public IReadOnlyList<TrendPoint> Trend { get; init; } = Array.Empty<TrendPoint>();
    public IReadOnlyList<Tai.WinUI.ViewModels.CategoryUsage> Categories { get; init; } = Array.Empty<Tai.WinUI.ViewModels.CategoryUsage>();
    public int TotalAppSeconds { get; init; }
    public int TotalWebSeconds { get; init; }
    public int AppCount { get; init; }
    public int WebsiteCount { get; init; }
    public string LongestAppName { get; init; } = "暂无数据";
    public string LongestAppDuration { get; init; } = "0分钟";
    public string PeakLabel { get; init; } = "暂无数据";
    public string PeakDuration { get; init; } = "0分钟";
}

public sealed class CoreUsageDataProvider : IUsageDataProvider
{
    private static readonly string[] CategoryColors =
    [
        "#4969D8", "#12966F", "#C77912", "#D14C62", "#7A5AF8", "#2870A8"
    ];

    private readonly IData _data;
    private readonly IWebData _webData;
    private readonly ICategorys _categories;

    public CoreUsageDataProvider(IData data, IWebData webData, ICategorys categories)
    {
        _data = data;
        _webData = webData;
        _categories = categories;
    }

    public Task<UsageSnapshot> GetTodayAsync(CancellationToken cancellationToken = default) =>
        GetAsync(UsagePeriod.Day, DateTime.Today, 8, cancellationToken);

    public Task<UsageSnapshot> GetAsync(
        UsagePeriod period,
        DateTime anchorDate,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await App.CoreReady.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var range = GetDateRange(period, anchorDate);
            var queryEnd = range.End.Date.AddDays(1).AddTicks(-1);
            var allAppLogs = _data.GetDateRangelogList(range.Start, queryEnd).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            var allSiteLogs = _webData.GetDateRangeWebSiteList(range.Start, queryEnd) ?? new();
            cancellationToken.ThrowIfCancellationRequested();

            var apps = CreateAppItems(allAppLogs, take);
            var websites = CreateWebsiteItems(allSiteLogs, take);
            var trend = CreateTrend(period, range);
            var categories = CreateCategories(period, range);
            var longest = allAppLogs.OrderByDescending(item => item.Time).FirstOrDefault();
            var longestName = GetAppDisplayName(longest?.AppModel);
            var peak = trend.OrderByDescending(point => point.Seconds).FirstOrDefault();

            return new UsageSnapshot
            {
                Period = period,
                AnchorDate = anchorDate.Date,
                DateRange = range,
                Apps = apps,
                Websites = websites,
                Trend = trend,
                Categories = categories,
                TotalAppSeconds = allAppLogs.Sum(item => item.Time),
                TotalWebSeconds = allSiteLogs.Sum(item => item.Duration),
                AppCount = _data.GetDateRangeAppCount(range.Start, queryEnd),
                WebsiteCount = _webData.GetBrowseSitesTotal(range.Start, queryEnd),
                LongestAppName = longest == null ? "暂无数据" : longestName,
                LongestAppDuration = longest == null ? "0分钟" : Time.ToString(longest.Time),
                PeakLabel = peak == null || peak.Seconds <= 0 ? "暂无数据" : peak.Label,
                PeakDuration = peak == null ? "0分钟" : peak.Duration
            };
        }, cancellationToken);
    }

    public static UsageDateRange GetDateRange(UsagePeriod period, DateTime anchorDate)
    {
        var anchor = anchorDate.Date;
        return period switch
        {
            UsagePeriod.Day => new UsageDateRange(anchor, anchor),
            UsagePeriod.Week => CreateWeekRange(anchor),
            UsagePeriod.Month => new UsageDateRange(
                new DateTime(anchor.Year, anchor.Month, 1),
                new DateTime(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))),
            UsagePeriod.Year => new UsageDateRange(
                new DateTime(anchor.Year, 1, 1),
                new DateTime(anchor.Year, 12, 31)),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
        };
    }

    private static UsageDateRange CreateWeekRange(DateTime anchor)
    {
        var offset = ((int)anchor.DayOfWeek + 6) % 7;
        var start = anchor.AddDays(-offset);
        return new UsageDateRange(start, start.AddDays(6));
    }

    private IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> CreateAppItems(
        IReadOnlyCollection<DailyLogModel> logs,
        int take)
    {
        var rows = take > 0 ? logs.Take(take) : logs;
        var max = logs.Count == 0 ? 1 : Math.Max(1, logs.Max(item => item.Time));
        return rows.Select(item =>
        {
            var app = item.AppModel;
            var category = _categories.GetCategory(app?.CategoryID ?? 0);
            var accent = NormalizeColor(category?.Color, "#4969D8");
            return new Tai.WinUI.ViewModels.UsageItem(
                app?.ID ?? 0,
                GetAppDisplayName(app),
                Time.ToString(item.Time),
                item.Time,
                Math.Max(2, item.Time * 100 / max),
                AppIconResolver.Resolve(app?.IconFile, app?.File, app?.Name, app?.Description),
                accent,
                category?.Name ?? "未分类",
                "应用");
        }).ToList();
    }

    private IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> CreateWebsiteItems(
        IReadOnlyCollection<Core.Models.Db.WebSiteModel> logs,
        int take)
    {
        var rows = take > 0 ? logs.Take(take) : logs;
        var max = logs.Count == 0 ? 1 : Math.Max(1, logs.Max(item => item.Duration));
        var categories = _webData.GetWebSiteCategories().ToDictionary(item => item.ID);
        return rows.Select(item =>
        {
            categories.TryGetValue(item.CategoryID, out var category);
            return new Tai.WinUI.ViewModels.UsageItem(
                item.ID,
                string.IsNullOrWhiteSpace(item.Alias) ? (item.Title ?? item.Domain ?? "未知网站") : item.Alias,
                Time.ToString(item.Duration),
                item.Duration,
                Math.Max(2, item.Duration * 100 / max),
                AppIconResolver.Resolve(item.IconFile),
                NormalizeColor(category?.Color, "#12966F"),
                category?.Name ?? "未分类",
                "网站");
        }).ToList();
    }

    private IReadOnlyList<TrendPoint> CreateTrend(UsagePeriod period, UsageDateRange range)
    {
        var appValues = period == UsagePeriod.Year
            ? _data.GetMonthTotalData(range.Start)
            : _data.GetRangeTotalData(range.Start, range.End);
        var webValues = _webData.GetBrowseDataStatistics(range.Start, range.End)
            .Select(item => item.Value)
            .ToArray();
        var count = period switch
        {
            UsagePeriod.Day => 24,
            UsagePeriod.Week => 7,
            UsagePeriod.Month => DateTime.DaysInMonth(range.Start.Year, range.Start.Month),
            UsagePeriod.Year => 12,
            _ => 0
        };

        var result = new List<TrendPoint>(count);
        for (var index = 0; index < count; index++)
        {
            var seconds = ValueAt(appValues, index) + ValueAt(webValues, index);
            result.Add(new TrendPoint(GetTrendLabel(period, range.Start, index), seconds));
        }
        return result;
    }

    private IReadOnlyList<Tai.WinUI.ViewModels.CategoryUsage> CreateCategories(
        UsagePeriod period,
        UsageDateRange range)
    {
        var totals = new Dictionary<string, (double Seconds, string Color)>(StringComparer.CurrentCultureIgnoreCase);
        var appCategories = period switch
        {
            UsagePeriod.Day => _data.GetCategoryHoursData(range.Start),
            UsagePeriod.Year => _data.GetCategoryYearData(range.Start),
            _ => _data.GetCategoryRangeData(range.Start, range.End)
        };

        foreach (var item in appCategories)
        {
            var category = _categories.GetCategory(item.CategoryID);
            AddCategory(totals, category?.Name ?? "未分类", item.Values?.Sum() ?? 0,
                NormalizeColor(category?.Color, CategoryColors[totals.Count % CategoryColors.Length]));
        }

        foreach (var item in _webData.GetCategoriesStatistics(range.Start, range.End))
        {
            AddCategory(totals, string.IsNullOrWhiteSpace(item.Name) ? "未分类" : item.Name,
                item.Value, CategoryColors[totals.Count % CategoryColors.Length]);
        }

        var grandTotal = totals.Sum(item => item.Value.Seconds);
        return totals
            .OrderByDescending(item => item.Value.Seconds)
            .Take(6)
            .Select(item => new Tai.WinUI.ViewModels.CategoryUsage(
                item.Key,
                TrendPoint.FormatDuration(item.Value.Seconds),
                grandTotal <= 0 ? 0 : (int)Math.Round(item.Value.Seconds * 100 / grandTotal),
                item.Value.Color))
            .ToList();
    }

    private static void AddCategory(
        IDictionary<string, (double Seconds, string Color)> totals,
        string name,
        double seconds,
        string color)
    {
        if (seconds <= 0) return;
        if (totals.TryGetValue(name, out var current))
            totals[name] = (current.Seconds + seconds, current.Color);
        else
            totals[name] = (seconds, color);
    }

    private static double ValueAt(IReadOnlyList<double> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : 0;

    private static string GetTrendLabel(UsagePeriod period, DateTime start, int index) => period switch
    {
        UsagePeriod.Day => $"{index:00}:00",
        UsagePeriod.Week => start.AddDays(index).ToString("ddd M/d"),
        UsagePeriod.Month => start.AddDays(index).ToString("M/d"),
        UsagePeriod.Year => $"{index + 1}月",
        _ => string.Empty
    };

    private static string GetAppDisplayName(AppModel? app)
    {
        if (!string.IsNullOrWhiteSpace(app?.Alias)) return app.Alias;
        if (!string.IsNullOrWhiteSpace(app?.Description)) return app.Description;
        if (!string.IsNullOrWhiteSpace(app?.Name)) return app.Name;
        return "未知应用";
    }

    private static string NormalizeColor(string? color, string fallback)
    {
        if (string.IsNullOrWhiteSpace(color)) return fallback;
        var value = color.Trim();
        if (value.StartsWith('#')) value = value[1..];
        return value.Length == 6 && value.All(Uri.IsHexDigit) ? $"#{value}" : fallback;
    }
}

public static class AppIconResolver
{
    public static string Resolve(
        string? configuredPath,
        string? executablePath = null,
        string? processName = null,
        string? description = null)
    {
        var configured = ResolveExistingPath(configuredPath);
        if (IsUsableImage(configured)) return configured!;

        if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
        {
            var extracted = Iconer.ExtractFromFile(
                executablePath,
                processName ?? Path.GetFileNameWithoutExtension(executablePath),
                description ?? string.Empty,
                isCheck: true,
                isRelativePath: false);
            if (IsUsableImage(extracted)) return Path.GetFullPath(extracted);
        }

        return DefaultIconPath;
    }

    public static string DefaultIconPath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "defaultIcon.png");

    private static string? ResolveExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUsableImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            using var image = System.Drawing.Image.FromFile(path);
            return image.Width > 0 && image.Height > 0;
        }
        catch
        {
            return false;
        }
    }
}
