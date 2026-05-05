namespace PZServerLauncher.Host.Data.Entities;

public sealed class ModsMapsDraftMapRowEntity
{
    public required string ProfileId { get; set; }

    public int RowId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string MapFolder { get; set; } = string.Empty;

    public string WorkshopId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }
}
