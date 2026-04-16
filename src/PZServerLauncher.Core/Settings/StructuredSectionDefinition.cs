namespace PZServerLauncher.Core.Settings;

public sealed record StructuredSectionDefinition(
    string SectionId,
    string DisplayName,
    IReadOnlyList<StructuredFieldDefinition> Fields,
    string? Description = null,
    string? CategoryId = null,
    string? CategoryTitle = null,
    int CategoryOrder = 0);
