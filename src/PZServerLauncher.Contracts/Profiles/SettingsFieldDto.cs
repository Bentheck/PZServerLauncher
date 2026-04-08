using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record SettingsFieldDto(
    string FieldId,
    string Label,
    string KeyPath,
    ConfigFileKind SourceFile,
    SettingsFieldControlKind Control,
    SettingsValueKind ValueType,
    string? DefaultValue,
    string? HelpText,
    bool RequiresRestart,
    bool IsReadOnly,
    IReadOnlyList<SettingsFieldOptionDto> Options);
