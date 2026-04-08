namespace PZServerLauncher.Host.Data.Entities;

public sealed class AuditEntryEntity
{
    public Guid EntryId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public required string Action { get; set; }

    public required string Subject { get; set; }

    public required string ActorType { get; set; }

    public string? ActorId { get; set; }

    public required string Detail { get; set; }
}
