namespace PZServerLauncher.Contracts.Profiles;

public sealed record SandboxPresetDto(
    string PresetId,
    string Label,
    bool IsBuiltIn,
    IReadOnlyDictionary<string, string?> Values);
