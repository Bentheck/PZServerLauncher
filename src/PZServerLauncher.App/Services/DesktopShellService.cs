using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using System.Diagnostics;

namespace PZServerLauncher.App.Services;

public sealed class DesktopShellService : IDisposable
{
    private readonly DesktopLogService _logService;
    private readonly ShutdownWarningDialogService _shutdownWarningDialogService;
    private readonly DesktopShutdownService? _shutdownService;
    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;
    private Window? _mainWindow;
    private TrayIcon? _trayIcon;
    private bool _allowExit;
    private bool _exitConfirmationOpen;
    private bool _exitInProgress;

    public DesktopShellService(
        DesktopLogService logService,
        ShutdownWarningDialogService? shutdownWarningDialogService = null,
        DesktopShutdownService? shutdownService = null)
    {
        _logService = logService;
        _shutdownWarningDialogService = shutdownWarningDialogService ?? new ShutdownWarningDialogService();
        _shutdownService = shutdownService;
    }

    public void Initialize(IClassicDesktopStyleApplicationLifetime desktopLifetime, Window mainWindow)
    {
        _desktopLifetime = desktopLifetime;
        _mainWindow = mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        _shutdownWarningDialogService.Initialize(mainWindow);
        _logService.Info("Desktop shell initialized.");
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _logService.Info("Showing launcher window.");
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = false;
        }

        _mainWindow.ShowInTaskbar = true;
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

        _logService.Info("Exit requested from desktop shell.");
        _mainWindow.Close();
    }

    public void OpenExternalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            _logService.Info($"Opened external URL: {url}");
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to open external URL: {url}", ex);
        }
    }

    public void Dispose()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= OnMainWindowClosing;
        }

        if (_trayIcon is not null)
        {
            _trayIcon.Clicked -= OnTrayIconClicked;
            _trayIcon.Dispose();
        }

        _logService.Info("Desktop shell disposed.");
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowExit)
        {
            return;
        }

        e.Cancel = true;
        if (_exitConfirmationOpen || _exitInProgress)
        {
            return;
        }

        _ = RequestShutdownAsync();
    }

    private async Task RequestShutdownAsync()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _exitConfirmationOpen = true;

        try
        {
            var choice = await _shutdownWarningDialogService.ShowAsync();
            if (choice == ShutdownWarningChoice.KeepRunning)
            {
                _logService.Info("Desktop shutdown cancelled by the user.");
                return;
            }

            if (choice == ShutdownWarningChoice.SendToTray)
            {
                SendToTray();
                _logService.Info("Launcher sent to tray from close dialog.");
                return;
            }

            _exitInProgress = true;
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (_shutdownService is not null)
            {
                await _shutdownService.StopAppAndServersAsync(cancellationSource.Token);
            }

            _allowExit = true;
            _mainWindow.Close();
            _desktopLifetime?.Shutdown();
        }
        catch (Exception ex)
        {
            _allowExit = false;
            _exitInProgress = false;
            _logService.Error("Desktop shutdown failed.", ex);
        }
        finally
        {
            _exitConfirmationOpen = false;
        }
    }

    private void SendToTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!EnsureTrayIcon())
        {
            _logService.Info("Tray icon could not be created. Keeping launcher window open.");
            return;
        }

        _mainWindow.ShowInTaskbar = false;
        _mainWindow.Hide();
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = true;
        }
    }

    private bool EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return true;
        }

        try
        {
            var openItem = new NativeMenuItem("Open Launcher");
            openItem.Click += (_, _) => ShowMainWindow();

            var closeItem = new NativeMenuItem("Close Everything");
            closeItem.Click += (_, _) => _ = CloseEverythingFromTrayAsync();

            var menu = new NativeMenu();
            menu.Items.Add(openItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(closeItem);

            _trayIcon = new TrayIcon
            {
                Icon = LoadTrayIcon(),
                ToolTipText = "PZ Server Launcher",
                Menu = menu,
                IsVisible = false,
            };
            _trayIcon.Clicked += OnTrayIconClicked;
            return true;
        }
        catch (Exception ex)
        {
            _trayIcon = null;
            _logService.Error("Failed to create launcher tray icon.", ex);
            return false;
        }
    }

    private async Task CloseEverythingFromTrayAsync()
    {
        if (_mainWindow is null || _exitInProgress)
        {
            return;
        }

        _exitInProgress = true;

        try
        {
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            if (_shutdownService is not null)
            {
                await _shutdownService.StopAppAndServersAsync(cancellationSource.Token);
            }

            _allowExit = true;
            if (_trayIcon is not null)
            {
                _trayIcon.IsVisible = false;
            }

            _mainWindow.Close();
            _desktopLifetime?.Shutdown();
        }
        catch (Exception ex)
        {
            _allowExit = false;
            _exitInProgress = false;
            _logService.Error("Tray shutdown failed.", ex);
        }
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private static WindowIcon LoadTrayIcon()
    {
        using var iconStream = AssetLoader.Open(new Uri("avares://PZServerLauncher.App/Assets/avalonia-logo.ico"));
        return new WindowIcon(iconStream);
    }
}
