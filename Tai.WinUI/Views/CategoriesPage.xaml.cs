using System.Collections.ObjectModel;
using System.Text.Json;
using Core.Models;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Tai.WinUI.Views;

public sealed partial class CategoriesPage : Page
{
    private readonly ICategorys _categoryService;
    private readonly ICategoryCatalogService _catalogService;
    private readonly IAppData _appData;
    private readonly DispatcherQueue _dispatcherQueue;

    public ObservableCollection<CategoryRow> Categories { get; } = new();

    public CategoriesPage()
    {
        _categoryService = App.Services.GetRequiredService<ICategorys>();
        _catalogService = App.Services.GetRequiredService<ICategoryCatalogService>();
        _appData = App.Services.GetRequiredService<IAppData>();
        InitializeComponent();
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
            if (IsLoaded) RefreshCategories((CategoryList.SelectedItem as CategoryRow)?.Model.ID ?? 0);
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
            if (IsLoaded) RefreshCategories((CategoryList.SelectedItem as CategoryRow)?.Model.ID ?? 0);
        });
    }

    private List<CategorySnapshot> BuildCategorySnapshot()
    {
        return _categoryService.GetCategories()
            .OrderBy(item => item.Name)
            .Select(category => new CategorySnapshot(
                category,
                _appData.GetAppsByCategoryID(category.ID).Count))
            .ToList();
    }

    private void RefreshCategories(int selectedId = 0)
    {
        ApplyCategorySnapshot(BuildCategorySnapshot(), selectedId);
    }

    private void ApplyCategorySnapshot(IReadOnlyList<CategorySnapshot> snapshot, int selectedId = 0)
    {
        Categories.Clear();
        foreach (var item in snapshot)
        {
            var row = new CategoryRow(item.Category, item.ItemCount);
            Categories.Add(row);
            if (item.Category.ID == selectedId) CategoryList.SelectedItem = row;
        }
        CategoryCountText.Text = $"{Categories.Count} 个分类";
        EmptyCategoryState.Visibility = Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RuleSection.Visibility = Categories.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (Categories.Count > 0 && CategoryList.SelectedItem == null)
            CategoryList.SelectedIndex = 0;
    }

    private sealed record CategorySnapshot(CategoryModel Category, int ItemCount);

    private async void FetchCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        FetchCategoriesButton.IsEnabled = false;
        try
        {
            await App.CoreReady;
            var result = await _catalogService.FetchLatestAsync();
            if (IsLoaded) RefreshCategories((CategoryList.SelectedItem as CategoryRow)?.Model.ID ?? 0);
            if (IsLoaded) await ShowMessageAsync(result.Succeeded ? "分类已更新" : "获取失败", result.Message);
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
        var nameBox = new TextBox { Header = "分类名称", PlaceholderText = "例如：开发" };
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
            RefreshCategories(created.ID);
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
        var nameBox = new TextBox { Header = "分类名称", Text = row.Name };
        var dialog = CreateDialog("重命名分类", nameBox, "保存");
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var name = nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            row.Model.Name = name;
            _categoryService.Update(row.Model);
            RefreshCategories(row.Model.ID);
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
            Title = "删除分类",
            Content = $"确定删除“{row.Name}”吗？分类中的应用不会被删除。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        try
        {
            _catalogService.DeleteCategory(row.Model);
            RefreshCategories();
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

        var enabled = new ToggleSwitch { Header = "启用目录匹配", IsOn = row.Model.IsDirectoryMath };
        var paths = new TextBox
        {
            Header = "匹配目录",
            PlaceholderText = "每行输入一个目录",
            Text = string.Join(Environment.NewLine, ReadDirectories(row.Model.Directories)),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(enabled);
        content.Children.Add(paths);
        var dialog = CreateDialog($"{row.Name} · 匹配规则", content, "保存");
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
        Title = title,
        Content = content,
        PrimaryButtonText = primaryText,
        CloseButtonText = "取消",
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
                Title = title,
                Content = message,
                CloseButtonText = "确定",
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
    public int ItemCount { get; }
    public string ItemCountText => $"{ItemCount} 个应用";
    public SolidColorBrush AccentBrush { get; }
    public SolidColorBrush SoftBrush { get; }

    private static Windows.UI.Color ParseColor(string? value)
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
