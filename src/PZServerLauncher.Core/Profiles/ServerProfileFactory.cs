namespace PZServerLauncher.Core.Profiles;

public static class ServerProfileFactory
{
    public static ServerProfile CreateStarterProfile() =>
        new()
        {
            ProfileId = "main-server",
            DisplayName = "Main Server",
            ServerName = "mainserver",
            InstallDirectory = @"D:\PZServers\Build42-Unstable",
            CacheDirectory = @"D:\PZServers\Profiles\main-server",
            Branch = ProjectZomboidBranch.Unstable42,
            DefaultPort = 16261,
            UdpPort = 16262,
            RconPort = 27015,
            UseSteam = true,
            AdminUsername = "admin",
            AdminPassword = "change-me-before-first-run",
            PreferredMemoryInGigabytes = 6,
            StartWithHost = false,
            AutoRestartOnCrash = true,
            WorkshopPreset = WorkshopPreset.Empty,
            BackupPolicy = BackupPolicy.Default,
        };
}
