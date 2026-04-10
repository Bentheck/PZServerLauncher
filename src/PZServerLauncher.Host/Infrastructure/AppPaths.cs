namespace PZServerLauncher.Host.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? rootDirectory = null)
    {
        RootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PZServerLauncher")
            : rootDirectory;
        DataDirectory = Path.Combine(RootDirectory, "data");
        StateDirectory = Path.Combine(RootDirectory, "state");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        RuntimeDirectory = Path.Combine(RootDirectory, "runtime");
        ToolsDirectory = Path.Combine(RootDirectory, "tools");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(ToolsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string StateDirectory { get; }

    public string LogsDirectory { get; }

    public string RuntimeDirectory { get; }

    public string ToolsDirectory { get; }

    public string BackupsDirectory { get; }

    public string DatabasePath => Path.Combine(DataDirectory, "app.db");

    public string DatabaseBackupPath => Path.Combine(DataDirectory, "app.db.bak");

    public string MigrationLockPath => Path.Combine(StateDirectory, "migration.lock");

    public string HostStatePath => Path.Combine(StateDirectory, "host-state.json");

    public string RuntimeProfileDirectory(string profileId) =>
        Path.Combine(RuntimeDirectory, "profiles", profileId);
}
