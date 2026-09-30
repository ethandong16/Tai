using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Data.SQLite;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tai.WinUI.Services;

namespace Tai.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly int[] CategoryIntervals = { 0, 6, 12, 24, 72, 168 };
    private static readonly SemaphoreSlim ImportOperationGate = new(1, 1);
    private readonly IAppConfig _appConfig;
    private readonly IData _data;
    private readonly IWebData _webData;
    private readonly IMain _main;
    private readonly ICategoryCatalogService _catalogService;
    private readonly IDatabase _database;
    private int _customCategoryIntervalHours;
    private ConfigModel _config = null!;
    private bool _isLoading;

    public SettingsPage()
    {
        InitializeComponent();
        SettingsTabs.SelectedItem = GeneralTab;
        _appConfig = App.Services.GetRequiredService<IAppConfig>();
        _data = App.Services.GetRequiredService<IData>();
        _webData = App.Services.GetRequiredService<IWebData>();
        _main = App.Services.GetRequiredService<IMain>();
        _catalogService = App.Services.GetRequiredService<ICategoryCatalogService>();
        _database = App.Services.GetRequiredService<IDatabase>();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_config != null) return;

        _config = _appConfig.GetConfig() ?? new ConfigModel();
        NormalizeConfig(_config);
        PopulateControls();
    }

    private static void NormalizeConfig(ConfigModel config)
    {
        config.General ??= new GeneralModel();
        config.General.DefaultCategoryIds ??= new Dictionary<string, int>();
        config.Behavior ??= new BehaviorModel();
        config.Links ??= new List<LinkModel>();
        config.Links.RemoveAll(link => link == null);
        foreach (var link in config.Links)
        {
            link.Name ??= "新的关联";
            link.ProcessList ??= new List<string>();
        }
        config.Behavior.IgnoreProcessList ??= new List<string>();
        config.Behavior.IgnoreURLList ??= new List<string>();
        config.Behavior.ProcessWhiteList ??= new List<string>();
    }

    private void PopulateControls()
    {
        _isLoading = true;
        try
        {
            var general = _config.General;
            foreach (var toggle in new[] { StartAtBootToggle, StartupShowToggle, SaveWindowSizeToggle,
                         WebEnabledToggle, SleepWatchToggle, WhiteListToggle })
            {
                toggle.OnContent = L.IsEnglish ? "On" : "开启";
                toggle.OffContent = L.IsEnglish ? "Off" : "关闭";
            }
            StartAtBootToggle.IsOn = general.IsStartatboot;
            StartupShowToggle.IsOn = general.IsStartupShowMainWindow;
            SaveWindowSizeToggle.IsOn = general.IsSaveWindowSize;
            WebEnabledToggle.IsOn = general.IsWebEnabled;
            StartPagePicker.SelectedIndex = Math.Clamp(general.StartPage, 0, 3);
            ThemePicker.SelectedIndex = Math.Clamp(general.Theme, 0, 2);
            LanguagePicker.SelectedIndex = general.Language switch { "zh-CN" => 1, "en-US" => 2, _ => 0 };
            while (CategoryIntervalPicker.Items.Count > CategoryIntervals.Length)
                CategoryIntervalPicker.Items.RemoveAt(CategoryIntervalPicker.Items.Count - 1);
            var displayedInterval = Math.Clamp(general.CategoryUpdateIntervalHours, 0, 720);
            var categoryIntervalIndex = Array.IndexOf(CategoryIntervals, displayedInterval);
            if (categoryIntervalIndex < 0)
            {
                _customCategoryIntervalHours = displayedInterval;
                CategoryIntervalPicker.Items.Add(new ComboBoxItem
                {
                    Content = L.IsEnglish ? $"Every {_customCategoryIntervalHours} hours" : $"每 {_customCategoryIntervalHours} 小时"
                });
                categoryIntervalIndex = CategoryIntervals.Length;
            }
            CategoryIntervalPicker.SelectedIndex = categoryIntervalIndex;
            FillCountPicker(FrequentCountPicker, 10, general.IndexPageFrequentUseNum);

            var behavior = _config.Behavior;
            SleepWatchToggle.IsOn = behavior.IsSleepWatch;
            WhiteListToggle.IsOn = behavior.IsWhiteList;
            RenderList(IgnoreProcessList, behavior.IgnoreProcessList);
            RenderList(IgnoreUrlList, behavior.IgnoreURLList);
            RenderList(WhiteList, behavior.ProcessWhiteList);
            RenderLinks();

            var now = DateTimeOffset.Now;
            DeleteStartPicker.Date ??= now;
            DeleteEndPicker.Date ??= now;
            ExportStartPicker.Date ??= now;
            ExportEndPicker.Date ??= now;
            VersionText.Text = L.IsEnglish
                ? $"Tai version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0"}"
                : $"Tai 版本号 {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0"}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static void FillCountPicker(ComboBox picker, int max, int value)
    {
        picker.Items.Clear();
        for (var i = 1; i <= max; i++) picker.Items.Add(i.ToString());
        picker.SelectedIndex = Math.Clamp(value - 1, 0, max - 1);
    }

    private void GeneralSetting_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _config == null) return;
        var general = _config.General;
        if (ReferenceEquals(sender, StartAtBootToggle)) general.IsStartatboot = StartAtBootToggle.IsOn;
        else if (ReferenceEquals(sender, StartupShowToggle)) general.IsStartupShowMainWindow = StartupShowToggle.IsOn;
        else if (ReferenceEquals(sender, SaveWindowSizeToggle)) general.IsSaveWindowSize = SaveWindowSizeToggle.IsOn;
        else if (ReferenceEquals(sender, WebEnabledToggle)) general.IsWebEnabled = WebEnabledToggle.IsOn;
        SaveConfig();
    }

    private void GeneralSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _config == null) return;
        var general = _config.General;
        if (ReferenceEquals(sender, StartPagePicker)) general.StartPage = StartPagePicker.SelectedIndex;
        else if (ReferenceEquals(sender, ThemePicker)) general.Theme = ThemePicker.SelectedIndex;
        else if (ReferenceEquals(sender, CategoryIntervalPicker) && CategoryIntervalPicker.SelectedIndex >= 0)
            general.CategoryUpdateIntervalHours = CategoryIntervalPicker.SelectedIndex < CategoryIntervals.Length
                ? CategoryIntervals[CategoryIntervalPicker.SelectedIndex]
                : _customCategoryIntervalHours;
        else if (ReferenceEquals(sender, FrequentCountPicker)) general.IndexPageFrequentUseNum = FrequentCountPicker.SelectedIndex + 1;
        SaveConfig();
        if (ReferenceEquals(sender, ThemePicker)) App.MainWindowInstance?.ApplyAppearance();
    }

    private async void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _config == null || LanguagePicker.SelectedIndex < 0) return;
        var language = LanguagePicker.SelectedIndex switch { 1 => "zh-CN", 2 => "en-US", _ => "auto" };
        if (_config.General.Language == language) return;
        _config.General.Language = language;
        SaveConfig();
        if (await ConfirmAsync("语言设置", "重新启动 Tai 以应用所选语言。现在重启吗？"))
        {
            try { RestartApplication(); }
            catch (Exception exception)
            {
                App.LogStartupException(exception);
                await ShowMessageAsync("需要手动重启", "请关闭并重新打开 Tai 以应用所选语言。");
            }
        }
    }

    private void BehaviorSetting_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _config == null) return;
        if (ReferenceEquals(sender, SleepWatchToggle)) _config.Behavior.IsSleepWatch = SleepWatchToggle.IsOn;
        else if (ReferenceEquals(sender, WhiteListToggle)) _config.Behavior.IsWhiteList = WhiteListToggle.IsOn;
        SaveConfig();
    }

    private void SettingsTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs e)
    {
        if (GeneralSection == null) return;
        GeneralSection.Visibility = sender.SelectedItem == GeneralTab ? Visibility.Visible : Visibility.Collapsed;
        LinksSection.Visibility = sender.SelectedItem == LinksTab ? Visibility.Visible : Visibility.Collapsed;
        RulesSection.Visibility = sender.SelectedItem == RulesTab ? Visibility.Visible : Visibility.Collapsed;
        DataSection.Visibility = sender.SelectedItem == DataTab ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = sender.SelectedItem == AboutTab ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddIgnoreProcess_Click(object sender, RoutedEventArgs e) => AddListItem(IgnoreProcessInput, IgnoreProcessList, _config.Behavior.IgnoreProcessList);
    private void AddIgnoreUrl_Click(object sender, RoutedEventArgs e) => AddListItem(IgnoreUrlInput, IgnoreUrlList, _config.Behavior.IgnoreURLList);
    private void AddWhiteList_Click(object sender, RoutedEventArgs e) => AddListItem(WhiteListInput, WhiteList, _config.Behavior.ProcessWhiteList);

    private void AddListItem(TextBox input, ListView listView, List<string> target)
    {
        var value = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(value) || target.Contains(value, StringComparer.OrdinalIgnoreCase)) return;
        target.Add(value);
        listView.Items.Add(value);
        input.Text = string.Empty;
        SaveConfig();
    }

    private void RemoveIgnoreProcess_Click(object sender, RoutedEventArgs e) => RemoveListItem(IgnoreProcessList, _config.Behavior.IgnoreProcessList);
    private void RemoveIgnoreUrl_Click(object sender, RoutedEventArgs e) => RemoveListItem(IgnoreUrlList, _config.Behavior.IgnoreURLList);
    private void RemoveWhiteList_Click(object sender, RoutedEventArgs e) => RemoveListItem(WhiteList, _config.Behavior.ProcessWhiteList);

    private void RemoveListItem(ListView listView, List<string> target)
    {
        if (listView.SelectedItem is not string value) return;
        target.Remove(value);
        listView.Items.Remove(value);
        SaveConfig();
    }

    private static void RenderList(ListView listView, IEnumerable<string> values)
    {
        listView.Items.Clear();
        foreach (var value in values) listView.Items.Add(value);
    }

    private List<string> GetList(string key) => key switch
    {
        "IgnoreProcessList" => _config.Behavior.IgnoreProcessList,
        "IgnoreUrlList" => _config.Behavior.IgnoreURLList,
        "WhiteList" => _config.Behavior.ProcessWhiteList,
        _ => throw new ArgumentException($"Unknown settings list: {key}")
    };

    private ListView GetListView(string key) => key switch
    {
        "IgnoreProcessList" => IgnoreProcessList,
        "IgnoreUrlList" => IgnoreUrlList,
        "WhiteList" => WhiteList,
        _ => throw new ArgumentException($"Unknown settings list: {key}")
    };

    private async void ImportList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.Text("选择文件"),
            Filter = L.IsEnglish ? "JSON settings (*.json)|*.json|All files (*.*)|*.*" : "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = key
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var imported = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(dialog.FileName));
            if (imported == null) { await ShowMessageAsync("导入失败", "文件格式有误或者数据为空，请选择有效的 JSON 列表文件。"); return; }
            if (!await ConfirmAsync("导入配置", "导入将覆盖当前列表，确定继续吗？")) return;
            var target = GetList(key);
            target.Clear();
            target.AddRange(imported.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
            RenderList(GetListView(key), target);
            SaveConfig();
            await ShowMessageAsync("导入完成", L.IsEnglish ? $"Imported {target.Count} settings." : $"已导入 {target.Count} 项配置。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("导入失败", "无法读取该 JSON 配置文件。");
        }
    }

    private async void ExportList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string key }) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L.Text("选择文件"),
            Filter = L.IsEnglish ? "JSON settings (*.json)|*.json" : "JSON 配置 (*.json)|*.json",
            FileName = L.IsEnglish ? $"{key}-settings.json" : $"{key}导出配置.json"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(GetList(key), new JsonSerializerOptions { WriteIndented = true }));
            await ShowMessageAsync("导出完成", "配置列表已导出。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("导出失败", "无法写入该 JSON 配置文件。");
        }
    }

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        _config.Links.Add(new LinkModel());
        SaveConfig();
        RenderLinks();
    }

    private void RenderLinks()
    {
        if (LinksPanel == null || _config?.Links == null) return;
        LinksPanel.Children.Clear();
        foreach (var link in _config.Links.ToList())
        {
            var nameBox = new TextBox { Header = L.Text("名称"), Text = link.Name, MinWidth = 180 };
            var processBox = new TextBox
            {
                Header = L.Text("关联进程"),
                Text = string.Join(Environment.NewLine, link.ProcessList ?? new List<string>()),
                PlaceholderText = L.Text("每行输入一个进程名称，不带 .exe"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 68
            };
            var saveButton = new Button { Content = L.Text("保存关联"), Style = (Style)Application.Current.Resources["TaiSubtleButtonStyle"] };
            saveButton.Click += (_, _) =>
            {
                link.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "新的关联" : nameBox.Text.Trim();
                link.ProcessList = processBox.Text.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                SaveConfig();
                RenderLinks();
            };
            var removeButton = new Button { Content = L.Text("删除关联"), Style = (Style)Application.Current.Resources["TaiSubtleButtonStyle"] };
            removeButton.Click += (_, _) =>
            {
                _config.Links.Remove(link);
                SaveConfig();
                RenderLinks();
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            actions.Children.Add(saveButton);
            actions.Children.Add(removeButton);
            var editor = new Border
            {
                Style = (Style)Application.Current.Resources["TaiCardStyle"],
                Child = new StackPanel { Spacing = 10 }
            };
            var panel = (StackPanel)editor.Child;
            panel.Children.Add(nameBox);
            panel.Children.Add(processBox);
            panel.Children.Add(actions);
            LinksPanel.Children.Add(editor);
        }
    }

    private async void DeleteData_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetMonthRange(DeleteStartPicker, DeleteEndPicker, out var start, out var end))
        {
            await ShowMessageAsync("时间范围错误", "请选择有效的开始和结束月份。");
            return;
        }
        if (!await ConfirmAsync("删除确认", L.IsEnglish
            ? $"Delete all statistics from {L.Month(start)} through {L.Month(end)}? This cannot be undone."
            : $"将删除 {start:yyyy年MM月} 至 {end:yyyy年MM月} 的所有统计数据，此操作不可恢复。")) return;
        try
        {
            _data.ClearRange(start, end);
            _webData.Clear(start, end);
            await ShowMessageAsync("操作已完成", "所选范围内的统计数据已删除。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("删除失败", "无法删除所选范围的数据。");
        }
    }

    private async void ExportData_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetMonthRange(ExportStartPicker, ExportEndPicker, out var start, out var end))
        {
            await ShowMessageAsync("时间范围错误", "请选择有效的开始和结束月份。");
            return;
        }
        using var folder = new System.Windows.Forms.FolderBrowserDialog { Description = L.Text("请选择导出位置") };
        if (folder.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        try
        {
            _data.ExportToExcel(folder.SelectedPath, start, end);
            _webData.Export(folder.SelectedPath, start, end);
            await ShowMessageAsync("导出完成", "应用和网站数据已导出为 xlsx 与 csv 文件。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("导出失败", "无法导出所选范围的数据。");
        }
    }

    private async void ImportData_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L.Text("选择 Tai 数据库"),
            Filter = L.IsEnglish ? "SQLite database (*.db)|*.db|All files (*.*)|*.*" : "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        var source = Path.GetFullPath(dialog.FileName);
        var destination = Path.Combine(AppContext.BaseDirectory, "Data", "data.db");
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (!await ConfirmAsync("导入数据库", "导入会替换当前统计数据，并自动备份现有数据库。确定继续吗？")) return;
        if (!await ImportOperationGate.WaitAsync(0)) return;

        try
        {
            var dataDirectory = Path.GetDirectoryName(destination)!;
            var staged = Path.Combine(dataDirectory, $"data.import-{Guid.NewGuid():N}.db");
            var backup = Path.Combine(dataDirectory, $"data.before-import-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db");
            var trackingStopped = false;
            var categoryMappingResetAttempted = false;
            var categoryMappingCleared = false;
            var databaseReplaced = false;
            Dictionary<string, int>? previousCategoryIds = null;
            var previousCategoriesInitialized = false;
            var previousWebsiteCategoriesInitialized = false;

            try
            {
                await App.CoreReady;
                Directory.CreateDirectory(dataDirectory);
                await Task.Run(() => RetryTransientDatabaseOperationAsync(() =>
                {
                    TryDeleteFile(staged);
                    CreateDatabaseSnapshot(source, staged);
                }));
                await Task.Run(() =>
                {
                    MigrateLegacyDatabase(staged);
                    ValidateDatabaseFile(staged);
                });

                _main.Stop();
                trackingStopped = true;
                await _catalogService.PauseAndWaitAsync();
                var general = _appConfig.GetConfig().General;
                previousCategoryIds = new Dictionary<string, int>(general.DefaultCategoryIds ?? new());
                previousCategoriesInitialized = general.DefaultCategoriesInitialized;
                previousWebsiteCategoriesInitialized = general.DefaultWebsiteCategoriesInitialized;
                categoryMappingResetAttempted = true;
                if (!_appConfig.UpdateAndSave(current =>
                    {
                        current.General.DefaultCategoryIds = new Dictionary<string, int>();
                        current.General.DefaultCategoriesInitialized = false;
                        current.General.DefaultWebsiteCategoriesInitialized = false;
                        return true;
                    }))
                    throw new IOException("Unable to invalidate category IDs before database import.");
                categoryMappingCleared = true;
                _database.CloseWriter();
                await RetryTransientDatabaseOperationAsync(() =>
                {
                    SQLiteConnection.ClearAllPools();
                    DeleteDatabaseSidecar(destination + "-wal");
                    DeleteDatabaseSidecar(destination + "-shm");
                    DeleteDatabaseSidecar(destination + "-journal");
                    TryDeleteFile(destination + ".version");
                    if (File.Exists(destination))
                        File.Replace(staged, destination, backup, true);
                    else
                        File.Move(staged, destination);
                });
                databaseReplaced = true;
            }
            catch (Exception exception)
            {
                TryDeleteFile(staged);
                var categoryMappingRestored = true;
                if (categoryMappingResetAttempted && !databaseReplaced)
                {
                    categoryMappingRestored = _appConfig.UpdateAndSave(current =>
                    {
                        current.General.DefaultCategoryIds = previousCategoryIds ?? new Dictionary<string, int>();
                        current.General.DefaultCategoriesInitialized = previousCategoriesInitialized;
                        current.General.DefaultWebsiteCategoriesInitialized = previousWebsiteCategoriesInitialized;
                        return categoryMappingCleared;
                    });
                }
                if (trackingStopped)
                {
                    try { _main.Start(); } catch { }
                }
                App.LogStartupException(exception);
                await ShowMessageAsync("导入失败", categoryMappingRestored
                    ? "所选文件不是有效的 Tai 数据库，或当前数据库无法替换。原数据未被修改。"
                    : "数据库未导入，但分类设置无法恢复。请重启 Tai 并检查现有分类。");
                return;
            }

            await ShowMessageAsync("导入完成", "数据库已导入并备份原数据。Tai 将重新启动以加载新数据。");
            try
            {
                RestartApplication();
            }
            catch (Exception exception)
            {
                App.LogStartupException(exception);
                await ShowMessageAsync("需要手动重启", "数据库已成功导入，但 Tai 无法自动重启。请关闭并重新打开软件。");
            }
        }
        finally
        {
            ImportOperationGate.Release();
        }
    }

    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = L.Text("导出 Tai 配置"), Filter = L.IsEnglish ? "JSON settings (*.json)|*.json" : "JSON 配置 (*.json)|*.json", FileName = L.IsEnglish ? "Tai-settings.json" : "Tai配置.json" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            await ShowMessageAsync("导出完成", "Tai 配置已导出。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("导出失败", "无法写入该配置文件。");
        }
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = L.Text("导入 Tai 配置"), Filter = L.IsEnglish ? "JSON settings (*.json)|*.json" : "JSON 配置 (*.json)|*.json" };
        if (dialog.ShowDialog() != true || !await ConfirmAsync("导入配置", "导入会覆盖当前 Tai 配置，确定继续吗？")) return;
        if (!await ImportOperationGate.WaitAsync(0)) return;
        var catalogPaused = false;
        var importedSaved = false;
        try
        {
            await App.CoreReady;
            var imported = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(dialog.FileName));
            if (imported == null) throw new InvalidDataException("Empty configuration");
            NormalizeConfig(imported);
            catalogPaused = true;
            await _catalogService.PauseAndWaitAsync();
            var previous = _appConfig.GetConfig();
            var previousGeneral = previous.General;
            var previousBehavior = previous.Behavior;
            var previousLinks = previous.Links;
            if (!_appConfig.UpdateAndSave(current =>
                {
                    var localGeneral = current.General ?? new GeneralModel();
                    imported.General.DefaultCategoryIds = new Dictionary<string, int>(
                        localGeneral.DefaultCategoryIds ?? new Dictionary<string, int>());
                    imported.General.DefaultCategoriesInitialized = localGeneral.DefaultCategoriesInitialized;
                    imported.General.DefaultWebsiteCategoriesInitialized = localGeneral.DefaultWebsiteCategoriesInitialized;
                    imported.General.LastCategoryCatalogUpdateUtc = localGeneral.LastCategoryCatalogUpdateUtc;
                    current.General = imported.General;
                    current.Behavior = imported.Behavior;
                    current.Links = imported.Links;
                    return true;
                }))
            {
                _appConfig.UpdateAndSave(current =>
                {
                    current.General = previousGeneral;
                    current.Behavior = previousBehavior;
                    current.Links = previousLinks;
                    return false;
                });
                throw new IOException("Unable to save imported configuration.");
            }
            importedSaved = true;
            _config = _appConfig.GetConfig();
            PopulateControls();
            App.MainWindowInstance?.ApplyAppearance();
            await ShowMessageAsync("导入完成", "Tai 配置已导入。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync(importedSaved ? "配置已保存" : "导入失败",
                importedSaved ? "配置已导入，但界面未能刷新。请重启 Tai。" : "无法读取该配置文件或保存设置。");
        }
        finally
        {
            if (catalogPaused) _catalogService.ResumeAutomaticUpdates();
            ImportOperationGate.Release();
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/ethandong16/Tai.WinUI/releases"));
    }

    private static bool TryGetMonthRange(CalendarDatePicker startPicker, CalendarDatePicker endPicker, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (startPicker.Date is not DateTimeOffset startValue || endPicker.Date is not DateTimeOffset endValue) return false;
        start = new DateTime(startValue.Year, startValue.Month, 1);
        end = new DateTime(endValue.Year, endValue.Month, DateTime.DaysInMonth(endValue.Year, endValue.Month), 23, 59, 59);
        return start <= end;
    }

    private static void CreateDatabaseSnapshot(string source, string destination)
    {
        using var sourceConnection = new SQLiteConnection($"Data Source={source};Read Only=True;FailIfMissing=True;BusyTimeout=60000;");
        using var destinationConnection = new SQLiteConnection($"Data Source={destination};BusyTimeout=60000;");
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection, "main", "main", -1, null, 0);
    }

    // Pre-1.0.0.3 databases stored process fields directly on the log tables.
    private static void MigrateLegacyDatabase(string path)
    {
        using var connection = new SQLiteConnection($"Data Source={path};BusyTimeout=60000;");
        connection.Open();

        var tables = GetTableNames(connection);
        if (!tables.Contains("DailyLogModels") && !tables.Contains("HoursLogModels") && !tables.Contains("AppModels")) return;

        var dailyColumns = GetColumnNames(connection, "DailyLogModels");
        var hoursColumns = GetColumnNames(connection, "HoursLogModels");

        using var transaction = connection.BeginTransaction();
        EnsureCurrentLogTable(connection, transaction, tables, "DailyLogModels", "[Date] datetime, [Time] int NULL DEFAULT 0, [AppModelID] int NULL DEFAULT 0");
        EnsureCurrentLogTable(connection, transaction, tables, "HoursLogModels", "[DataTime] datetime, [Time] int NULL DEFAULT 0, [AppModelID] int NULL DEFAULT 0");
        dailyColumns = GetColumnNames(connection, "DailyLogModels", transaction);
        hoursColumns = GetColumnNames(connection, "HoursLogModels", transaction);
        EnsureCurrentAppTable(connection, transaction, tables);
        var appColumns = GetColumnNames(connection, "AppModels", transaction);
        if (!appColumns.Contains("ID"))
            throw new InvalidDataException("Tai AppModels table is missing ID.");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "Name", "nvarchar NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "Alias", "nvarchar NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "Description", "nvarchar NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "File", "nvarchar NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "CategoryID", "int NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "IconFile", "nvarchar NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "AppModels", appColumns, "TotalTime", "int NULL DEFAULT 0");

        if (!dailyColumns.Contains("Date") || !dailyColumns.Contains("Time"))
            throw new InvalidDataException("Tai DailyLogModels table is missing Date or Time.");
        if (!hoursColumns.Contains("DataTime") || !hoursColumns.Contains("Time"))
            throw new InvalidDataException("Tai HoursLogModels table is missing DataTime or Time.");

        AddAppModelIdColumn(connection, transaction, "DailyLogModels", dailyColumns);
        AddAppModelIdColumn(connection, transaction, "HoursLogModels", hoursColumns);
        dailyColumns = GetColumnNames(connection, "DailyLogModels", transaction);
        hoursColumns = GetColumnNames(connection, "HoursLogModels", transaction);

        var sources = new List<string>();
        var hasDailyLegacyLogs = dailyColumns.Contains("ProcessName");
        if (hasDailyLegacyLogs)
        {
            var description = dailyColumns.Contains("ProcessDescription") ? "ProcessDescription" : "''";
            var file = dailyColumns.Contains("File") ? "File" : "''";
            sources.Add($"SELECT ProcessName AS Name, {description} AS Description, {file} AS File, Time AS TotalTime FROM DailyLogModels");
        }
        if (hoursColumns.Contains("ProcessName"))
        {
            var file = hoursColumns.Contains("File") ? "File" : "''";
            var totalTime = hasDailyLegacyLogs ? "0" : "Time";
            sources.Add($"SELECT ProcessName AS Name, '' AS Description, {file} AS File, {totalTime} AS TotalTime FROM HoursLogModels");
        }

        if (sources.Count > 0)
        {
            using var insertApps = connection.CreateCommand();
            insertApps.Transaction = transaction;
            insertApps.CommandText = $"""
                    INSERT INTO AppModels (Name, Description, File, TotalTime)
                    SELECT source.Name, MAX(source.Description), MAX(source.File), COALESCE(SUM(source.TotalTime), 0)
                    FROM ({string.Join(" UNION ALL ", sources)}) AS source
                    WHERE source.Name IS NOT NULL AND TRIM(source.Name) <> ''
                      AND NOT EXISTS (SELECT 1 FROM AppModels existing WHERE existing.Name = source.Name)
                    GROUP BY source.Name
                    """;
            insertApps.ExecuteNonQuery();
        }

        UpdateLegacyLogAppIds(connection, transaction, "DailyLogModels", dailyColumns);
        UpdateLegacyLogAppIds(connection, transaction, "HoursLogModels", hoursColumns);
        RebuildLegacyLogTable(connection, transaction, "DailyLogModels", dailyColumns, "Date");
        RebuildLegacyLogTable(connection, transaction, "HoursLogModels", hoursColumns, "DataTime");
        transaction.Commit();
    }

    private static void RebuildLegacyLogTable(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        string tableName,
        HashSet<string> columns,
        string timeColumn)
    {
        var hasLegacyColumns = columns.Contains("ProcessName")
            || columns.Contains("ProcessDescription")
            || columns.Contains("File");
        if (!hasLegacyColumns && columns.Contains("ID") && columns.Contains(timeColumn)
            && columns.Contains("Time") && columns.Contains("AppModelID")) return;

        if (!columns.Contains(timeColumn) || !columns.Contains("Time"))
            throw new InvalidDataException($"Tai {tableName} table is missing {timeColumn} or Time.");

        var temporaryName = $"{tableName}_import_{Guid.NewGuid():N}";
        var idExpression = columns.Contains("ID") ? "[ID]" : "rowid";
        var appIdExpression = columns.Contains("AppModelID") ? "COALESCE([AppModelID], 0)" : "0";

        using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = $"CREATE TABLE [{temporaryName}] ([ID] INTEGER PRIMARY KEY, [{timeColumn}] datetime, [Time] int NULL DEFAULT 0, [AppModelID] int NULL DEFAULT 0)";
            create.ExecuteNonQuery();
        }

        using (var copy = connection.CreateCommand())
        {
            copy.Transaction = transaction;
            copy.CommandText = $"INSERT INTO [{temporaryName}] ([ID], [{timeColumn}], [Time], [AppModelID]) SELECT {idExpression}, [{timeColumn}], COALESCE([Time], 0), {appIdExpression} FROM [{tableName}]";
            copy.ExecuteNonQuery();
        }

        using (var replace = connection.CreateCommand())
        {
            replace.Transaction = transaction;
            replace.CommandText = $"DROP TABLE [{tableName}]; ALTER TABLE [{temporaryName}] RENAME TO [{tableName}]";
            replace.ExecuteNonQuery();
        }
    }

    private static void EnsureCurrentLogTable(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        HashSet<string> tables,
        string tableName,
        string columns)
    {
        if (tables.Contains(tableName)) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"CREATE TABLE [{tableName}] ([ID] INTEGER PRIMARY KEY, {columns})";
        command.ExecuteNonQuery();
        tables.Add(tableName);
    }

    private static void EnsureCurrentAppTable(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        HashSet<string> tables)
    {
        if (tables.Contains("AppModels")) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE [AppModels] (
                [ID] INTEGER PRIMARY KEY,
                [Name] nvarchar NULL DEFAULT '',
                [Alias] nvarchar NULL DEFAULT '',
                [Description] nvarchar NULL DEFAULT '',
                [File] nvarchar NULL DEFAULT '',
                [CategoryID] int NULL DEFAULT 0,
                [IconFile] nvarchar NULL DEFAULT '',
                [TotalTime] int NULL DEFAULT 0
            )
            """;
        command.ExecuteNonQuery();
        tables.Add("AppModels");
    }

    private static void EnsureColumn(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        string tableName,
        HashSet<string> columns,
        string columnName,
        string definition)
    {
        if (columns.Contains(columnName)) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {definition}";
        command.ExecuteNonQuery();
        columns.Add(columnName);
    }

    private static void AddAppModelIdColumn(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        string tableName,
        HashSet<string> columns)
    {
        if (columns.Count == 0 || columns.Contains("AppModelID")) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"ALTER TABLE [{tableName}] ADD COLUMN [AppModelID] INTEGER NOT NULL DEFAULT 0";
        command.ExecuteNonQuery();
    }

    private static void UpdateLegacyLogAppIds(
        SQLiteConnection connection,
        SQLiteTransaction transaction,
        string tableName,
        HashSet<string> columns)
    {
        if (!columns.Contains("ProcessName")) return;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            UPDATE [{tableName}]
            SET [AppModelID] = COALESCE(
                (SELECT [ID] FROM [AppModels] WHERE [AppModels].[Name] = [{tableName}].[ProcessName] LIMIT 1),
                0)
            """;
        command.ExecuteNonQuery();
    }

    private static HashSet<string> GetTableNames(SQLiteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var reader = command.ExecuteReader();
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) tables.Add(reader.GetString(0));
        return tables;
    }

    private static HashSet<string> GetColumnNames(
        SQLiteConnection connection,
        string tableName,
        SQLiteTransaction? transaction = null)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (transaction == null && !GetTableNames(connection).Contains(tableName)) return columns;

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info([{tableName}])";
        using var reader = command.ExecuteReader();
        while (reader.Read()) columns.Add(reader.GetString(1));
        return columns;
    }

    private static void ValidateDatabaseFile(string path)
    {
        using var connection = new SQLiteConnection($"Data Source={path};Read Only=True;FailIfMissing=True;");
        connection.Open();

        using var integrityCommand = connection.CreateCommand();
        integrityCommand.CommandText = "PRAGMA quick_check(1)";
        if (!string.Equals(integrityCommand.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SQLite integrity check failed.");

        using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name COLLATE NOCASE IN ('AppModels','DailyLogModels','HoursLogModels')";
        if (Convert.ToInt32(schemaCommand.ExecuteScalar()) != 3)
            throw new InvalidDataException("Required Tai tables are missing.");
    }

    private static void DeleteDatabaseSidecar(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static async Task RetryTransientDatabaseOperationAsync(Action operation)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (Exception exception) when (attempt < 7 && IsTransientDatabaseConflict(exception))
            {
                await Task.Delay(200 * (attempt + 1));
            }
        }
    }

    private static bool IsTransientDatabaseConflict(Exception exception) => exception switch
    {
        SQLiteException sqlite => sqlite.ResultCode is SQLiteErrorCode.Busy or SQLiteErrorCode.Locked,
        IOException io => io.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021),
        _ => false
    };

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // File.Replace will report a useful failure if SQLite still owns the database.
        }
    }

    private static void RestartApplication()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
            throw new InvalidOperationException("Cannot locate the Tai executable.");

        Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        Application.Current.Exit();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = L.Text(title),
            Content = L.Text(message),
            PrimaryButtonText = L.Text("确定"),
            CloseButtonText = L.Text("取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = L.Text(title),
            Content = L.Text(message),
            CloseButtonText = L.Text("确定"),
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void SaveConfig() => _appConfig.Save();

}
