using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Services;
using PZServerLauncher.App.Views;
using System.Threading.Tasks;

namespace PZServerLauncher.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private DesktopLogService? _desktopLogService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _serviceProvider = new ServiceCollection()
            .AddSingleton<DesktopLogService>()
            .AddSingleton<LocalHostApiClient>()
            .AddSingleton<RuntimeEventStream>()
            .AddSingleton<DesktopShellService>()
            .AddSingleton<FolderPickerService>()
            .AddSingleton<MainWindowViewModel>()
            .AddSingleton<WorkspaceShellViewModel>()
            .BuildServiceProvider();

        _desktopLogService = _serviceProvider.GetRequiredService<DesktopLogService>();
        _desktopLogService.Info("Desktop application starting.");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<WorkspaceShellViewModel>(),
            };
            desktop.MainWindow = mainWindow;
            _serviceProvider.GetRequiredService<DesktopShellService>().Initialize(desktop, mainWindow);
            _serviceProvider.GetRequiredService<FolderPickerService>().Initialize(mainWindow);
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _desktopLogService?.Error("Unhandled app-domain exception.", exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _desktopLogService?.Error("Unobserved task exception.", e.Exception);
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _desktopLogService?.Info("Desktop application exiting.");
    }
}
