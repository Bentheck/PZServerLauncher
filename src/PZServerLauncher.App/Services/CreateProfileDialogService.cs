using Avalonia.Controls;
using PZServerLauncher.App.ViewModels;
using PZServerLauncher.App.Views;

namespace PZServerLauncher.App.Services;

public sealed class CreateProfileDialogService
{
    private Window? _mainWindow;

    public void Initialize(Window mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public async Task<CreateProfileRequest?> ShowAsync(IEnumerable<CreateProfilePortReservation> existingProfiles)
    {
        if (_mainWindow is null)
        {
            return null;
        }

        var dialog = new CreateProfileDialog
        {
            DataContext = new CreateProfileDialogViewModel(existingProfiles),
        };

        return await dialog.ShowDialog<CreateProfileRequest?>(_mainWindow);
    }
}

public sealed record CreateProfileRequest(
    string DisplayName,
    int DefaultPort,
    int PreferredMemoryInGigabytes,
    int MaxPlayers);

public sealed record CreateProfilePortReservation(
    string ProfileId,
    string DisplayName,
    int DefaultPort,
    int UdpPort,
    int RconPort)
{
    public IReadOnlyList<int> ReservedPorts => [DefaultPort, UdpPort, RconPort];

    public string PortSummary => $"{DefaultPort} / {UdpPort} / {RconPort}";
}
