using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Data.SQLite;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using Microsoft.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Tai.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    private readonly IAppConfig _appConfig;
    private readonly IData _data;
    private readonly IWebData _webData;
    private readonly IMain _main;
    private readonly IDatabase _database;
    private ConfigModel _config = null!;
    private bool _isLoading;

    public SettingsPage()
    {
        InitializeComponent();
        _appConfig = App.Services.GetRequiredService<IAppConfig>();
        _data = App.Services.GetRequiredService<IData>();
        _webData = App.Services.GetRequiredService<IWebData>();
        _main = App.Services.GetRequiredService<IMain>();
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
        config.Behavior ??= new BehaviorModel();
        config.Links ??= new List<LinkModel>();
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
            StartAtBootToggle.IsOn = general.IsStartatboot;
            StartupShowToggle.IsOn = general.IsStartupShowMainWindow;
            SaveWindowSizeToggle.IsOn = general.IsSaveWindowSize;
            WebEnabledToggle.IsOn = general.IsWebEnabled;
            StartPagePicker.SelectedIndex = Math.Clamp(general.StartPage, 0, 3);
            ThemePicker.SelectedIndex = Math.Clamp(general.Theme, 0, 2);
            var themeColor = ParseColor(general.ThemeColor);
            ThemeColorPicker.Color = themeColor;
            ThemeColorSwatch.Background = new SolidColorBrush(themeColor);
            FillCountPicker(FrequentCountPicker, 10, general.IndexPageFrequentUseNum);
            FillCountPicker(MoreCountPicker, 20, general.IndexPageMoreNum);

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
            VersionText.Text = $"Tai 版本号 {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"}";
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
        else if (ReferenceEquals(sender, FrequentCountPicker)) general.IndexPageFrequentUseNum = FrequentCountPicker.SelectedIndex + 1;
        else if (ReferenceEquals(sender, MoreCountPicker)) general.IndexPageMoreNum = MoreCountPicker.SelectedIndex + 1;
        SaveConfig();
        if (ReferenceEquals(sender, ThemePicker)) App.MainWindowInstance?.ApplyAppearance();
    }

    private void ThemeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (!_isLoading && _config != null) {
            _config.General.ThemeColor = $"#{sender.Color.R:X2}{sender.Color.G:X2}{sender.Color.B:X2}";
            ThemeColorSwatch.Background = new SolidColorBrush(sender.Color);
            SaveConfig();
            App.MainWindowInstance?.ApplyAppearance();
        }
    }

    private void BehaviorSetting_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _config == null) return;
        if (ReferenceEquals(sender, SleepWatchToggle)) _config.Behavior.IsSleepWatch = SleepWatchToggle.IsOn;
        else if (ReferenceEquals(sender, WhiteListToggle)) _config.Behavior.IsWhiteList = WhiteListToggle.IsOn;
        SaveConfig();
    }

    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // All tabs edit the same persisted ConfigModel.
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
            Title = "选择文件",
            Filter = "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*",
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
            await ShowMessageAsync("导入完成", $"已导入 {target.Count} 项配置。");
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
            Title = "选择文件",
            Filter = "JSON 配置 (*.json)|*.json",
            FileName = $"{key}导出配置.json"
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
            var nameBox = new TextBox { Header = "名称", Text = link.Name, MinWidth = 180 };
            var processBox = new TextBox
            {
                Header = "关联进程",
                Text = string.Join(Environment.NewLine, link.ProcessList ?? new List<string>()),
                PlaceholderText = "每行输入一个进程名称，不带 .exe",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 68
            };
            var saveButton = new Button { Content = "保存关联", Style = (Style)Application.Current.Resources["TaiSubtleButtonStyle"] };
            saveButton.Click += (_, _) =>
            {
                link.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? "新的关联" : nameBox.Text.Trim();
                link.ProcessList = processBox.Text.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                SaveConfig();
                RenderLinks();
            };
            var removeButton = new Button { Content = "删除关联", Style = (Style)Application.Current.Resources["TaiSubtleButtonStyle"] };
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
        if (!await ConfirmAsync("删除确认", $"将删除 {start:yyyy年MM月} 至 {end:yyyy年MM月} 的所有统计数据，此操作不可恢复。")) return;
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
        using var folder = new System.Windows.Forms.FolderBrowserDialog { Description = "请选择导出位置" };
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
            Title = "选择 Tai 数据库",
            Filter = "SQLite 数据库 (*.db)|*.db|所有文件 (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true) return;
        var source = Path.GetFullPath(dialog.FileName);
        var destination = Path.Combine(AppContext.BaseDirectory, "Data", "data.db");
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (!await ConfirmAsync("导入数据库", "导入会替换当前统计数据，并自动备份现有数据库。确定继续吗？")) return;

        var dataDirectory = Path.GetDirectoryName(destination)!;
        var staged = Path.Combine(dataDirectory, $"data.import-{Guid.NewGuid():N}.db");
        var backup = Path.Combine(dataDirectory, $"data.before-import-{DateTime.Now:yyyyMMddHHmmss}.db");
        var trackingStopped = false;

        try
        {
            Directory.CreateDirectory(dataDirectory);
            await Task.Run(() => CreateDatabaseSnapshot(source, staged));
            ValidateDatabaseFile(staged);

            _main.Stop();
            trackingStopped = true;
            _database.CloseWriter();
            DeleteDatabaseSidecar(destination + "-wal");
            DeleteDatabaseSidecar(destination + "-shm");
            if (File.Exists(destination))
            {
                File.Replace(staged, destination, backup, true);
            }
            else
            {
                File.Move(staged, destination);
            }
        }
        catch (Exception exception)
        {
            TryDeleteFile(staged);
            if (trackingStopped)
            {
                try { _main.Start(); } catch { }
            }
            App.LogStartupException(exception);
            await ShowMessageAsync("导入失败", "所选文件不是有效的 Tai 数据库，或当前数据库无法替换。原数据未被修改。");
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

    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "导出 Tai 配置", Filter = "JSON 配置 (*.json)|*.json", FileName = "Tai配置.json" };
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
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "导入 Tai 配置", Filter = "JSON 配置 (*.json)|*.json" };
        if (dialog.ShowDialog() != true || !await ConfirmAsync("导入配置", "导入会覆盖当前 Tai 配置，确定继续吗？")) return;
        try
        {
            var imported = JsonSerializer.Deserialize<ConfigModel>(File.ReadAllText(dialog.FileName));
            if (imported == null) throw new InvalidDataException("Empty configuration");
            NormalizeConfig(imported);
            var current = _appConfig.GetConfig();
            current.General = imported.General;
            current.Behavior = imported.Behavior;
            current.Links = imported.Links;
            _config = current;
            _appConfig.Save();
            PopulateControls();
            App.MainWindowInstance?.ApplyAppearance();
            await ShowMessageAsync("导入完成", "Tai 配置已导入。");
        }
        catch (Exception exception)
        {
            App.LogStartupException(exception);
            await ShowMessageAsync("导入失败", "无法读取该配置文件。");
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Planshit/Tai/releases"));
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
        using var sourceConnection = new SQLiteConnection($"Data Source={source};Read Only=True;FailIfMissing=True;");
        using var destinationConnection = new SQLiteConnection($"Data Source={destination};");
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection, "main", "main", -1, null, 0);
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
        schemaCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('AppModels','DailyLogModels','HoursLogModels')";
        if (Convert.ToInt32(schemaCommand.ExecuteScalar()) != 3)
            throw new InvalidDataException("Required Tai tables are missing.");
    }

    private static void DeleteDatabaseSidecar(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

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
            Title = title,
            Content = message,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void SaveConfig() => _appConfig.Save();

    private static Windows.UI.Color ParseColor(string? value)
    {
        var hex = (value ?? string.Empty).TrimStart('#');
        if (hex.Length == 6 && byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r)
            && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g)
            && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return ColorHelper.FromArgb(255, r, g, b);
        }
        return ColorHelper.FromArgb(255, 43, 32, 217);
    }
}
