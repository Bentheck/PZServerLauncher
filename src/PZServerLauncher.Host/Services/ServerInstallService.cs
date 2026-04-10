using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ServerInstallService(
    AppPaths appPaths,
    ProjectZomboidServerPlanner planner,
    SteamCmdToolService steamCmdToolService,
    ProfileStore profileStore,
    AuditStore auditStore)
{
    private const int MaxMissingConfigurationRetries = 2;

    public async Task ExecuteInstallAsync(
        string profileId,
        OperationJob job,
        JobStore jobStore,
        RuntimeStateStore runtimeStateStore,
        CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        Directory.CreateDirectory(Path.Combine(appPaths.ToolsDirectory, "steamcmd", "scripts"));
        var scriptPlan = planner.CreateInstallScript(profile);
        var scriptPath = Path.Combine(appPaths.ToolsDirectory, "steamcmd", "scripts", $"{profileId}-{job.Kind.ToString().ToLowerInvariant()}.txt");
        await File.WriteAllTextAsync(scriptPath, planner.FormatSteamCmdScript(scriptPlan), cancellationToken);

        await jobStore.UpdateAsync(job with
        {
            Status = OperationJobStatus.Running,
            ProgressPercent = 10,
            StartedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

        var result = await RunSteamCmdWithRetryAsync(
            profileId,
            scriptPath,
            runtimeStateStore,
            cancellationToken);

        var status = result.ExitCode == 0 ? OperationJobStatus.Succeeded : OperationJobStatus.Failed;
        var detail = BuildJobDetail(result);
        await jobStore.UpdateAsync(job with
        {
            Status = status,
            ProgressPercent = status == OperationJobStatus.Succeeded ? 100 : 100,
            Detail = detail,
            StartedAtUtc = job.StartedAtUtc ?? DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

        await auditStore.WriteAsync(
            status == OperationJobStatus.Succeeded ? "install.completed" : "install.failed",
            profileId,
            "local",
            status == OperationJobStatus.Succeeded
                ? "SteamCMD install/update completed."
                : detail,
            cancellationToken: cancellationToken);
    }

    private async Task<SteamCmdExecutionResult> RunSteamCmdWithRetryAsync(
        string profileId,
        string scriptPath,
        RuntimeStateStore runtimeStateStore,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (attempt > 1)
            {
                runtimeStateStore.AppendLog(profileId, $"Retrying SteamCMD after transient missing-configuration failure (attempt {attempt} of {MaxMissingConfigurationRetries + 1}).");
            }

            var result = await steamCmdToolService.RunScriptAsync(scriptPath, async line =>
            {
                runtimeStateStore.AppendLog(profileId, line);
                await Task.CompletedTask;
            }, cancellationToken);

            if (!result.HasMissingConfigurationFailure || attempt > MaxMissingConfigurationRetries)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private static string BuildJobDetail(SteamCmdExecutionResult result)
    {
        if (result.ExitCode == 0)
        {
            return "SteamCMD finished successfully.";
        }

        if (result.HasMissingConfigurationFailure)
        {
            return "SteamCMD failed after retrying a transient 'Missing configuration' response from Steam. Try the install again in a moment.";
        }

        return result.LastRelevantLine is { Length: > 0 } line
            ? $"SteamCMD exited with code {result.ExitCode}: {line}"
            : $"SteamCMD exited with code {result.ExitCode}.";
    }
}
