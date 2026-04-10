namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsSaveResultDto(
    SettingsValueSetDto ValueSet,
    SettingsValidationResultDto Validation,
    bool DraftUpdated);
