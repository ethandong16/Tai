using Core.Librarys;
using Core.Models;
using Core.Servicers.Interfaces;
using System.Collections.Concurrent;

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

public sealed record TrendPoint(string Label, double Seconds, bool IsFuture = false)
{
    public string Duration => IsFuture ? "尚未发生" : FormatDuration(Seconds);

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

    Task<UsageSnapshot> GetTodayAsync(int take = 8, CancellationToken cancellationToken = default);

    bool TryGetCached(
        UsagePeriod period,
        DateTime anchorDate,
        int take,
        out UsageSnapshot snapshot);

    Task PreloadAsync(
        DateTime anchorDate,
        int take = 8,
        CancellationToken cancellationToken = default);
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
    private readonly ConcurrentDictionary<UsageCacheKey, UsageSnapshot> _cache = new();
    private readonly ConcurrentDictionary<UsageCacheKey, Task<UsageSnapshot>> _loads = new();

    public CoreUsageDataProvider(IData data, IWebData webData, ICategorys categories)
    {
        _data = data;
        _webData = webData;
        _categories = categories;
    }

    public Task<UsageSnapshot> GetTodayAsync(int take = 8, CancellationToken cancellationToken = default) =>
        GetAsync(UsagePeriod.Day, DateTime.Today, take, cancellationToken);

    public Task<UsageSnapshot> GetAsync(
        UsagePeriod period,
        DateTime anchorDate,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        return GetFreshAsync(period, anchorDate, take, cancellationToken);
    }

    public bool TryGetCached(
        UsagePeriod period,
        DateTime anchorDate,
        int take,
        out UsageSnapshot snapshot)
    {
        return _cache.TryGetValue(CreateCacheKey(period, anchorDate, take), out snapshot!);
    }

    public async Task PreloadAsync(
        DateTime anchorDate,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        foreach (var period in Enum.GetValues<UsagePeriod>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await GetFreshAsync(period, anchorDate, take, cancellationToken);
        }
    }

    private async Task<UsageSnapshot> GetFreshAsync(
        UsagePeriod period,
        DateTime anchorDate,
        int take,
        CancellationToken cancellationToken)
    {
        await App.CoreReady.WaitAsync(cancellationToken);
        var key = CreateCacheKey(period, anchorDate, take);
        var load = _loads.GetOrAdd(key, LoadAndCacheAsync);
        return await load.WaitAsync(cancellationToken);
    }

    private async Task<UsageSnapshot> LoadAndCacheAsync(UsageCacheKey key)
    {
        try
        {
            var snapshot = await Task.Run(() => BuildSnapshot(key)).ConfigureAwait(false);

            _cache[key] = snapshot;
            return snapshot;
        }
        finally
        {
            _loads.TryRemove(key, out _);
        }
    }

    private UsageSnapshot BuildSnapshot(UsageCacheKey key)
    {
        var range = GetDateRange(key.Period, key.AnchorDate);
        var queryEnd = range.End.Date.AddDays(1).AddTicks(-1);
        var allAppLogs = _data.GetDateRangelogList(range.Start, queryEnd).ToList();
        var allSiteLogs = _webData.GetDateRangeWebSiteList(range.Start, queryEnd) ?? new();
        var apps = CreateAppItems(allAppLogs, key.Take);
        var websites = CreateWebsiteItems(allSiteLogs, key.Take);
        var trend = CreateTrend(key.Period, range);
        var categories = CreateCategories(key.Period, range);
        var longest = allAppLogs.OrderByDescending(item => item.Time).FirstOrDefault();
        var longestName = GetAppDisplayName(longest?.AppModel);
        var peak = trend.Where(point => !point.IsFuture).OrderByDescending(point => point.Seconds).FirstOrDefault();

        return new UsageSnapshot
        {
            Period = key.Period,
            AnchorDate = key.AnchorDate,
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
    }

    private static UsageCacheKey CreateCacheKey(UsagePeriod period, DateTime anchorDate, int take) =>
        new(period, anchorDate.Date, take);

    private readonly record struct UsageCacheKey(UsagePeriod Period, DateTime AnchorDate, int Take);

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
        var total = logs.Sum(item => Math.Max(0, item.Time));
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
                CalculateSharePercent(item.Time, total),
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
        var total = logs.Sum(item => Math.Max(0, item.Duration));
        var categories = _webData.GetWebSiteCategories().ToDictionary(item => item.ID);
        return rows.Select(item =>
        {
            categories.TryGetValue(item.CategoryID, out var category);
            return new Tai.WinUI.ViewModels.UsageItem(
                item.ID,
                string.IsNullOrWhiteSpace(item.Alias) ? (item.Title ?? item.Domain ?? "未知网站") : item.Alias,
                Time.ToString(item.Duration),
                item.Duration,
                CalculateSharePercent(item.Duration, total),
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
            var isFuture = IsFuturePoint(period, range.Start, index, DateTime.Now);
            var seconds = isFuture ? 0 : ValueAt(appValues, index) + ValueAt(webValues, index);
            result.Add(new TrendPoint(GetTrendLabel(period, range.Start, index), seconds, isFuture));
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

    internal static int CalculateSharePercent(double value, double total)
    {
        if (value <= 0 || total <= 0) return 0;
        return Math.Clamp((int)Math.Round(value * 100 / total, MidpointRounding.AwayFromZero), 0, 100);
    }

    internal static bool IsFuturePoint(UsagePeriod period, DateTime start, int index, DateTime now)
    {
        var pointDate = period switch
        {
            UsagePeriod.Day => start.Date.AddHours(index),
            UsagePeriod.Week or UsagePeriod.Month => start.Date.AddDays(index),
            UsagePeriod.Year => new DateTime(start.Year, index + 1, 1),
            _ => start.Date
        };

        return period switch
        {
            UsagePeriod.Day => pointDate.Date > now.Date ||
                               (pointDate.Date == now.Date && pointDate.Hour > now.Hour),
            UsagePeriod.Week or UsagePeriod.Month => pointDate.Date > now.Date,
            UsagePeriod.Year => pointDate.Year > now.Year ||
                                (pointDate.Year == now.Year && pointDate.Month > now.Month),
            _ => false
        };
    }

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
