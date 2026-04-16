using Avalonia.Controls;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Views;

namespace PZServerLauncher.App.Services;

public sealed class ShutdownWarningDialogService
{
    private Window? _mainWindow;

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public async Task<ShutdownWarningChoice> ShowAsync()
    {
        if (_mainWindow is null)
        {
            return ShutdownWarningChoice.KeepRunning;
        }

        var dialog = new ShutdownWarningDialog
        {
            DataContext = new ShutdownWarningDialogViewModel(),
        };

        return await dialog.ShowDialog<ShutdownWarningChoice>(_mainWindow);
    }
}
