namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsSectionDto(
    string SectionId,
    string Title,
    string? Description,
    IReadOnlyList<SettingsFieldDto> Fields);
