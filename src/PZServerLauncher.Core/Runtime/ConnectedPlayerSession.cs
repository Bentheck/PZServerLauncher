namespace PZServerLauncher.Core.Runtime;

public sealed record ConnectedPlayerSession(
    string UserName,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset LastSeenAtUtc);
