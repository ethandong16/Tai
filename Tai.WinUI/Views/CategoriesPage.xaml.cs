using System.Collections.ObjectModel;
using System.Text.Json;
using Core.Models;
using Core.Models.Db;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tai.WinUI.Services;

namespace Tai.WinUI.Views;

public sealed partial class CategoriesPage : Page
{
    private readonly ICategorys _categoryService;
    private readonly ICategoryCatalogService _catalogService;
    private readonly IAppData _appData;
    private readonly IWebData _webData;
    private readonly DispatcherQueue _dispatcherQueue;
    private IReadOnlyDictionary<int, CategoryAppRow[]> _appsByCategory = new Dictionary<int, CategoryAppRow[]>();
    private IReadOnlyDictionary<int, CategoryWebsiteRow[]> _websitesByCategory = new Dictionary<int, CategoryWebsiteRow[]>();
    private bool _refreshingCategories;
    private int _refreshGeneration;
    private int _websiteRefreshGeneration;

    public ObservableCollection<CategoryRow> Categories { get; } = new();
    public IReadOnlyList<CategoryAppRow> CategoryApps { get; private set; } = Array.Empty<CategoryAppRow>();
    public ObservableCollection<WebsiteCategoryRow> WebsiteCategories { get; } = new();
    public IReadOnlyList<CategoryWebsiteRow> CategoryWebsites { get; private set; } = Array.Empty<CategoryWebsiteRow>();
    public Task LoadDataTask { get; private set; } = Task.CompletedTask;

    public CategoriesPage()
    {
        _categoryService = App.Services.GetRequiredService<ICategorys>();
        _catalogService = App.Services.GetRequiredService<ICategoryCatalogService>();
        _appData = App.Services.GetRequiredService<IAppData>();
        _webData = App.Services.GetRequiredService<IWebData>();
        InitializeComponent();
        CategoryMode.SelectedItem = AppsMode;
        _dispatcherQueue = DispatcherQueue;
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += CategoriesPage_Loaded;
        _catalogService.CatalogUpdated += CatalogService_CatalogUpdated;
    }

    private async void CategoriesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.CoreReady;
            if (IsLoaded)
                await (LoadDataTask = RefreshDataAsync());
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
        }
    }

    private void CatalogService_CatalogUpdated(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (IsLoaded) LoadDataTask = RefreshAfterCatalogUpdateAsync();
        });
    }

    private async Task RefreshAfterCatalogUpdateAsync()
    {
        try { await RefreshDataAsync(); }
        catch (Exception exception) { App.LogStartupException(exception); }
    }

    private void CategoryMode_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (CategoryGrid == null || WebsiteGrid == null) return;
        var showWebsites = sender.SelectedItem == WebsitesMode;
        CategoryGrid.Visibility = showWebsites ? Visibility.Collapsed : Visibility.Visible;
        WebsiteGrid.Visibility = showWebsites ? Visibility.Visible : Visibility.Collapsed;
        CategoryActions.Visibility = showWebsites ? Visibility.Collapsed : Visibility.Visible;
        CategoryCountText.Text = L.CategoryCount(
            showWebsites ? Math.Max(0, WebsiteCategories.Count - 1) : Categories.Count, showWebsites);
        if (showWebsites && IsLoaded && WebsiteCategories.Count > 0)
            LoadDataTask = RefreshWebsiteAfterModeChangeAsync();
    }

    private async Task RefreshWebsiteAfterModeChangeAsync()
    {
        try { await RefreshWebsiteDataAsync(null, Volatile.Read(ref _refreshGeneration)); }
        catch (Exception exception) { App.LogStartupException(exception); }
    }

    private List<CategorySnapshot> BuildAppSnapshot()
    {
        return _categoryService.GetCategories()
            .OrderBy(item => item.Name)
            .Select(category => new CategorySnapshot(
                category,
                _appData.GetAppsByCategoryID(category.ID)
                    .Select(app => new CategoryAppRow(app))
                    .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray()))
            .ToList();
    }

    private List<WebsiteSnapshot> BuildWebsiteSnapshot()
    {
        var websites = _webData.GetAllWebSites();
        var categories = _webData.GetWebSiteCategories().OrderBy(item => item.Name).ToList();
        var rowsByCategory = websites
            .GroupBy(site => site.CategoryID)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(site => site.Duration)
                .ThenBy(site => site.Domain, StringComparer.OrdinalIgnoreCase)
                .Select(site => new CategoryWebsiteRow(site)).ToArray());
        return categories
            .Select(category => new WebsiteSnapshot(category,
                rowsByCategory.GetValueOrDefault(category.ID) ?? Array.Empty<CategoryWebsiteRow>()))
            .Append(new WebsiteSnapshot(null,
                rowsByCategory.GetValueOrDefault(0) ?? Array.Empty<CategoryWebsiteRow>()))
            .ToList();
    }

    private async Task RefreshDataAsync(int? selectedAppId = null, int? selectedWebsiteId = null)
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);
        var apps = await Task.Run(BuildAppSnapshot);
        if (!IsLoaded || generation != Volatile.Read(ref _refreshGeneration)) return;
        ApplyAppSnapshot(apps, selectedAppId ?? (CategoryList.SelectedItem as CategoryRow)?.Model.ID ?? 0);

        await RefreshWebsiteDataAsync(selectedWebsiteId, generation);
    }

    private async Task RefreshWebsiteDataAsync(int? selectedId, int pageGeneration)
    {
        var websiteGeneration = Interlocked.Increment(ref _websiteRefreshGeneration);
        var websites = await Task.Run(BuildWebsiteSnapshot);
        if (!IsLoaded || pageGeneration != Volatile.Read(ref _refreshGeneration)
            || websiteGeneration != Volatile.Read(ref _websiteRefreshGeneration)) return;
        ApplyWebsiteSnapshot(websites,
            selectedId ?? (WebsiteCategoryList.SelectedItem as WebsiteCategoryRow)?.Id ?? 0);
    }

    private void ApplyAppSnapshot(List<CategorySnapshot> snapshot, int selectedId)
    {
        _refreshingCategories = true;
        try
        {
            _appsByCategory = snapshot.ToDictionary(item => item.Category.ID, item => item.Apps);
            Categories.Clear();
            CategoryRow? selected = null;
            foreach (var item in snapshot)
            {
                var row = new CategoryRow(item.Category, item.Apps.Length);
                Categories.Add(row);
                if (item.Category.ID == selectedId) selected = row;
            }
            CategoryList.SelectedItem = selected ?? Categories.FirstOrDefault();
        }
        finally
        {
            _refreshingCategories = false;
        }
        if (CategoryMode.SelectedItem == AppsMode)
            CategoryCountText.Text = L.CategoryCount(Categories.Count, false);
        EmptyCategoryState.Visibility = Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RuleSection.Visibility = Categories.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RefreshSelectedCategoryApps();
    }

    private void ApplyWebsiteSnapshot(List<WebsiteSnapshot> snapshot, int selectedId)
    {
        _refreshingCategories = true;
        try
        {
            _websitesByCategory = snapshot.ToDictionary(item => item.Category?.ID ?? 0, item => item.Websites);
            WebsiteCategories.Clear();
            foreach (var item in snapshot)
                WebsiteCategories.Add(new WebsiteCategoryRow(item.Category, item.Websites.Length));
            WebsiteCategoryList.SelectedItem = WebsiteCategories.FirstOrDefault(item => item.Id == selectedId)
                ?? WebsiteCategories.FirstOrDefault();
        }
        finally
        {
            _refreshingCategories = false;
        }
        if (CategoryMode.SelectedItem == WebsitesMode)
            CategoryCountText.Text = L.CategoryCount(WebsiteCategories.Count - 1, true);
        RefreshSelectedCategoryWebsites();
    }

    private sealed record CategorySnapshot(CategoryModel Category, CategoryAppRow[] Apps);
    private sealed record WebsiteSnapshot(WebSiteCategoryModel? Category, CategoryWebsiteRow[] Websites);

    private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshingCategories && SelectedCategoryNameText != null)
            RefreshSelectedCategoryApps();
    }

    private void RefreshSelectedCategoryApps()
    {
        AppDetailsScrollViewer.ChangeView(null, 0, null, disableAnimation: true);
        CategoryApps = Array.Empty<CategoryAppRow>();
        if (CategoryList.SelectedItem is not CategoryRow selected)
        {
            CategoryAppsList.ItemsSource = CategoryApps;
            SelectedCategoryNameText.Text = string.Empty;
            SelectedAppCountText.Text = string.Empty;
            EmptyAppsText.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedCategoryNameText.Text = selected.DisplayName;
        CategoryApps = _appsByCategory.GetValueOrDefault(selected.Model.ID) ?? Array.Empty<CategoryAppRow>();
        CategoryAppsList.ItemsSource = CategoryApps;
        SelectedAppCountText.Text = L.Count(CategoryApps.Count, "应用", "app");
        EmptyAppsText.Visibility = CategoryApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WebsiteCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_refreshingCategories && SelectedWebsiteCategoryNameText != null)
            RefreshSelectedCategoryWebsites();
    }

    private void RefreshSelectedCategoryWebsites()
    {
        CategoryWebsites = Array.Empty<CategoryWebsiteRow>();
        if (WebsiteCategoryList.SelectedItem is not WebsiteCategoryRow selected)
        {
            CategoryWebsitesList.ItemsSource = CategoryWebsites;
            SelectedWebsiteCategoryNameText.Text = string.Empty;
            SelectedWebsiteCountText.Text = string.Empty;
            EmptyWebsitesText.Visibility = Visibility.Collapsed;
            return;
        }
        SelectedWebsiteCategoryNameText.Text = selected.DisplayName;
        CategoryWebsites = _websitesByCategory.GetValueOrDefault(selected.Id) ?? Array.Empty<CategoryWebsiteRow>();
        CategoryWebsitesList.ItemsSource = CategoryWebsites;
        SelectedWebsiteCountText.Text = L.Count(CategoryWebsites.Count, "网站", "website");
        EmptyWebsitesText.Visibility = CategoryWebsites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ChangeWebsiteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryWebsiteRow site }) return;
        var choices = WebsiteCategories.ToList();
        var picker = new ComboBox
        {
            Header = L.Text("网站分类"),
            ItemsSource = choices,
            DisplayMemberPath = nameof(WebsiteCategoryRow.DisplayName),
            SelectedItem = choices.FirstOrDefault(item => item.Id == site.CategoryId),
            MinWidth = 220
        };
        if (await CreateDialog(site.Name, picker, "保存").ShowAsync() != ContentDialogResult.Primary
            || picker.SelectedItem is not WebsiteCategoryRow selected) return;
        try
        {
            await Task.Run(() => _webData.UpdateWebSitesCategory(new[] { site.Id }, selected.Id));
            await RefreshWebsiteDataAsync(selected.Id, Volatile.Read(ref _refreshGeneration));
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("保存失败", "无法更新网站分类。");
        }
    }

    private async void FetchCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        FetchCategoriesButton.IsEnabled = false;
        try
        {
            await App.CoreReady;
            var result = await _catalogService.FetchLatestAsync();
            if (IsLoaded) await RefreshDataAsync();
            if (IsLoaded) await ShowMessageAsync(result.Succeeded ? "分类已更新" : "获取失败", L.CatalogMessage(result.Message));
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            if (IsLoaded) await ShowMessageAsync("获取失败", "获取分类时发生错误，请稍后重试。");
        }
        finally
        {
            FetchCategoriesButton.IsEnabled = true;
        }
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = L.Text("分类名称"), PlaceholderText = L.Text("例如：开发") };
        var dialog = CreateDialog("新建分类", nameBox, "创建");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var name = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var created = _categoryService.Create(new CategoryModel
            {
                Name = name,
                Color = "#4969D8",
                IconFile = string.Empty,
                Directories = "[]"
            });
            await RefreshDataAsync(selectedAppId: created.ID);
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("创建失败", "无法创建分类，请稍后重试。");
        }
    }

    private async void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryRow row }) return;
        var nameBox = new TextBox { Header = L.Text("分类名称"), Text = row.Name };
        var dialog = CreateDialog("重命名分类", nameBox, "保存");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var name = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            row.Model.Name = name;
            _categoryService.Update(row.Model);
            await RefreshDataAsync(selectedAppId: row.Model.ID);
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("保存失败", "无法更新分类。");
        }
    }

    private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CategoryRow row }) return;
        var dialog = new ContentDialog
        {
            Title = L.Text("删除分类"),
            Content = L.IsEnglish ? $"Delete '{row.DisplayName}'? Apps in this category will remain." : $"确定删除“{row.Name}”吗？分类中的应用不会被删除。",
            PrimaryButtonText = L.Text("删除"),
            CloseButtonText = L.Text("取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            _catalogService.DeleteCategory(row.Model);
            await RefreshDataAsync();
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("删除失败", "请先将该分类中的应用移出，并确认设置可以保存。");
        }
    }

    private async void ManageRules_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not CategoryRow row)
        {
            await ShowMessageAsync("请选择分类", "先在左侧选择一个分类，再管理它的匹配规则。");
            return;
        }

        var enabled = new ToggleSwitch
        {
            Header = L.Text("启用目录匹配"), IsOn = row.Model.IsDirectoryMath,
            OnContent = L.IsEnglish ? "On" : "开启", OffContent = L.IsEnglish ? "Off" : "关闭"
        };
        var paths = new TextBox
        {
            Header = L.Text("匹配目录"),
            PlaceholderText = L.Text("每行输入一个目录"),
            Text = string.Join(Environment.NewLine, ReadDirectories(row.Model.Directories)),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(enabled);
        content.Children.Add(paths);
        var dialog = CreateDialog(L.IsEnglish ? $"{row.DisplayName} · Matching rules" : $"{row.Name} · 匹配规则", content, "保存");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            row.Model.IsDirectoryMath = enabled.IsOn;
            row.Model.Directories = JsonSerializer.Serialize(paths.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            _categoryService.Update(row.Model);
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("保存失败", "无法更新自动分类规则。");
        }
    }

    private ContentDialog CreateDialog(string title, object content, string primaryText) => new()
    {
        Title = L.Text(title),
        Content = content,
        PrimaryButtonText = L.Text(primaryText),
        CloseButtonText = L.Text("取消"),
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = XamlRoot
    };

    private async Task ShowMessageAsync(string title, string message)
    {
        if (!IsLoaded || XamlRoot == null) return;
        try
        {
            await new ContentDialog
            {
                Title = L.Text(title),
                Content = L.Text(message),
                CloseButtonText = L.Text("确定"),
                XamlRoot = XamlRoot
            }.ShowAsync();
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
        }
    }

    private static IReadOnlyList<string> ReadDirectories(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed class CategoryRow
{
    public CategoryRow(CategoryModel model, int itemCount)
    {
        Model = model;
        ItemCount = itemCount;
        var color = ParseColor(model.Color);
        AccentBrush = new SolidColorBrush(color);
        SoftBrush = new SolidColorBrush(ColorHelper.FromArgb(32, color.R, color.G, color.B));
    }

    public CategoryModel Model { get; }
    public string Name => Model.Name;
    public string DisplayName => L.Text(Name);
    public int ItemCount { get; }
    public string ItemCountText => L.Count(ItemCount, "应用", "app");
    public SolidColorBrush AccentBrush { get; }
    public SolidColorBrush SoftBrush { get; }

    internal static Windows.UI.Color ParseColor(string? value)
    {
        var hex = (value ?? string.Empty).TrimStart('#');
        if (hex.Length == 6
            && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return ColorHelper.FromArgb(255, r, g, b);
        return ColorHelper.FromArgb(255, 73, 105, 216);
    }
}

public sealed class WebsiteCategoryRow
{
    public WebsiteCategoryRow(WebSiteCategoryModel? model, int count)
    {
        Model = model;
        Count = count;
        var color = CategoryRow.ParseColor(model?.Color);
        AccentBrush = new SolidColorBrush(color);
        SoftBrush = new SolidColorBrush(ColorHelper.FromArgb(32, color.R, color.G, color.B));
    }

    public WebSiteCategoryModel? Model { get; }
    public int Id => Model?.ID ?? 0;
    public string Name => Model?.Name ?? "未分类";
    public string DisplayName => L.Text(Name);
    public int Count { get; }
    public string CountText => L.Count(Count, "网站", "website");
    public SolidColorBrush AccentBrush { get; }
    public SolidColorBrush SoftBrush { get; }
}

public sealed class CategoryWebsiteRow
{
    public CategoryWebsiteRow(WebSiteModel site)
    {
        Id = site.ID;
        CategoryId = site.CategoryID;
        Name = !string.IsNullOrWhiteSpace(site.Alias) ? site.Alias
            : !string.IsNullOrWhiteSpace(site.Title) ? site.Title : site.Domain;
        Domain = site.Domain;
        IconPath = AppIconResolver.Resolve(site.IconFile);
    }

    public int Id { get; }
    public int CategoryId { get; }
    public string Name { get; }
    public string Domain { get; }
    public string IconPath { get; }
    public ImageSource Icon => new BitmapImage(new Uri(IconPath, UriKind.Absolute));
}

public sealed class CategoryAppRow
{
    public CategoryAppRow(AppModel app)
    {
        Name = !string.IsNullOrWhiteSpace(app.Alias) ? app.Alias
            : !string.IsNullOrWhiteSpace(app.Description) ? app.Description
            : app.Name;
        ProcessName = app.Name;
        IconPath = AppIconResolver.Resolve(app.IconFile, app.File, app.Name, app.Description);
    }

    public string Name { get; }
    public string ProcessName { get; }
    public string IconPath { get; }
    public ImageSource Icon => new BitmapImage(new Uri(IconPath, UriKind.Absolute));
}
