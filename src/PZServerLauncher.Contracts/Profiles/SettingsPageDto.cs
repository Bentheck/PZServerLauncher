namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsPageDto(
    string PageId,
    string Title,
    string? Description,
    bool SupportsStructuredEditing,
    bool SupportsDrafts,
    IReadOnlyList<SettingsSectionDto> Sections,
    IReadOnlyList<SettingsBuiltInPresetDto>? BuiltInPresets = null);
