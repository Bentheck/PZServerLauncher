using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ProfileImportCandidateDto(
    string CandidateId,
    string DisplayName,
    string ServerName,
    string CacheDirectory,
    string? InstallDirectory,
    ProjectZomboidBranch Branch,
    WorkshopPreset WorkshopPreset,
    IReadOnlyList<string> Diagnostics,
    bool IsAlreadyImported);
