namespace PZServerLauncher.Contracts.Runtime;

public sealed record RestoreBackupRequestDto(
    string BackupFileName,
    bool RestartAfterRestore);
