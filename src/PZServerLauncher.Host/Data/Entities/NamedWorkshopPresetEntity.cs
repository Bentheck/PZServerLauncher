namespace PZServerLauncher.Host.Data.Entities;

public sealed class NamedWorkshopPresetEntity
{
    public Guid PresetId { get; set; }

    public string ProfileId { get; set; } = string.Empty;

    public int Branch { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string WorkshopItemIdsJson { get; set; } = "[]";

    public string EnabledModIdsJson { get; set; } = "[]";

    public string MapFoldersJson { get; set; } = "[]";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
