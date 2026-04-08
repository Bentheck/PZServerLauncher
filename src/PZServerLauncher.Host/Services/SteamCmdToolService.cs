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

    public async Task<int> RunScriptAsync(string scriptPath, Func<string, Task> onOutput, CancellationToken cancellationToken = default)
    {
        await EnsureInstalledAsync(cancellationToken);

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

        var stdoutTask = ReadLinesAsync(process.StandardOutput, onOutput, cancellationToken);
        var stderrTask = ReadLinesAsync(process.StandardError, onOutput, cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync(cancellationToken));
        return process.ExitCode;
    }

    private static async Task ReadLinesAsync(StreamReader reader, Func<string, Task> onOutput, CancellationToken cancellationToken)
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
                await onOutput(line);
            }
        }
    }
}
