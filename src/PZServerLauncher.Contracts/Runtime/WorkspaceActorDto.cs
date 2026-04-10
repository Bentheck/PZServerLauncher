using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Runtime;

public sealed record WorkspaceActorDto(
    string DisplayName,
    WorkspaceSurfaceKind Surface,
    IReadOnlyList<UserRole> Roles);
