using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Core.Librarys.SQLite;
using Core.Servicers.Instances;
using Core.Servicers.Interfaces;
using System.IO;

namespace Tai.WinUI;

public partial class App : Application
{
    private Window? _window;
    public static IServiceProvider Services { get; private set; } = null!;
    private static readonly TaskCompletionSource<object?> CoreReadySource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static Task CoreReady => CoreReadySource.Task;

    public App()
    {
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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Data"));
        var main = Services.GetRequiredService<IMain>();
        main.OnStarted += (_, _) => CoreReadySource.TrySetResult(null);
        main.Run();
        _window = new MainWindow();
        _window.Activate();
    }
}
