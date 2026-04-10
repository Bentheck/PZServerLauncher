namespace PZServerLauncher.Core.Runtime;

public sealed record OperationJob(
    Guid JobId,
    OperationJobKind Kind,
    OperationJobStatus Status,
    string? ProfileId,
    string Summary,
    string? Detail,
    int ProgressPercent,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
