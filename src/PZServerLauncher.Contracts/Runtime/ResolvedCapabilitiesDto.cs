namespace PZServerLauncher.Contracts.Runtime;

public sealed record ResolvedCapabilitiesDto(
    IReadOnlyList<Capability> AllowedCapabilities);
