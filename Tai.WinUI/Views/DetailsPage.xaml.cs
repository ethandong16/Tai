using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Tai.WinUI.Services;

namespace Tai.WinUI.Views;

public sealed partial class DetailsPage : Page
{
    private readonly List<DetailRow> _allRows = new();

    public DateTimeOffset Today { get; set; } = DateTimeOffset.Now;
    public ObservableCollection<DetailRow> Rows { get; } = new()
    {
        new("Visual Studio", "8小时13分", 82, "\uE943", "#7B61FF", "应用"),
        new("Google Chrome", "6小时31分", 65, "\uE774", "#3B82F6", "应用"),
        new("GitHub", "4小时23分", 72, "\uE77B", "#24292F", "网站"),
        new("Microsoft Edge", "4小时30分", 46, "\uE774", "#0EA5A4", "应用"),
        new("YouTube", "2小时15分", 41, "\uE714", "#E5484D", "网站")
    };

    public DetailsPage()
    {
        InitializeComponent();
        _allRows.AddRange(Rows);
        _ = LoadRowsAsync();
    }

    private async Task LoadRowsAsync()
    {
        var provider = App.Services.GetService<IUsageDataProvider>();
        if (provider == null) return;

        try
        {
            var snapshot = await provider.GetTodayAsync();
            Rows.Clear();
            _allRows.Clear();
            foreach (var item in snapshot.Apps)
            {
                _allRows.Add(new DetailRow(item.Name, item.Duration, item.Percent, item.Glyph, item.AccentHex, "应用"));
            }
            foreach (var item in snapshot.Websites)
            {
                _allRows.Add(new DetailRow(item.Name, item.Duration, item.Percent, item.Glyph, item.AccentHex, "网站"));
            }
            ApplyTypeFilter();
        }
        catch
        {
            // Keep the initial rows when the database cannot be read.
        }
    }

    private void RowsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DetailRow row)
        {
            Frame?.Navigate(row.Category == "网站" ? typeof(WebsiteDetailPage) : typeof(AppDetailPage), row);
        }
    }

    private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Rows != null) ApplyTypeFilter();
    }

    private void ApplyTypeFilter()
    {
        if (_allRows.Count == 0) return;

        var selected = (TypeFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filtered = string.IsNullOrWhiteSpace(selected) || selected == "全部"
            ? _allRows
            : _allRows.Where(row => row.Category == selected).ToList();

        Rows.Clear();
        foreach (var row in filtered) Rows.Add(row);
    }
}

public sealed class DetailRow
{
    public DetailRow(string name, string duration, int percent, string glyph, string accent, string category)
    {
        Name = name;
        Duration = duration;
        Percent = percent;
        Glyph = glyph;
        AccentBrush = new SolidColorBrush(ParseColor(accent));
        Category = category;
    }

    public string Name { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string PercentText => $"{Percent}%";
    public string Glyph { get; set; } = string.Empty;
    public SolidColorBrush AccentBrush { get; }
    public string Category { get; set; } = string.Empty;

    private static Windows.UI.Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        return ColorHelper.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }
}
