namespace PZServerLauncher.Core.Settings;

public sealed record BuiltInSettingsPresetDefinition(
    string PresetId,
    string Label,
    IReadOnlyDictionary<string, string?> Values);
