using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record CreateUserRequestDto(
    string UserName,
    string Password,
    IReadOnlyList<UserRole> Roles);
