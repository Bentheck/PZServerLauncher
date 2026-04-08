namespace PZServerLauncher.Host.Data.Entities;

public sealed class SettingsDraftEntity
{
    public required string ProfileId { get; set; }

    public int Branch { get; set; }

    public required string CatalogId { get; set; }

    public int CatalogVersion { get; set; }

    public required string PageId { get; set; }

    public string ValuesJson { get; set; } = "{}";

    public string? SourceSha256 { get; set; }

    public bool IsDirty { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
