using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record UserAccountDto(
    string UserId,
    string UserName,
    string? Email,
    IReadOnlyList<UserRole> Roles,
    bool TwoFactorEnabled);
