namespace PZServerLauncher.Core.Settings;

public sealed record StructuredPageDefinition(
    string PageId,
    string DisplayName,
    IReadOnlyList<StructuredSectionDefinition> Sections);
