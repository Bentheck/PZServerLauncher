using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record UpdateUserRequestDto(
    string UserName,
    string Email,
    IReadOnlyList<UserRole> Roles);
