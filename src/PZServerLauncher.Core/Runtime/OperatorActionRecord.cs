namespace PZServerLauncher.Core.Runtime;

public sealed record OperatorActionRecord(
    string Kind,
    string CommandText,
    string Summary,
    DateTimeOffset TimestampUtc);
