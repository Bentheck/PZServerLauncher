using System.Security.Cryptography;
using System.Text;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ConfigFileService(ProjectZomboidServerPlanner planner)
{
    public CommonConfigDto GetCommonConfig(PZServerLauncher.Core.Profiles.ServerProfile profile) =>
        new(
            profile.ServerName,
            profile.DefaultPort,
            profile.UdpPort,
            profile.RconPort,
            profile.BindIp,
            profile.AdminUsername,
            profile.PreferredMemoryInGigabytes,
            profile.StartWithHost,
            profile.AutoRestartOnCrash);

    public PZServerLauncher.Core.Profiles.ServerProfile ApplyCommonConfig(
        PZServerLauncher.Core.Profiles.ServerProfile profile,
        CommonConfigDto common) =>
        profile with
        {
            ServerName = common.ServerName,
            DefaultPort = common.DefaultPort,
            UdpPort = common.UdpPort,
            RconPort = common.RconPort,
            BindIp = common.BindIp,
            AdminUsername = common.AdminUsername,
            PreferredMemoryInGigabytes = common.PreferredMemoryInGigabytes,
            StartWithHost = common.StartWithHost,
            AutoRestartOnCrash = common.AutoRestartOnCrash,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

    public RawConfigFileDto ReadRawFile(PZServerLauncher.Core.Profiles.ServerProfile profile, ConfigFileKind kind)
    {
        var path = ResolveFilePath(profile, kind);
        var content = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        return new RawConfigFileDto(kind, content, ComputeSha256(content), []);
    }

    public RawConfigFileDto WriteRawFile(
        PZServerLauncher.Core.Profiles.ServerProfile profile,
        ConfigFileKind kind,
        string expectedSha256,
        string content)
    {
        var path = ResolveFilePath(profile, kind);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existingContent = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        if (!string.Equals(expectedSha256, ComputeSha256(existingContent), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The config file changed since it was last read.");
        }

        File.WriteAllText(path, content);
        return new RawConfigFileDto(kind, content, ComputeSha256(content), []);
    }

    private string ResolveFilePath(PZServerLauncher.Core.Profiles.ServerProfile profile, ConfigFileKind kind)
    {
        var paths = planner.ResolvePaths(profile);
        return kind switch
        {
            ConfigFileKind.Ini => paths.IniFilePath,
            ConfigFileKind.SandboxVars => paths.SandboxVarsFilePath,
            ConfigFileKind.SpawnRegions => paths.SpawnRegionsFilePath,
            ConfigFileKind.SpawnPoints => paths.SpawnPointsFilePath,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported config file kind."),
        };
    }

    private static string ComputeSha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
