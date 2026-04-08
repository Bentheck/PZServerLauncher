namespace PZServerLauncher.Core.Settings;

public sealed record StructuredConfigIssue(
    string Message,
    int? LineNumber = null,
    string? FieldId = null);
