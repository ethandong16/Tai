using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Core.Librarys.SQLite;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using System.IO;

namespace Tai.WinUI;

public partial class App : Application
{
    private MainWindow? _window;
    public static IServiceProvider Services { get; private set; } = null!;
    private static readonly TaskCompletionSource<object?> CoreReadySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static Task CoreReady => CoreReadySource.Task;

    public App()
    {
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
        serviceCollection.AddSingleton<IWebFilter, WebFilter>();
        serviceCollection.AddSingleton<Tai.WinUI.Services.IUsageDataProvider, Tai.WinUI.Services.CoreUsageDataProvider>();
        Services = serviceCollection.BuildServiceProvider();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var smokeTest = Environment.GetCommandLineArgs().Contains("--smoke-test");
        try
        {
            _window = new MainWindow();
            _window.Activate();
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Data"));
            System.Data.Entity.DbConfiguration.SetConfiguration(new SQLiteConfiguration());
            var main = Services.GetRequiredService<IMain>();
            await main.RunAsync();
            CoreReadySource.TrySetResult(null);
            if (smokeTest)
            {
                await Services.GetRequiredService<Tai.WinUI.Services.IUsageDataProvider>().GetTodayAsync();
                await _window.VerifyPagesAsync();
                main.Stop();
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "startup-smoke.ok"),
                    "Window, seven pages, tracker initialization and database query passed.");
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
