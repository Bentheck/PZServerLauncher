namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsBuiltInPresetDto(
    string PresetId,
    string Label,
    IReadOnlyDictionary<string, string?> Values);
