using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PZServerLauncher.App.Services;

public sealed class DesktopShellService : IDisposable
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
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private static WindowIcon LoadWindowIcon()
    {
        var asset = AssetLoader.Open(new Uri("avares://PZServerLauncher.App/Assets/avalonia-logo.ico"));
        return new WindowIcon(new Bitmap(asset));
    }
}
