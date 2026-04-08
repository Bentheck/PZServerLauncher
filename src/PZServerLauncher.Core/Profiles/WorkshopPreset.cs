namespace PZServerLauncher.Core.Profiles;

public sealed record WorkshopPreset
{
    public static WorkshopPreset Empty { get; } = new();

    public IReadOnlyList<string> WorkshopItemIds { get; init; } = [];

    public IReadOnlyList<string> EnabledModIds { get; init; } = [];

    public IReadOnlyList<string> MapFolders { get; init; } = [];
}
