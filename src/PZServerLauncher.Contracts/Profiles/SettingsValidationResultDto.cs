namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsValidationResultDto(
    string PageId,
    bool IsValid,
    IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors,
    IReadOnlyList<string> PageErrors,
    bool RequiresAdvancedFilesFallback,
    string? FallbackReason);
