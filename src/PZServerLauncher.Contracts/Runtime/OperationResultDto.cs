namespace PZServerLauncher.Contracts.Runtime;

public sealed record OperationResultDto(
    bool Success,
    string Message,
    Guid? JobId = null);
