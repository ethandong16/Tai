using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Windows.Globalization;

namespace Tai.WinUI.Services;

public static class L
{
    public static bool IsEnglish => ApplicationLanguages.PrimaryLanguageOverride.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["概览"] = "Overview", ["统计"] = "Statistics", ["详细记录"] = "Details",
        ["分类"] = "Categories", ["设置"] = "Settings", ["使用详情"] = "Usage details",
        ["应用"] = "Apps", ["网站"] = "Websites", ["全部"] = "All",
        ["未分类"] = "Uncategorized", ["未知应用"] = "Unknown app", ["未知网站"] = "Unknown website",
        ["暂无数据"] = "No data", ["暂无记录"] = "No records", ["尚未发生"] = "Not yet",
        ["浏览器"] = "Browsers", ["办公文档"] = "Office", ["开发工具"] = "Development",
        ["沟通协作"] = "Communication", ["设计创作"] = "Design", ["学习阅读"] = "Learning",
        ["影音娱乐"] = "Media", ["游戏"] = "Games", ["系统工具"] = "Utilities",
        ["办公协作"] = "Productivity", ["开发技术"] = "Development", ["社交社区"] = "Social",
        ["购物生活"] = "Shopping", ["搜索资讯"] = "Search and news",
        ["网站分类"] = "Website category", ["分类名称"] = "Category name",
        ["例如：开发"] = "For example: Development", ["保存"] = "Save", ["创建"] = "Create",
        ["删除"] = "Delete", ["取消"] = "Cancel", ["确定"] = "OK",
        ["新建分类"] = "New category", ["创建失败"] = "Could not create", ["重命名分类"] = "Rename category",
        ["删除分类"] = "Delete category", ["保存失败"] = "Could not save",
        ["分类已更新"] = "Categories updated", ["获取失败"] = "Update failed",
        ["获取分类时发生错误，请稍后重试。"] = "Could not update categories. Try again later.",
        ["无法创建分类，请稍后重试。"] = "Could not create the category. Try again later.",
        ["无法更新分类。"] = "Could not update the category.",
        ["无法更新网站分类。"] = "Could not update the website category.",
        ["请选择分类"] = "Select a category",
        ["先在左侧选择一个分类，再管理它的匹配规则。"] = "Select a category before editing its matching rules.",
        ["启用目录匹配"] = "Match directories", ["匹配目录"] = "Directories",
        ["每行输入一个目录"] = "One directory per line",
        ["无法更新自动分类规则。"] = "Could not update the matching rules.",
        ["请先将该分类中的应用移出，并确认设置可以保存。"] = "Move apps out of this category before deleting it.",
        ["名称"] = "Name", ["关联进程"] = "Linked processes",
        ["每行输入一个进程名称，不带 .exe"] = "One process name per line, without .exe",
        ["保存关联"] = "Save link", ["删除关联"] = "Remove link",
        ["新的关联"] = "New link", ["选择文件"] = "Select file",
        ["导入失败"] = "Import failed", ["导入完成"] = "Import complete",
        ["导入配置"] = "Import settings", ["导出完成"] = "Export complete",
        ["导出失败"] = "Export failed", ["删除失败"] = "Delete failed",
        ["时间范围错误"] = "Invalid date range", ["删除确认"] = "Confirm deletion",
        ["操作已完成"] = "Done", ["需要手动重启"] = "Restart required",
        ["语言设置"] = "Language", ["重新启动 Tai 以应用所选语言。现在重启吗？"] = "Restart Tai to apply the selected language. Restart now?",
        ["请关闭并重新打开 Tai 以应用所选语言。"] = "Close and reopen Tai to apply the selected language.",
        ["等待更新"] = "Waiting to update", ["数据读取失败，已保留上次结果"] = "Could not load data; showing previous results",
        ["读取失败，已保留上次结果"] = "Could not load data; showing previous results",
        ["正在读取..."] = "Loading...", ["已显示缓存，正在更新..."] = "Showing cached data, updating...",
        ["此时间范围内暂无记录"] = "No records in this period",
        ["所选范围"] = "Selected period",
        ["网站数据暂时无法读取"] = "Website data is unavailable",
        ["检查设置"] = "Check settings", ["今天还没有网站记录"] = "No website activity today",
        ["网站统计尚未开启"] = "Website tracking is off",
        ["Tai 已保留上次结果，请稍后刷新；如果问题持续，请检查数据目录。"] = "Tai kept the previous results. Refresh later or check the data folder if this continues.",
        ["如果你刚刚使用过浏览器，请检查 Chrome 或 Edge 插件是否已连接。"] = "If you just used a browser, check that the Chrome or Edge extension is connected.",
        ["开启网站统计并连接浏览器插件后，这里会显示今日浏览时长。"] = "Turn on website tracking and connect the browser extension to see today's activity.",
        ["检查网站统计设置"] = "Check website tracking", ["开启网站统计"] = "Turn on website tracking",
        ["此时间范围内暂无趋势数据"] = "No trend data for this period",
        ["这个时间范围内还没有使用记录"] = "No usage records for this period",
        ["分类占比：暂无记录"] = "Category breakdown: no records",
        ["概览页面加载失败，请查看 Log/startup.log。"] = "Could not load Overview. See Log/startup.log.",
        ["文件格式有误或者数据为空，请选择有效的 JSON 列表文件。"] = "Select a valid JSON list file.",
        ["导入将覆盖当前列表，确定继续吗？"] = "Importing will replace the current list. Continue?",
        ["无法读取该 JSON 配置文件。"] = "Could not read the JSON settings file.",
        ["配置列表已导出。"] = "Settings list exported.",
        ["无法写入该 JSON 配置文件。"] = "Could not write the JSON settings file.",
        ["请选择有效的开始和结束月份。"] = "Select a valid start and end month.",
        ["所选范围内的统计数据已删除。"] = "Statistics for the selected period were deleted.",
        ["无法删除所选范围的数据。"] = "Could not delete data for the selected period.",
        ["请选择导出位置"] = "Select export folder",
        ["应用和网站数据已导出为 xlsx 与 csv 文件。"] = "App and website data exported as XLSX and CSV files.",
        ["无法导出所选范围的数据。"] = "Could not export data for the selected period.",
        ["选择 Tai 数据库"] = "Select Tai database",
        ["导入数据库"] = "Import database",
        ["导入会替换当前统计数据，并自动备份现有数据库。确定继续吗？"] = "Importing will replace current statistics and back up the database. Continue?",
        ["所选文件不是有效的 Tai 数据库，或当前数据库无法替换。原数据未被修改。"] = "The file is not a valid Tai database, or the current database could not be replaced. Existing data was not changed.",
        ["数据库未导入，但分类设置无法恢复。请重启 Tai 并检查现有分类。"] = "The database was not imported, and category settings could not be restored. Restart Tai and check your categories.",
        ["数据库已导入并备份原数据。Tai 将重新启动以加载新数据。"] = "Database imported and previous data backed up. Tai will restart to load the new data.",
        ["数据库已成功导入，但 Tai 无法自动重启。请关闭并重新打开软件。"] = "Database imported, but Tai could not restart. Close and reopen it.",
        ["导出 Tai 配置"] = "Export Tai settings", ["Tai 配置已导出。"] = "Tai settings exported.",
        ["无法写入该配置文件。"] = "Could not write the settings file.",
        ["导入 Tai 配置"] = "Import Tai settings",
        ["导入会覆盖当前 Tai 配置，确定继续吗？"] = "Importing will replace current Tai settings. Continue?",
        ["Tai 配置已导入。"] = "Tai settings imported.",
        ["配置已保存"] = "Settings saved",
        ["配置已导入，但界面未能刷新。请重启 Tai。"] = "Settings imported, but the view could not refresh. Restart Tai.",
        ["无法读取该配置文件或保存设置。"] = "Could not read or save the settings file."
    };

    public static string Text(string value) => IsEnglish && English.TryGetValue(value, out var translated)
        ? translated : value;

    public static string CatalogMessage(string message)
    {
        if (!IsEnglish) return message;
        var translated = Regex.Replace(message, @"已获取包含 (\d+) 个分类的目录。",
            match => $"Updated catalog with {match.Groups[1].Value} categories. ");
        translated = Regex.Replace(translated, @"已删除的 (\d+) 个默认分类保持删除状态。",
            match => $"{match.Groups[1].Value} deleted default categories remain deleted.");
        return translated
            .Replace("分类目录已是最新。", "Categories are up to date. ")
            .Replace("更新时间未能保存，稍后会重试。", "The update time could not be saved; Tai will retry. ")
            .Replace("分类获取已暂停。", "Category updates are paused.")
            .Replace("获取分类已取消。", "Category update was canceled.")
            .Replace("无法连接 GitHub，已保留当前分类。", "Could not connect to GitHub. Current categories were kept.")
            .Replace("GitHub 返回的分类配置无效，已保留当前分类。", "GitHub returned an invalid category catalog. Current categories were kept.")
            .Replace("保存分类设置失败，已保留当前分类目录。", "Could not save category settings. Current categories were kept.")
            .Replace("分类映射已保存，但缓存写入失败，目录规则未切换；请重试。", "Category mapping was saved, but the cache could not be written. Try again.")
            .Replace("无法完整应用新的分类配置，请查看启动日志。", "Could not apply the new categories. See the startup log.")
            .Trim();
    }

    public static string Count(int count, string chineseUnit, string englishUnit) => IsEnglish
        ? $"{count} {englishUnit}{(count == 1 ? "" : "s")}" : $"{count} 个{chineseUnit}";

    public static string CategoryCount(int count, bool websites)
    {
        if (IsEnglish)
            return $"{count} {(websites ? "website" : "app")} {(count == 1 ? "category" : "categories")}";
        return $"{count} 个{(websites ? "网站" : "应用")}分类";
    }

    public static string Duration(int seconds)
    {
        if (!IsEnglish) return seconds > 0 ? Core.Librarys.Time.ToString(seconds) : "0分钟";
        if (seconds <= 0) return "0 min";
        if (seconds < 60) return $"{seconds} sec";
        if (seconds < 3600) return $"{seconds / 60} min" + (seconds % 60 > 0 ? $" {seconds % 60} sec" : "");
        return $"{seconds / 3600} hr" + (seconds % 3600 >= 60 ? $" {(seconds % 3600) / 60} min" : "");
    }

    public static string Date(DateTime date) => IsEnglish
        ? date.ToString("MMM d, yyyy", CultureInfo.GetCultureInfo("en-US")) : date.ToString("yyyy年M月d日");

    public static string Month(DateTime date) => IsEnglish
        ? date.ToString("MMM yyyy", CultureInfo.GetCultureInfo("en-US")) : date.ToString("yyyy年MM月");
}
