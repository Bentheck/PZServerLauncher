namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsFieldOptionDto(
    string Value,
    string Label,
    string? Description);
