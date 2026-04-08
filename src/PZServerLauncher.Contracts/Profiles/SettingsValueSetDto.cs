namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsValueSetDto(
    string CatalogId,
    int CatalogVersion,
    string PageId,
    IReadOnlyDictionary<string, string?> Values,
    string? SourceSha256,
    bool RequiresAdvancedFilesFallback,
    string? FallbackReason);
