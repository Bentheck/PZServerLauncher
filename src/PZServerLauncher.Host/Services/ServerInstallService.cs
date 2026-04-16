using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Planning;
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

        var installValidation = ValidateInstalledLauncher(profile);
        var status = result.ExitCode == 0 && installValidation.IsValid ? OperationJobStatus.Succeeded : OperationJobStatus.Failed;
        var detail = result.ExitCode == 0
            ? installValidation.Detail ?? BuildJobDetail(result)
            : BuildJobDetail(result);

        if (!installValidation.IsValid && installValidation.Detail is { Length: > 0 } validationDetail)
        {
            runtimeStateStore.AppendLog(profileId, validationDetail);
        }

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

    private static InstallValidationResult ValidateInstalledLauncher(Core.Profiles.ServerProfile profile)
    {
        var launcherFileName = profile.UseSteam
            ? ProjectZomboidDefaults.StableBatchFileName
            : ProjectZomboidDefaults.NonSteamBatchFileName;
        var expectedPath = Path.Combine(profile.InstallDirectory, launcherFileName);
        if (File.Exists(expectedPath))
        {
            return InstallValidationResult.Valid();
        }

        var nestedCandidates = new[]
        {
            Path.Combine(profile.InstallDirectory, "Project Zomboid Dedicated Server", launcherFileName),
            Path.Combine(profile.InstallDirectory, "steamapps", "common", "Project Zomboid Dedicated Server", launcherFileName),
        };

        var misplacedPath = nestedCandidates.FirstOrDefault(File.Exists);
        if (misplacedPath is not null)
        {
            return InstallValidationResult.Invalid(
                $"Install/update completed, but {launcherFileName} was found at '{misplacedPath}' instead of the configured install root '{profile.InstallDirectory}'. Set this profile Install Directory to the real server folder and run update again.");
        }

        return InstallValidationResult.Invalid(
            $"Install/update completed, but {launcherFileName} is still missing from '{profile.InstallDirectory}'. Verify the Install Directory and rerun install/update.");
    }

    private sealed record InstallValidationResult(bool IsValid, string? Detail)
    {
        public static InstallValidationResult Valid() => new(true, null);

        public static InstallValidationResult Invalid(string detail) => new(false, detail);
    }
}
