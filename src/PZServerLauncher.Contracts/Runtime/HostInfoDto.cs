using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record HostInfoDto(
    HostHealth Health,
    HostSettings Settings);
