using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.Services;

public sealed record HostSnapshot(
    HostInfoDto HostInfo,
    IReadOnlyList<ProfileDto> Profiles,
    IReadOnlyDictionary<string, ServerRuntimeStatus> Statuses,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Backups,
    IReadOnlyList<OperationJob> Jobs);
