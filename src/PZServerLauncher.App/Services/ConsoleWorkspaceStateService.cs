using System.Text.Json;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.Services;

public sealed class ConsoleWorkspaceStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _stateFilePath;

    public ConsoleWorkspaceStateService()
    {
        var rootDirectory = LauncherStorageRootResolver.Resolve();
        var stateDirectory = Path.Combine(rootDirectory, "state");
        _stateFilePath = Path.Combine(stateDirectory, "desktop-consoles.json");
    }

    public ConsoleWorkspaceState? Load()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<ConsoleWorkspaceState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(ConsoleWorkspaceState state)
    {
        var directory = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_stateFilePath, json);
    }
}

public sealed record ConsoleWorkspaceState(
    int SelectedSlotNumber,
    IReadOnlyList<ConsoleSlotState> Slots)
{
    public string? GetProfileId(int slotNumber) =>
        Slots.FirstOrDefault(slot => slot.SlotNumber == slotNumber)?.ProfileId;
}

public sealed record ConsoleSlotState(int SlotNumber, string? ProfileId);
