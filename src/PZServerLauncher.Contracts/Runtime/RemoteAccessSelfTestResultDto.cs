namespace PZServerLauncher.Contracts.Runtime;

public sealed record RemoteAccessSelfTestResultDto(
    bool Success,
    string Summary,
    IReadOnlyList<string> Checks);
