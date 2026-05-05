using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record ModsMapsDraftDto(
    string ProfileId,
    ProjectZomboidBranch Branch,
    IReadOnlyList<string> WorkshopItemIds,
    IReadOnlyList<ModsMapsModRowDto> ModRows,
    IReadOnlyList<ModsMapsMapRowDto> MapRows,
    ModsMapsEditorMode EditorMode,
    bool IsDirty,
    DateTimeOffset UpdatedAtUtc);
