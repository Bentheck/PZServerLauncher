namespace PZServerLauncher.Core.Runtime;

public sealed record AuditEntry(
    Guid EntryId,
    DateTimeOffset OccurredAtUtc,
    string Action,
    string Subject,
    string ActorType,
    string? ActorId,
    string Detail);
