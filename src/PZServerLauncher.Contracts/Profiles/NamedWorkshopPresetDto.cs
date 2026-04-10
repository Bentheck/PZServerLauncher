using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record NamedWorkshopPresetDto(
    Guid PresetId,
    string ProfileId,
    string Name,
    ProjectZomboidBranch Branch,
    WorkshopPreset Preset,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
