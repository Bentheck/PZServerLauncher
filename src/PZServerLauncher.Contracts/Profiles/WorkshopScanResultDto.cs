using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record WorkshopScanResultDto(
    WorkshopPreset Preset,
    IReadOnlyList<string> Diagnostics);
