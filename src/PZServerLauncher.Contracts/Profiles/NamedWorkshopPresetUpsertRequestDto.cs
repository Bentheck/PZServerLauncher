using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record NamedWorkshopPresetUpsertRequestDto(
    string Name,
    WorkshopPreset Preset);
