using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record WorkspaceActorDto(
    string DisplayName,
    string? Email,
    WorkspaceSurfaceKind Surface,
    IReadOnlyList<UserRole> Roles);
