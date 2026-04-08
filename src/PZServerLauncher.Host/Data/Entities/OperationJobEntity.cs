namespace PZServerLauncher.Host.Data.Entities;

public sealed class OperationJobEntity
{
    public Guid JobId { get; set; }

    public int Kind { get; set; }

    public int Status { get; set; }

    public string? ProfileId { get; set; }

    public required string Summary { get; set; }

    public string? Detail { get; set; }

    public int ProgressPercent { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
