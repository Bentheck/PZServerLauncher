using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PZServerLauncher.App.Services;

public sealed class DesktopShellService(DesktopLogService logService) : IDisposable
{
    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;
    private Window? _mainWindow;
    private TrayIcon? _trayIcon;
    private bool _allowExit;

    public void Initialize(IClassicDesktopStyleApplicationLifetime desktopLifetime, Window mainWindow)
    {
        _desktopLifetime = desktopLifetime;
        _mainWindow = mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        logService.Info("Desktop shell initialized.");

        var menu = new NativeMenu();
        var openItem = new NativeMenuItem("Open Launcher");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Add(openItem);

        var exitItem = new NativeMenuItem("Exit Desktop");
        exitItem.Click += (_, _) => ExitDesktop();
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "PZ Server Launcher",
            Icon = LoadWindowIcon(),
            Menu = menu,
            IsVisible = true,
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        logService.Info("Showing launcher window.");
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ExitDesktop()
    {
        if (_desktopLifetime is null || _mainWindow is null)
        {
            return;
        }

        _allowExit = true;
        logService.Info("Exit requested from desktop shell.");
        _mainWindow.Close();
        _desktopLifetime.Shutdown();
    }

    public void Dispose()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
        }

        _trayIcon?.Dispose();
        logService.Info("Desktop shell disposed.");
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow?.Hide();
        logService.Info("Launcher window hidden to tray.");
    }

    private static WindowIcon LoadWindowIcon()
    {
        var asset = AssetLoader.Open(new Uri("avares://PZServerLauncher.App/Assets/avalonia-logo.ico"));
        return new WindowIcon(new Bitmap(asset));
    }
}
