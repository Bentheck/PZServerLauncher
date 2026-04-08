namespace PZServerLauncher.Contracts.Runtime;

public sealed record WorkspacePageDto(
    string Id,
    string Title,
    string Route,
    WorkspacePageScope Scope,
    bool RequiresProfileSelection,
    IReadOnlyList<Capability> RequiredCapabilities,
    bool IsEnabled);
