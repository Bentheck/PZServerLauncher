namespace PZServerLauncher.Contracts.Runtime;

public sealed record WorkspaceBootstrapDto(
    WorkspaceActorDto Actor,
    ResolvedCapabilitiesDto Capabilities,
    IReadOnlyList<WorkspacePageDto> GlobalPages,
    IReadOnlyList<WorkspacePageDto> ProfilePages);
