namespace PZServerLauncher.Contracts.Runtime;

public sealed record ResetWorldRequestDto(
    bool CreateBackupBeforeReset,
    bool RestartAfterReset);
