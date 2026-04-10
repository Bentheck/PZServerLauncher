namespace PZServerLauncher.Core.Runtime;

public sealed record OwnerBootstrapState(
    bool IsConfigured,
    string? OwnerUserId,
    string? OwnerUserName,
    DateTimeOffset? ConfiguredAtUtc);
