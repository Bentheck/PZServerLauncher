namespace PZServerLauncher.Host.Data.Entities;

public sealed class ModsMapsDraftEntity
{
    public required string ProfileId { get; set; }

    public int Branch { get; set; }

    public string WorkshopItemIdsJson { get; set; } = "[]";

    public string EditorMode { get; set; } = "Browse";

    public bool IsDirty { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
