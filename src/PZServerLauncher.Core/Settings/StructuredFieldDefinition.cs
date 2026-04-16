namespace PZServerLauncher.Core.Settings;

public sealed record StructuredFieldDefinition(
    string FieldId,
    string DisplayName,
    StructuredValueKind ValueKind,
    StructuredConfigTarget Target,
    string? DefaultValue = null,
    bool RestartRequired = false,
    string? HelpText = null,
    IReadOnlyList<StructuredFieldOptionDefinition>? Options = null);
