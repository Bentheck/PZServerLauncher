using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record UpdateUserRequestDto(
    string UserName,
    IReadOnlyList<UserRole> Roles);
