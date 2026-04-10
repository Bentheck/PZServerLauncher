using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Infrastructure.Planning;

public sealed class ProjectZomboidServerPlanner : IProjectZomboidServerPlanner
{
    private static readonly HashSet<string> ManagedServerArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "-cachedir",
        "-servername",
        "-port",
        "-udpport",
        "-adminusername",
        "-adminpassword",
        "-ip",
    };

    public SteamCmdScriptPlan CreateInstallScript(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var lines = new List<string>
        {
            "@ShutdownOnFailedCommand 1",
            "@NoPromptForPassword 1",
            $"force_install_dir {QuoteForSteamCmd(profile.InstallDirectory)}",
            "login anonymous",
            GetAppUpdateCommand(profile.Branch),
            "quit",
        };

        return new SteamCmdScriptPlan(profile.InstallDirectory, lines);
    }

    public ServerLaunchPlan CreateLaunchPlan(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var launcherFileName = profile.UseSteam
            ? ProjectZomboidDefaults.StableBatchFileName
            : ProjectZomboidDefaults.NonSteamBatchFileName;
        var batchLauncherPath = Path.Combine(profile.InstallDirectory, launcherFileName);
        var managedArguments = BuildManagedServerArguments(profile);

        if (TryCreateDirectJavaPlan(profile, batchLauncherPath, managedArguments, out var directJavaPlan))
        {
            return directJavaPlan;
        }

        return new ServerLaunchPlan(
            WorkingDirectory: profile.InstallDirectory,
            LauncherPath: batchLauncherPath,
            Arguments: managedArguments,
            Notes: $"Could not extract the Java launch template from {launcherFileName}. Falling back to the official batch launcher with vendor-managed memory settings.",
            Strategy: ServerLaunchStrategy.VendorBatchFallback);
    }

    public ServerPaths ResolvePaths(ServerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var serverDirectory = Path.Combine(profile.CacheDirectory, "Server");
        var savesDirectory = Path.Combine(profile.CacheDirectory, "Saves", "Multiplayer");
        var worldDirectory = Path.Combine(savesDirectory, profile.ServerName);

        return new ServerPaths(
            CacheRootDirectory: profile.CacheDirectory,
            ServerConfigDirectory: serverDirectory,
            SavesDirectory: savesDirectory,
            WorldDirectory: worldDirectory,
            IniFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}.ini"),
            SandboxVarsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_SandboxVars.lua"),
            SpawnRegionsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_spawnregions.lua"),
            SpawnPointsFilePath: Path.Combine(serverDirectory, $"{profile.ServerName}_spawnpoints.lua"));
    }

    public string FormatLaunchCommand(ServerLaunchPlan plan) =>
        CommandLineFormatter.Format(plan.LauncherPath, plan.Arguments);

    public string FormatSteamCmdScript(SteamCmdScriptPlan plan) =>
        string.Join(Environment.NewLine, plan.ScriptLines);

    private static string GetAppUpdateCommand(ProjectZomboidBranch branch) =>
        branch switch
        {
            ProjectZomboidBranch.Stable41 => $"app_update {ProjectZomboidDefaults.DedicatedServerAppId} validate",
            ProjectZomboidBranch.Unstable42 => $"app_update {ProjectZomboidDefaults.DedicatedServerAppId} -beta unstable validate",
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unsupported Project Zomboid branch."),
        };

    private static string QuoteForSteamCmd(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private static List<string> BuildManagedServerArguments(ServerProfile profile)
    {
        var arguments = new List<string>
        {
            $"-cachedir={profile.CacheDirectory}",
            "-servername",
            profile.ServerName,
            "-port",
            profile.DefaultPort.ToString(CultureInfo.InvariantCulture),
            "-udpport",
            profile.UdpPort.ToString(CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(profile.AdminUsername))
        {
            arguments.Add("-adminusername");
            arguments.Add(profile.AdminUsername);
        }

        if (!string.IsNullOrWhiteSpace(profile.AdminPassword))
        {
            arguments.Add("-adminpassword");
            arguments.Add(profile.AdminPassword);
        }

        if (!string.IsNullOrWhiteSpace(profile.BindIp))
        {
            arguments.Add("-ip");
            arguments.Add(profile.BindIp);
        }

        return arguments;
    }

    private static bool TryCreateDirectJavaPlan(
        ServerProfile profile,
        string batchLauncherPath,
        IReadOnlyList<string> managedArguments,
        out ServerLaunchPlan plan)
    {
        plan = null!;

        if (!File.Exists(batchLauncherPath))
        {
            return false;
        }

        var batchContent = File.ReadAllText(batchLauncherPath);
        if (!TryExtractJavaTemplate(batchContent, profile.InstallDirectory, out var template))
        {
            return false;
        }

        var arguments = new List<string>(template.JvmArguments.Count + template.ServerArguments.Count + managedArguments.Count + 3)
        {
            $"-Xms{profile.PreferredMemoryInGigabytes}g",
            $"-Xmx{profile.PreferredMemoryInGigabytes}g",
        };

        arguments.AddRange(template.JvmArguments);
        arguments.Add(template.MainClass);
        arguments.AddRange(template.ServerArguments);
        arguments.AddRange(managedArguments);

        plan = new ServerLaunchPlan(
            WorkingDirectory: profile.InstallDirectory,
            LauncherPath: template.JavaExecutablePath,
            Arguments: arguments,
            Notes: $"Using the installed {Path.GetFileName(batchLauncherPath)} template with launcher-managed memory set to {profile.PreferredMemoryInGigabytes} GB.",
            Strategy: ServerLaunchStrategy.DirectJavaTemplate);

        return true;
    }

    private static bool TryExtractJavaTemplate(string batchContent, string installDirectory, out JavaLaunchTemplate template)
    {
        template = null!;

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CD"] = installDirectory,
        };

        string? javaCommand = null;
        foreach (var line in CollapseBatchCommands(batchContent))
        {
            if (line.Length == 0 || line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("::", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryParseSetLine(line, installDirectory, variables, out var variableName, out var variableValue))
            {
                variables[variableName] = variableValue;
                continue;
            }

            if (line.Contains("zombie.network.GameServer", StringComparison.OrdinalIgnoreCase))
            {
                javaCommand = line;
            }
        }

        if (string.IsNullOrWhiteSpace(javaCommand))
        {
            return false;
        }

        var normalizedCommand = NormalizeCommand(javaCommand, installDirectory, variables);
        var tokens = TokenizeWindowsCommand(normalizedCommand);
        if (tokens.Count < 3)
        {
            return false;
        }

        var executableToken = tokens[0];
        if (string.Equals(executableToken, "call", StringComparison.OrdinalIgnoreCase) && tokens.Count > 1)
        {
            executableToken = tokens[1];
            tokens.RemoveAt(0);
        }

        var mainClassIndex = tokens.FindIndex(token => string.Equals(token, "zombie.network.GameServer", StringComparison.Ordinal));
        if (mainClassIndex < 2)
        {
            return false;
        }

        var javaExecutablePath = ResolveJavaExecutablePath(executableToken, installDirectory);
        if (javaExecutablePath is null)
        {
            return false;
        }

        var jvmArguments = tokens
            .Skip(1)
            .Take(mainClassIndex - 1)
            .Where(token => !token.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) &&
                            !token.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var serverArguments = RemoveManagedArguments(tokens.Skip(mainClassIndex + 1).ToList());

        template = new JavaLaunchTemplate(
            javaExecutablePath,
            jvmArguments,
            tokens[mainClassIndex],
            serverArguments);

        return true;
    }

    private static IReadOnlyList<string> CollapseBatchCommands(string batchContent)
    {
        var lines = batchContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);
        var commands = new List<string>();
        var builder = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var continues = trimmed.EndsWith('^');
            if (continues)
            {
                trimmed = trimmed[..^1].TrimEnd();
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(trimmed);

            if (!continues)
            {
                commands.Add(builder.ToString().Trim());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            commands.Add(builder.ToString().Trim());
        }

        return commands;
    }

    private static bool TryParseSetLine(
        string line,
        string installDirectory,
        IReadOnlyDictionary<string, string> variables,
        out string variableName,
        out string variableValue)
    {
        variableName = string.Empty;
        variableValue = string.Empty;

        if (!line.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var assignment = line[4..].Trim();
        if (assignment.StartsWith('"') && assignment.EndsWith('"') && assignment.Length > 1)
        {
            assignment = assignment[1..^1];
        }

        var separatorIndex = assignment.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return false;
        }

        variableName = assignment[..separatorIndex].Trim();
        var rawValue = assignment[(separatorIndex + 1)..].Trim();
        variableValue = NormalizeCommand(rawValue, installDirectory, variables);
        return !string.IsNullOrWhiteSpace(variableName);
    }

    private static string NormalizeCommand(
        string input,
        string installDirectory,
        IReadOnlyDictionary<string, string> variables)
    {
        var normalized = Regex.Replace(input, @"\^\s*$", string.Empty, RegexOptions.Multiline);
        normalized = normalized.Replace("%~dp0", EnsureTrailingSlash(installDirectory), StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace("%CD%", installDirectory, StringComparison.OrdinalIgnoreCase);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var updated = Regex.Replace(
                normalized,
                "%(?<name>[A-Za-z0-9_]+)%",
                match =>
                {
                    var name = match.Groups["name"].Value;
                    if (variables.TryGetValue(name, out var value))
                    {
                        return value;
                    }

                    return Environment.GetEnvironmentVariable(name) ?? match.Value;
                });

            if (string.Equals(updated, normalized, StringComparison.Ordinal))
            {
                break;
            }

            normalized = updated;
        }

        return normalized.Trim();
    }

    private static string EnsureTrailingSlash(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static List<string> TokenizeWindowsCommand(string commandLine)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static string? ResolveJavaExecutablePath(string token, string installDirectory)
    {
        var candidate = token;
        if (string.Equals(candidate, "java", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate, "java.exe", StringComparison.OrdinalIgnoreCase))
        {
            return TryResolveBundledJavaFallback(installDirectory) ?? candidate;
        }

        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.Combine(installDirectory, candidate);
        }

        if (File.Exists(candidate))
        {
            return candidate;
        }

        return TryResolveBundledJavaFallback(installDirectory);
    }

    private static string? TryResolveBundledJavaFallback(string installDirectory)
    {
        var commonCandidates = new[]
        {
            Path.Combine(installDirectory, "jre64", "bin", "java.exe"),
            Path.Combine(installDirectory, "jre", "bin", "java.exe"),
            Path.Combine(installDirectory, "runtime", "bin", "java.exe"),
        };

        foreach (var candidate in commonCandidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var discovered = Directory.Exists(installDirectory)
            ? Directory.EnumerateFiles(installDirectory, "java.exe", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            : null;

        return discovered;
    }

    private static List<string> RemoveManagedArguments(IReadOnlyList<string> originalArguments)
    {
        var arguments = new List<string>();
        for (var index = 0; index < originalArguments.Count; index++)
        {
            var token = originalArguments[index];
            if (ManagedServerArguments.Any(argument => token.StartsWith($"{argument}=", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (ManagedServerArguments.Contains(token))
            {
                index++;
                continue;
            }

            arguments.Add(token);
        }

        return arguments;
    }

    private sealed record JavaLaunchTemplate(
        string JavaExecutablePath,
        IReadOnlyList<string> JvmArguments,
        string MainClass,
        IReadOnlyList<string> ServerArguments);
}
