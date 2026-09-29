using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Core.Librarys.SQLite;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using System.IO;
using System.Globalization;
using System.Text.Json;
using Microsoft.Windows.Globalization;

namespace Tai.WinUI;

public partial class App : Application
{
    private MainWindow? _window;
    internal static MainWindow? MainWindowInstance { get; private set; }
    public static IServiceProvider Services { get; private set; } = null!;
    private static readonly TaskCompletionSource<object?> CoreReadySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static Task CoreReady => CoreReadySource.Task;

    public App()
    {
        ConfigureLanguage();
        UnhandledException += App_UnhandledException;
        InitializeComponent();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IDatabase, Database>();
        serviceCollection.AddSingleton<IAppManager, AppManager>();
        serviceCollection.AddSingleton<IWindowManager, WindowManager>();
        serviceCollection.AddSingleton<IAppTimerServicer, AppTimerServicer>();
        serviceCollection.AddSingleton<IAppObserver, AppObserver>();
        serviceCollection.AddSingleton<IWebServer, WebServer>();
        serviceCollection.AddSingleton<IMain, Main>();
        serviceCollection.AddSingleton<IData, Data>();
        serviceCollection.AddSingleton<IWebData, WebData>();
        serviceCollection.AddSingleton<ISleepdiscover, Sleepdiscover>();
        serviceCollection.AddSingleton<IAppConfig, AppConfig>();
        serviceCollection.AddSingleton<IDateTimeObserver, DateTimeObserver>();
        serviceCollection.AddSingleton<IAppData, AppData>();
        serviceCollection.AddSingleton<ICategorys, Categorys>();
        serviceCollection.AddSingleton<ICategoryCatalogService, CategoryCatalogService>();
        serviceCollection.AddSingleton<IWebFilter, WebFilter>();
        serviceCollection.AddSingleton<Tai.WinUI.Services.IUsageDataProvider, Tai.WinUI.Services.CoreUsageDataProvider>();
        Services = serviceCollection.BuildServiceProvider();
    }

    private static void ConfigureLanguage()
    {
        var selected = "auto";
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "AppConfig.json");
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("General", out var general)
                    && general.TryGetProperty("Language", out var language))
                    selected = language.GetString() ?? "auto";
            }
        }
        catch (Exception)
        {
            selected = "auto";
        }

        var languageTag = selected switch
        {
            "zh-CN" => "zh-CN",
            "en-US" => "en-US",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en-US"
        };
        ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        var culture = CultureInfo.GetCultureInfo(languageTag);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var smokeTest = Environment.GetCommandLineArgs().Contains("--smoke-test");
        try
        {
            _window = new MainWindow();
            MainWindowInstance = _window;
            _window.Activate();
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Data"));
            System.Data.Entity.DbConfiguration.SetConfiguration(new SQLiteConfiguration());
            var main = Services.GetRequiredService<IMain>();
            await main.RunAsync();
            _window.ApplyStartupSettings();
            CoreReadySource.TrySetResult(null);
            if (!smokeTest)
                _ = WarmUsageCacheAsync();
            if (smokeTest)
            {
                var categoryService = Services.GetRequiredService<ICategorys>();
                var general = Services.GetRequiredService<IAppConfig>().GetConfig().General;
                if (!general.DefaultCategoriesInitialized || general.DefaultCategoryIds.Count < 9 ||
                    general.DefaultCategoryIds.Values.Any(id => categoryService.GetCategory(id) == null))
                {
                    throw new InvalidOperationException("Default application categories were not initialized.");
                }
                if (!_window.IsPerMonitorDpiAware())
                {
                    throw new InvalidOperationException("The WinUI window is not per-monitor DPI aware.");
                }
                await Services.GetRequiredService<Tai.WinUI.Services.IUsageDataProvider>().GetTodayAsync();
                await _window.VerifyPagesAsync();
                main.Stop();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-smoke.ok"),
                    "Per-monitor DPI, responsive shell and seven pages at 1240/920/700/480 widths, dashboard detail navigation, website source filter, period ranges, themes, title bar contrast, icons, tracker initialization and database queries passed.");
                Exit();
            }
        }
        catch (Exception exception)
        {
            CoreReadySource.TrySetException(exception);
            LogStartupException(exception);
            if (smokeTest || _window == null)
            {
                Environment.ExitCode = 1;
                Exit();
            }
            else
            {
                _window.ShowStartupError();
            }
        }
    }

    private static async Task WarmUsageCacheAsync()
    {
        try
        {
            await Services.GetRequiredService<Tai.WinUI.Services.IUsageDataProvider>()
                .PreloadAsync(DateTime.Today);
        }
        catch (Exception exception)
        {
            LogStartupException(exception);
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogStartupException(e.Exception);

        // Unexpected UI failures are logged, but must not be silently swallowed.
    }

    public static void LogStartupException(Exception exception)
    {
        try
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "Log");
            Directory.CreateDirectory(logDirectory);
            File.AppendAllText(
                Path.Combine(logDirectory, "startup.log"),
                $"[{DateTime.Now:O}] HResult=0x{exception.HResult:X8}\r\n{exception}\r\n");
        }
        catch
        {
            // Preserve the original exception path if logging is unavailable.
        }
    }
}
