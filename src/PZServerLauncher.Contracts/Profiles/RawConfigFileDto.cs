using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Contracts.Profiles;

public sealed record RawConfigFileDto(
    ConfigFileKind Kind,
    string Content,
    string Sha256,
    IReadOnlyList<string> Diagnostics);
