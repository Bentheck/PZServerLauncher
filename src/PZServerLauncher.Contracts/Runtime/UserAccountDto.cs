using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record UserAccountDto(
    string UserId,
    string UserName,
    IReadOnlyList<UserRole> Roles,
    bool TwoFactorEnabled);
