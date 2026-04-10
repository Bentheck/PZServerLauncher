using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsDraftDto(
    string ProfileId,
    ProjectZomboidBranch Branch,
    string CatalogId,
    int CatalogVersion,
    string PageId,
    IReadOnlyDictionary<string, string?> Values,
    string? SourceSha256,
    bool IsDirty,
    DateTimeOffset UpdatedAtUtc);
