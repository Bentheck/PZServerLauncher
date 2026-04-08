namespace PZServerLauncher.Core.Planning;

public sealed record ServerPaths(
    string CacheRootDirectory,
    string ServerConfigDirectory,
    string SavesDirectory,
    string WorldDirectory,
    string IniFilePath,
    string SandboxVarsFilePath,
    string SpawnRegionsFilePath,
    string SpawnPointsFilePath);
