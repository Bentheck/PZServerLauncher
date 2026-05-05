namespace PZServerLauncher.Host.Data.Entities;

public sealed class ModsMapsDraftModRowEntity
{
    public required string ProfileId { get; set; }

    public int RowId { get; set; }

    public string ModName { get; set; } = string.Empty;

    public string ModId { get; set; } = string.Empty;

    public string WorkshopId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public string DependencyModIdsJson { get; set; } = "[]";

    public string MapFoldersJson { get; set; } = "[]";
}
