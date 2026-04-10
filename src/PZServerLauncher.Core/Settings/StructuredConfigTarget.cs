using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Core.Settings;

public sealed record StructuredConfigTarget(
    ConfigFileKind FileKind,
    string KeyPath,
    string? Section = null);
