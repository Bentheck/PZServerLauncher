using System.Text.RegularExpressions;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed partial class WorkshopDownloadProgressTracker
{
    private static readonly string[] CompletionKeywords =
    [
        "complete",
        "completed",
        "finished",
        "done",
        "success",
        "succeeded",
        "installed",
        "ready"
    ];

    private readonly IReadOnlyList<string> _orderedWorkshopIds;
    private readonly Dictionary<string, int> _indexLookup;

    public WorkshopDownloadProgressTracker(IReadOnlyList<string> configuredWorkshopIds)
    {
        _orderedWorkshopIds = configuredWorkshopIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _indexLookup = _orderedWorkshopIds
            .Select((value, index) => new KeyValuePair<string, int>(value, index + 1))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    public WorkshopDownloadProgress? Current { get; private set; }

    public bool HasConfiguredItems => _orderedWorkshopIds.Count > 0;

    public WorkshopDownloadProgress? Observe(string line, DateTimeOffset updatedAtUtc)
    {
        if (!HasConfiguredItems || string.IsNullOrWhiteSpace(line))
        {
            return Current;
        }

        var matchedWorkshopId = FindConfiguredWorkshopId(line);
        if (matchedWorkshopId is null)
        {
            if (Current is { IsComplete: false } current &&
                current.CurrentItemIndex == current.TotalItemCount &&
                LooksLikeCompletion(line))
            {
                Current = current with
                {
                    IsComplete = true,
                    LastRawLine = line,
                    UpdatedAtUtc = updatedAtUtc,
                };
            }

            return Current;
        }

        var observedIndex = _indexLookup[matchedWorkshopId];
        var currentIndex = Current is null
            ? observedIndex
            : Math.Max(Current.CurrentItemIndex, observedIndex);
        var effectiveWorkshopId = Current is not null && observedIndex < Current.CurrentItemIndex
            ? Current.CurrentWorkshopId
            : matchedWorkshopId;
        var isComplete = currentIndex == _orderedWorkshopIds.Count && LooksLikeCompletion(line);

        Current = new WorkshopDownloadProgress(
            currentIndex,
            _orderedWorkshopIds.Count,
            effectiveWorkshopId,
            line,
            isComplete,
            updatedAtUtc);

        return Current;
    }

    [GeneratedRegex(@"(?<!\d)(?<value>\d{8,12})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex NumericTokenRegex();

    private string? FindConfiguredWorkshopId(string line)
    {
        foreach (Match match in NumericTokenRegex().Matches(line))
        {
            var value = match.Groups["value"].Value;
            if (_indexLookup.ContainsKey(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool LooksLikeCompletion(string line) =>
        CompletionKeywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
