using Core.Librarys;
using Core.Servicers.Interfaces;

namespace Tai.WinUI.Services;

public interface IUsageDataProvider
{
    Task<UsageSnapshot> GetTodayAsync(CancellationToken cancellationToken = default);
}

public sealed class UsageSnapshot
{
    public IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> Apps { get; init; } = Array.Empty<Tai.WinUI.ViewModels.UsageItem>();
    public IReadOnlyList<Tai.WinUI.ViewModels.UsageItem> Websites { get; init; } = Array.Empty<Tai.WinUI.ViewModels.UsageItem>();
    public int TotalAppSeconds { get; init; }
    public int TotalWebSeconds { get; init; }
    public int AppCount { get; init; }
    public int WebsiteCount { get; init; }
    public string LongestAppName { get; init; } = "Visual Studio";
    public string LongestAppDuration { get; init; } = "0分钟";
}

public sealed class CoreUsageDataProvider : IUsageDataProvider
{
    private readonly IData _data;
    private readonly IWebData _webData;

    public CoreUsageDataProvider(IData data, IWebData webData)
    {
        _data = data;
        _webData = webData;
    }

    public Task<UsageSnapshot> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var ready = Task.WhenAny(App.CoreReady, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).GetAwaiter().GetResult();
            if (ready != App.CoreReady)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();
            var start = DateTime.Today;
            var end = start.AddDays(1).AddSeconds(-1);

            var allAppLogs = _data.GetDateRangelogList(start, end).ToList();
            var appLogs = allAppLogs.Take(8).ToList();
            var maxAppTime = appLogs.Count == 0 ? 1 : appLogs.Max(item => item.Time);
            var apps = appLogs.Select(item =>
            {
                var app = item.AppModel;
                var name = string.IsNullOrWhiteSpace(app?.Alias)
                    ? (string.IsNullOrWhiteSpace(app?.Description) ? app?.Name : app.Description)
                    : app.Alias;
                return new Tai.WinUI.ViewModels.UsageItem(
                    name ?? "未知应用",
                    Time.ToString(item.Time),
                    Math.Max(4, item.Time * 100 / maxAppTime),
                    "\uE943",
                    "#4969D8",
                    "应用");
            }).ToList();

            var allSiteLogs = _webData.GetDateRangeWebSiteList(start, end) ?? new();
            var siteLogs = allSiteLogs.Take(8).ToList();
            var maxSiteTime = siteLogs.Count == 0 ? 1 : siteLogs.Max(item => item.Duration);
            var websites = siteLogs.Select(item => new Tai.WinUI.ViewModels.UsageItem(
                string.IsNullOrWhiteSpace(item.Alias) ? (item.Title ?? item.Domain) : item.Alias,
                Time.ToString(item.Duration),
                Math.Max(4, item.Duration * 100 / maxSiteTime),
                "\uE77B",
                "#12966F",
                "网站")).ToList();

            var longest = appLogs.OrderByDescending(item => item.Time).FirstOrDefault();
            var longestName = longest?.AppModel?.Alias;
            if (string.IsNullOrWhiteSpace(longestName)) longestName = longest?.AppModel?.Description;
            if (string.IsNullOrWhiteSpace(longestName)) longestName = longest?.AppModel?.Name;

            return new UsageSnapshot
            {
                Apps = apps,
                Websites = websites,
                TotalAppSeconds = allAppLogs.Sum(item => item.Time),
                TotalWebSeconds = allSiteLogs.Sum(item => item.Duration),
                AppCount = _data.GetDateRangeAppCount(start, end),
                WebsiteCount = _webData.GetBrowseSitesTotal(start, end),
                LongestAppName = longestName ?? "暂无数据",
                LongestAppDuration = longest == null ? "0分钟" : Time.ToString(longest.Time)
            };
        }, cancellationToken);
    }
}
