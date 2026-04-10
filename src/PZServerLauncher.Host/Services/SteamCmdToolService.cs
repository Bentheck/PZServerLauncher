using System.Collections.Concurrent;
using System.IO.Compression;
using System.Diagnostics;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class SteamCmdToolService(AppPaths appPaths, IHttpClientFactory httpClientFactory)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string RootDirectory => Path.Combine(appPaths.ToolsDirectory, "steamcmd");

    public string ExecutablePath => Path.Combine(RootDirectory, "steamcmd.exe");

    public async Task EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ExecutablePath))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
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
            _gate.Release();
        }
    }

    public async Task<SteamCmdExecutionResult> RunScriptAsync(string scriptPath, Func<string, Task> onOutput, CancellationToken cancellationToken = default)
    {
        await EnsureInstalledAsync(cancellationToken);
        var outputLines = new ConcurrentQueue<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = ExecutablePath,
            Arguments = $"+runscript \"{scriptPath}\"",
            WorkingDirectory = RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = ReadLinesAsync(process.StandardOutput, outputLines, onOutput, cancellationToken);
        var stderrTask = ReadLinesAsync(process.StandardError, outputLines, onOutput, cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        return new SteamCmdExecutionResult(process.ExitCode, outputLines.ToArray());
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
