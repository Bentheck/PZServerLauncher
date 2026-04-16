using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Services;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Runtime;
using PZServerLauncher.App.Views;
using System.Threading.Tasks;
using Avalonia.Threading;

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
            .AddSingleton<ILauncherRuntime>(_ => new LauncherRuntime(LauncherStorageRootResolver.Resolve()))
            .AddSingleton<DesktopShutdownService>()
            .AddSingleton<ShutdownWarningDialogService>()
            .AddSingleton<DesktopShellService>()
            .AddSingleton<FolderPickerService>()
            .AddSingleton<CreateProfileDialogService>()
            .AddSingleton<ConsoleWorkspaceStateService>()
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
            _serviceProvider.GetRequiredService<CreateProfileDialogService>().Initialize(mainWindow);
            desktop.Exit += OnDesktopExit;

            if (ScreenshotCaptureOptions.IsEnabled &&
                mainWindow.DataContext is WorkspaceShellViewModel shell &&
                ScreenshotCaptureOptions.OutputDirectory is { } outputDirectory)
            {
                mainWindow.Opened += async (_, _) =>
                {
                    try
                    {
                        _desktopLogService?.Info($"Screenshot capture starting in {outputDirectory}.");
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
                        await ScreenshotCaptureRunner.RunAsync(desktop, mainWindow, shell, outputDirectory);
                    }
                    catch (Exception ex)
                    {
                        _desktopLogService?.Error("Screenshot capture failed.", ex);
                        desktop.Shutdown(-1);
                    }
                };
            }
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
