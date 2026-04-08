using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public sealed record ConfigFileOptionViewModel(ConfigFileKind Kind, string Label)
{
    public static IReadOnlyList<ConfigFileOptionViewModel> All { get; } =
    [
        new(ConfigFileKind.Ini, "Server INI"),
        new(ConfigFileKind.SandboxVars, "SandboxVars.lua"),
        new(ConfigFileKind.SpawnRegions, "SpawnRegions.lua"),
        new(ConfigFileKind.SpawnPoints, "SpawnPoints.lua"),
    ];
}
