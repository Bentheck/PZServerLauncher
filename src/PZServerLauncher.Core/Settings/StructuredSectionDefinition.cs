namespace PZServerLauncher.Core.Settings;

public sealed record StructuredSectionDefinition(
    string SectionId,
    string DisplayName,
    IReadOnlyList<StructuredFieldDefinition> Fields);
