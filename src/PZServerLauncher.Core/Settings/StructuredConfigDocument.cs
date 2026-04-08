namespace PZServerLauncher.Core.Settings;

public sealed record StructuredConfigDocument(
    string SourceText,
    bool IsSupported,
    IReadOnlyList<StructuredConfigIssue> Issues);
