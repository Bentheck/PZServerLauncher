namespace PZServerLauncher.Core.Runtime;

public enum OperationJobKind
{
    Install,
    Update,
    Start,
    Stop,
    Restart,
    Backup,
    Restore,
    WriteConfig,
    BootstrapOwner,
    EnableRemoteAccess,
}
