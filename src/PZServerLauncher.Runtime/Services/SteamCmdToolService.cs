using System.Collections.Concurrent;
using System.IO.Compression;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Runtime.Infrastructure;

namespace PZServerLauncher.Runtime.Services;

public sealed class SteamCmdToolService(AppPaths appPaths, IHttpClientFactory httpClientFactory)
{
    private readonly SemaphoreSlim _installGate = new(1, 1);
    private readonly SemaphoreSlim _processGate = new(1, 1);
    private readonly SemaphoreSlim _catalogGate = new(1, 1);
    private IReadOnlyList<SteamBranchDto>? _cachedBranches;
    private DateTimeOffset _catalogRefreshedAtUtc;

    public string RootDirectory => Path.Combine(appPaths.ToolsDirectory, "steamcmd");

    public string ExecutablePath => Path.Combine(RootDirectory, "steamcmd.exe");

    public async Task EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ExecutablePath))
        {
            return;
        }

        await _installGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(ExecutablePath))
            {
                return;
            }

            Directory.CreateDirectory(RootDirectory);
            var archivePath = Path.Combine(RootDirectory, "steamcmd.zip");
            var client = httpClientFactory.CreateClient(nameof(SteamCmdToolService));
            await using (var response = await client.GetStreamAsync(PZServerLauncher.Core.Planning.ProjectZomboidDefaults.SteamCmdZipUrl, cancellationToken))
            await using (var file = File.Create(archivePath))
            {
                await response.CopyToAsync(file, cancellationToken);
            }

            ZipFile.ExtractToDirectory(archivePath, RootDirectory, overwriteFiles: true);
            File.Delete(archivePath);
        }
        finally
        {
            _installGate.Release();
        }
    }

    public async Task<SteamBranchCatalogDto> GetBranchCatalogAsync(
        string? installDirectory,
        string preferredBranch = "public",
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await _catalogGate.WaitAsync(cancellationToken);
        IReadOnlyList<SteamBranchDto> branches;
        try
        {
            if (!forceRefresh && _cachedBranches is { Count: > 0 })
            {
                branches = _cachedBranches;
            }
            else
            {
                await EnsureInstalledAsync(cancellationToken);
                var result = await RunProcessAsync(
                    ["+login", "anonymous", "+app_info_update", "1", "+app_info_print", PZServerLauncher.Core.Planning.ProjectZomboidDefaults.DedicatedServerAppId.ToString(System.Globalization.CultureInfo.InvariantCulture), "+quit"],
                    _ => Task.CompletedTask,
                    cancellationToken);

                branches = ParseBranchCatalog(result.OutputLines);
                if (result.ExitCode != 0 || branches.Count == 0)
                {
                    throw new InvalidOperationException(
                        result.LastRelevantLine is { Length: > 0 } detail
                            ? $"Steam branch scan failed: {detail}"
                            : "Steam branch scan returned no installable versions.");
                }

                _cachedBranches = branches;
                _catalogRefreshedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _catalogGate.Release();
        }

        var installedBranch = ReadInstalledBranch(installDirectory);
        var desiredBranch = installedBranch ?? preferredBranch;
        var selectedBranch = branches.Any(branch =>
            !branch.RequiresPassword &&
            string.Equals(branch.Name, desiredBranch, StringComparison.OrdinalIgnoreCase))
            ? desiredBranch
            : branches.FirstOrDefault(branch => branch.IsDefault && !branch.RequiresPassword)?.Name
                ?? branches.First(branch => !branch.RequiresPassword).Name;

        return new SteamBranchCatalogDto(branches, selectedBranch, _catalogRefreshedAtUtc);
    }

    public async Task<SteamCmdExecutionResult> RunScriptAsync(string scriptPath, Func<string, Task> onOutput, CancellationToken cancellationToken = default)
    {
        await EnsureInstalledAsync(cancellationToken);
        return await RunProcessAsync(["+runscript", scriptPath], onOutput, cancellationToken);
    }

    internal static IReadOnlyList<SteamBranchDto> ParseBranchCatalog(IEnumerable<string> outputLines)
    {
        var branches = new List<SteamBranchDto>();
        var waitingForBranchesBlock = false;
        var inBranchesBlock = false;
        var depth = 0;
        string? pendingBranchName = null;
        string? currentBranchName = null;
        Dictionary<string, string>? currentProperties = null;

        foreach (var rawLine in outputLines)
        {
            var line = rawLine.Trim();
            if (!inBranchesBlock)
            {
                if (string.Equals(line, "\"branches\"", StringComparison.Ordinal))
                {
                    waitingForBranchesBlock = true;
                    continue;
                }

                if (waitingForBranchesBlock && line == "{")
                {
                    inBranchesBlock = true;
                    depth = 1;
                }

                continue;
            }

            if (line == "{")
            {
                if (depth == 1 && pendingBranchName is not null)
                {
                    currentBranchName = pendingBranchName;
                    currentProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    pendingBranchName = null;
                }

                depth++;
                continue;
            }

            if (line == "}")
            {
                if (depth == 2 && currentBranchName is not null && currentProperties is not null)
                {
                    var description = currentProperties.GetValueOrDefault("description");
                    var displayName = string.Equals(currentBranchName, "public", StringComparison.OrdinalIgnoreCase)
                        ? "Latest stable (public)"
                        : string.IsNullOrWhiteSpace(description)
                            ? currentBranchName
                            : $"{description} ({currentBranchName})";
                    branches.Add(new SteamBranchDto(
                        currentBranchName,
                        displayName,
                        currentProperties.GetValueOrDefault("buildid") ?? "Unknown",
                        string.Equals(currentProperties.GetValueOrDefault("pwdrequired"), "1", StringComparison.Ordinal),
                        string.Equals(currentBranchName, "public", StringComparison.OrdinalIgnoreCase)));
                    currentBranchName = null;
                    currentProperties = null;
                }

                depth--;
                if (depth == 0)
                {
                    break;
                }

                continue;
            }

            var propertyMatch = Regex.Match(line, "^\"(?<key>[^\"]+)\"\\s+\"(?<value>[^\"]*)\"$");
            if (depth == 2 && currentProperties is not null && propertyMatch.Success)
            {
                currentProperties[propertyMatch.Groups["key"].Value] = propertyMatch.Groups["value"].Value;
                continue;
            }

            if (depth == 1 && TryReadQuotedKey(line, out var branchName))
            {
                pendingBranchName = branchName;
            }
        }

        return branches
            .OrderByDescending(branch => branch.IsDefault)
            .ThenBy(branch => branch.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<SteamCmdExecutionResult> RunProcessAsync(
        IReadOnlyList<string> arguments,
        Func<string, Task> onOutput,
        CancellationToken cancellationToken)
    {
        await _processGate.WaitAsync(cancellationToken);
        try
        {
            var outputLines = new ConcurrentQueue<string>();

            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                WorkingDirectory = RootDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = ReadLinesAsync(process.StandardOutput, outputLines, onOutput, cancellationToken);
            var stderrTask = ReadLinesAsync(process.StandardError, outputLines, onOutput, cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
            return new SteamCmdExecutionResult(process.ExitCode, outputLines.ToArray());
        }
        finally
        {
            _processGate.Release();
        }
    }

    private static bool TryReadQuotedKey(string line, out string key)
    {
        var match = Regex.Match(line, "^\"(?<key>[^\"]+)\"$");
        key = match.Success ? match.Groups["key"].Value : string.Empty;
        return match.Success;
    }

    private static string? ReadInstalledBranch(string? installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            return null;
        }

        var manifestPath = Path.Combine(installDirectory, "steamapps", $"appmanifest_{PZServerLauncher.Core.Planning.ProjectZomboidDefaults.DedicatedServerAppId}.acf");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var match = Regex.Match(
            File.ReadAllText(manifestPath),
            "\"BetaKey\"\\s+\"(?<branch>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups["branch"].Value)
            ? match.Groups["branch"].Value
            : "public";
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        ConcurrentQueue<string> outputLines,
        Func<string, Task> onOutput,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                outputLines.Enqueue(line);
                await onOutput(line);
            }
        }
    }
}

public sealed record SteamCmdExecutionResult(int ExitCode, IReadOnlyList<string> OutputLines)
{
    public bool HasMissingConfigurationFailure =>
        ExitCode == 7 &&
        OutputLines.Any(line => line.Contains("Missing configuration", StringComparison.OrdinalIgnoreCase));

    public string? LastRelevantLine =>
        OutputLines.LastOrDefault(line =>
            !string.IsNullOrWhiteSpace(line) &&
            !line.Contains("Loading Steam API", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(line, "OK", StringComparison.OrdinalIgnoreCase));
}
