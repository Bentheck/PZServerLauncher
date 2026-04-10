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
        return new RawConfigFileDto(kind, content, ComputeSha256(content), BuildDiagnostics(kind, content));
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
        return new RawConfigFileDto(kind, content, ComputeSha256(content), BuildDiagnostics(kind, content));
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

    private static IReadOnlyList<string> BuildDiagnostics(ConfigFileKind kind, string content)
    {
        var diagnostics = new List<string>();
        switch (kind)
        {
            case ConfigFileKind.Ini:
                ValidateIni(content, diagnostics);
                break;
            case ConfigFileKind.SandboxVars:
                if (!content.Contains("SandboxVars", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add("SandboxVars.lua should define a SandboxVars table.");
                }

                ValidateLuaLike(kind, content, diagnostics);
                break;
            case ConfigFileKind.SpawnRegions:
            case ConfigFileKind.SpawnPoints:
                ValidateLuaLike(kind, content, diagnostics);
                break;
            default:
                diagnostics.Add($"Unknown config kind '{kind}'.");
                break;
        }

        return diagnostics;
    }

    private static void ValidateIni(string content, ICollection<string> diagnostics)
    {
        var lines = content.ReplaceLineEndings("\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                diagnostics.Add($"Line {index + 1} is not a valid key=value entry.");
            }
        }
    }

    private static void ValidateLuaLike(ConfigFileKind kind, string content, ICollection<string> diagnostics)
    {
        var braceBalance = 0;
        var parenthesisBalance = 0;
        var lineNumber = 1;

        foreach (var character in content)
        {
            switch (character)
            {
                case '{':
                    braceBalance++;
                    break;
                case '}':
                    braceBalance--;
                    if (braceBalance < 0)
                    {
                        diagnostics.Add($"{kind} has an unmatched closing brace near line {lineNumber}.");
                        braceBalance = 0;
                    }

                    break;
                case '(':
                    parenthesisBalance++;
                    break;
                case ')':
                    parenthesisBalance--;
                    if (parenthesisBalance < 0)
                    {
                        diagnostics.Add($"{kind} has an unmatched closing parenthesis near line {lineNumber}.");
                        parenthesisBalance = 0;
                    }

                    break;
                case '\n':
                    lineNumber++;
                    break;
            }
        }

        if (braceBalance != 0)
        {
            diagnostics.Add($"{kind} has unbalanced curly braces.");
        }

        if (parenthesisBalance != 0)
        {
            diagnostics.Add($"{kind} has unbalanced parentheses.");
        }
    }
}
