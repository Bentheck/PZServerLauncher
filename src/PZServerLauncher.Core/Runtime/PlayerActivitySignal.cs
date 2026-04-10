namespace PZServerLauncher.Core.Runtime;

public sealed record PlayerActivitySignal(
    string UserName,
    string Activity,
    DateTimeOffset TimestampUtc,
    string SourceLine);
