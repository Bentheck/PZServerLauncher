using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PZServerLauncher.App.Services;

public sealed class FolderPickerService
{
    private Window? _mainWindow;

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        if (_mainWindow?.StorageProvider is null || !_mainWindow.StorageProvider.CanPickFolder)
        {
            return null;
        }

        var folders = await _mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
