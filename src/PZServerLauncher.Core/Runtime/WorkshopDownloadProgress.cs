namespace PZServerLauncher.Core.Runtime;

public sealed record WorkshopDownloadProgress(
    int CurrentItemIndex,
    int TotalItemCount,
    string? CurrentWorkshopId,
    string LastRawLine,
    bool IsComplete,
    DateTimeOffset UpdatedAtUtc)
{
    public string StatusLabel => IsComplete
        ? $"Workshop download complete ({TotalItemCount}/{TotalItemCount})"
        : $"Downloading workshop item {CurrentItemIndex}/{TotalItemCount}";

    public string DetailLabel => string.IsNullOrWhiteSpace(CurrentWorkshopId)
        ? StatusLabel
        : $"{StatusLabel} | Workshop ID {CurrentWorkshopId}";
}
