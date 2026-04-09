using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public sealed partial class InstallUpdateWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly FolderPickerService _folderPickerService;

    public InstallUpdateWorkspaceViewModel(
        MainWindowViewModel legacy,
        LocalHostApiClient hostApiClient,
        FolderPickerService folderPickerService)
        : base(
            "install-update",
            "Install & Update",
            "Install state, branch, paths, and lifecycle actions for the selected profile.",
            "Install & Update has no unsaved draft.",
            legacy,
            ["Install path", "Update actions", "Runtime controls", "Recent jobs"])
    {
        _hostApiClient = hostApiClient;
        _folderPickerService = folderPickerService;
        Legacy.RecentOperationJobs.CollectionChanged += OnRecentOperationJobsChanged;
        InstallCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.InstallCommand));
        UpdateCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.UpdateCommand));
        BackupCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.BackupCommand));
        StartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StartCommand));
        StopCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.StopCommand));
        RestartCommand = new AsyncRelayCommand(() => ExecuteProfileCommandAsync(Legacy.RestartCommand));
        RefreshCommand = new AsyncRelayCommand(() => Legacy.RefreshCommand.ExecuteAsync(null));
        SavePathOverridesCommand = new AsyncRelayCommand(SavePathOverridesAsync);
        ResetPathOverridesCommand = new RelayCommand(ResetPathOverrides);
        BrowseInstallDirectoryCommand = new AsyncRelayCommand(BrowseInstallDirectoryAsync);
        BrowseCacheDirectoryCommand = new AsyncRelayCommand(BrowseCacheDirectoryAsync);
    }

    public IAsyncRelayCommand InstallCommand { get; }

    public IAsyncRelayCommand UpdateCommand { get; }

    public IAsyncRelayCommand BackupCommand { get; }

    public IAsyncRelayCommand StartCommand { get; }

    public IAsyncRelayCommand StopCommand { get; }

    public IAsyncRelayCommand RestartCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand SavePathOverridesCommand { get; }

    public IRelayCommand ResetPathOverridesCommand { get; }

    public IAsyncRelayCommand BrowseInstallDirectoryCommand { get; }

    public IAsyncRelayCommand BrowseCacheDirectoryCommand { get; }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to install, update, and control its runtime."
        : $"Install and lifecycle controls for {SelectedProfile.DisplayName}, with preflight context for branch, paths, and safety.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "No profile selected";

    public string RuntimeState => SelectedProfile?.RuntimeState ?? "Unknown";

    public string InstallDirectory => SelectedProfile?.InstallDirectory ?? "No install path available";

    public string CacheDirectory => SelectedProfile?.CacheDirectory ?? "No cache path available";

    public bool HasPathOverridesDirty => SelectedProfile is not null &&
        (!string.Equals(EditableInstallDirectory.Trim(), SelectedProfile.InstallDirectory, StringComparison.Ordinal) ||
         !string.Equals(EditableCacheDirectory.Trim(), SelectedProfile.CacheDirectory, StringComparison.Ordinal));

    public string PathOverrideSummary => SelectedProfile is null
        ? "Select a profile before editing install and cache locations."
        : HasPathOverridesDirty
            ? "Path changes are pending. Save them before installing or updating this profile."
            : "Choose where the dedicated server binaries live and where this profile stores its Zomboid cache, saves, and config files.";

    public IReadOnlyList<OperationJob> RecentProfileJobs => SelectedProfile is null
        ? []
        : Legacy.RecentOperationJobs
            .Where(job =>
                string.Equals(job.ProfileId, SelectedProfile.ProfileId, StringComparison.OrdinalIgnoreCase) &&
                (job.Kind is OperationJobKind.Install or OperationJobKind.Update))
            .Take(5)
            .ToArray();

    public bool HasRecentProfileJobs => RecentProfileJobs.Count > 0;

    public string LastJobSummary => !HasRecentProfileJobs
        ? "No recent install or update jobs recorded for this profile."
        : $"{RecentProfileJobs[0].Kind} - {RecentProfileJobs[0].Status} | {RecentProfileJobs[0].Detail ?? RecentProfileJobs[0].Summary}";

    public string JobHistorySummary => SelectedProfile is null
        ? "Select a profile to see install and update history."
        : HasRecentProfileJobs
            ? $"{RecentProfileJobs.Count} recent install/update job(s) are attached to this profile."
            : "This profile does not have install or update history yet.";

    public string BranchChannelSummary => SelectedProfile?.BranchChannelSummary ?? "No branch channel summary available.";

    public string BranchInstallStatus => SelectedProfile is null
        ? "No profile selected."
        : SelectedProfile.IsInstallDetected
            ? $"Install detected for {SelectedProfile.Branch}."
            : $"No install detected yet for {SelectedProfile.Branch}.";

    public string LaunchModeLabel => SelectedProfile is null
        ? "Unknown"
        : SelectedProfile.UsesDirectJavaTemplate
            ? "Direct Java"
            : SelectedProfile.LauncherDetected
                ? "Batch fallback"
                : "Launcher missing";

    public string LaunchModeSummary => SelectedProfile is null
        ? "No launch mode available."
        : SelectedProfile.UsesDirectJavaTemplate
            ? "The host can launch through extracted JVM arguments and keep memory management under launcher control."
            : SelectedProfile.LauncherDetected
                ? "The host has a launcher, but template extraction is not safe yet, so it will fall back to the vendor batch flow."
                : "No launcher entrypoint was detected in the install footprint yet.";

    public string ConfigStateHeadline => SelectedProfile is null
        ? "Unknown"
        : SelectedProfile.ConfigDirectoryDetected
            ? SelectedProfile.IniDetected && SelectedProfile.SandboxDetected
                ? "Config set detected"
                : "Config root incomplete"
            : "Config root missing";

    public string RecoveryStateHeadline => SelectedProfile is null
        ? "Unknown"
        : SelectedProfile.HasBackup
            ? "Recovery point ready"
            : "No recovery point";

    public string RuntimeActionHeadline => SelectedProfile is null
        ? "No runtime window"
        : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
            ? "Live maintenance window"
            : "Idle deployment window";

    public string RuntimeActionSummary => SelectedProfile is null
        ? "Choose a profile to see runtime and maintenance guidance."
        : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
            ? $"{SelectedProfile.RuntimePolicySummary} {SelectedProfile.BackupSafetySummary}"
            : $"{SelectedProfile.LaunchReadinessSummary} {SelectedProfile.BackupSafetySummary}";

    public string InstallPostureSummary => SelectedProfile?.InstallPosture.DeploymentPostureSummary ?? "No install posture available.";

    public string UpdatePostureSummary => SelectedProfile?.InstallPosture.MaintenanceWindowSummary ?? "No update posture available.";

    public IReadOnlyList<OperationJob> RecentInstallJobs => SelectedProfile is null
        ? []
        : Legacy.RecentOperationJobs
            .Where(job =>
                string.Equals(job.ProfileId, SelectedProfile.ProfileId, StringComparison.OrdinalIgnoreCase) &&
                job.Kind == OperationJobKind.Install)
            .Take(5)
            .ToArray();

    public IReadOnlyList<OperationJob> RecentUpdateJobs => SelectedProfile is null
        ? []
        : Legacy.RecentOperationJobs
            .Where(job =>
                string.Equals(job.ProfileId, SelectedProfile.ProfileId, StringComparison.OrdinalIgnoreCase) &&
                job.Kind == OperationJobKind.Update)
            .Take(5)
            .ToArray();

    public string LastInstallJobSummary => !RecentInstallJobs.Any()
        ? "No recent install job recorded for this profile."
        : $"{RecentInstallJobs[0].Status} | {RecentInstallJobs[0].Detail ?? RecentInstallJobs[0].Summary}";

    public string LastUpdateJobSummary => !RecentUpdateJobs.Any()
        ? "No recent update job recorded for this profile."
        : $"{RecentUpdateJobs[0].Status} | {RecentUpdateJobs[0].Detail ?? RecentUpdateJobs[0].Summary}";

    public string InstallJobHistorySummary => SelectedProfile is null
        ? "Select a profile to see install history."
        : RecentInstallJobs.Any()
            ? $"{RecentInstallJobs.Count} recent install job(s) recorded."
            : "No install history recorded yet.";

    public string UpdateJobHistorySummary => SelectedProfile is null
        ? "Select a profile to see update history."
        : RecentUpdateJobs.Any()
            ? $"{RecentUpdateJobs.Count} recent update job(s) recorded."
            : "No update history recorded yet.";

    public string SteamCmdCommandSummary => SelectedProfile?.SteamCmdCommandSummary ?? "SteamCMD command summary unavailable.";

    public string SteamCmdScriptPreview => SelectedProfile?.SteamCmdScriptPreview ?? "SteamCMD script preview unavailable.";

    public string LaunchCommandPreview => SelectedProfile?.LaunchCommandPreview ?? "Launch command preview unavailable.";

    public string InstallReadiness => SelectedProfile?.InstallFootprintSummary ?? "No install profile selected.";

    public string CacheReadiness => SelectedProfile?.CacheFootprintSummary ?? "No cache profile selected.";

    public string BackupSafety => SelectedProfile?.BackupSafetySummary ?? "No backup context available.";

    public string LatestBackupLabel => SelectedProfile is null
        ? "No backup archive available."
        : SelectedProfile.HasBackup
            ? SelectedProfile.LastBackup
            : "No backup archive yet.";

    public string InstallSignal => SelectedProfile is null
        ? "No profile"
        : SelectedProfile.IsInstallDetected
            ? "Ready"
            : "Missing";

    public string InstallSignalSummary => InstallReadiness;

    public string CacheSignal => SelectedProfile is null
        ? "No profile"
        : SelectedProfile.CacheDetected
            ? "Ready"
            : "Missing";

    public string CacheSignalSummary => CacheReadiness;

    public string ConfigSignal => SelectedProfile is null
        ? "No profile"
        : !SelectedProfile.ConfigDirectoryDetected
            ? "Missing"
            : SelectedProfile.IniDetected && SelectedProfile.SandboxDetected
                ? "Ready"
                : "Partial";

    public string ConfigSignalSummary => ConfigFootprintSummary;

    public string BackupSignal => SelectedProfile is null
        ? "No profile"
        : SelectedProfile.HasBackup
            ? "Protected"
            : "Needs snapshot";

    public string BackupSignalSummary => BackupSafety;

    public string LaunchModeSignal => SelectedProfile is null
        ? "No profile"
        : !SelectedProfile.LauncherDetected
            ? "Blocked"
            : SelectedProfile.UsesDirectJavaTemplate
                ? "Direct Java"
                : "Batch fallback";

    public string LaunchModeSignalSummary => LaunchReadiness;

    public string MaintenanceSignal => SelectedProfile is null
        ? "No profile"
        : !SelectedProfile.IsInstallDetected
            ? "Install first"
            : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Live window"
                : "Idle window";

    public string MaintenanceSignalSummary => MaintenanceWindowSummary;

    public string DeploymentPosture => SelectedProfile?.InstallPosture.DeploymentPostureSummary ?? "No deployment posture available.";

    public string MaintenanceWindowSummary => SelectedProfile?.InstallPosture.MaintenanceWindowSummary ?? "No maintenance window summary available.";

    public string BranchIsolationSummary => SelectedProfile?.InstallPosture.BranchIsolationSummary ?? "No branch isolation summary available.";

    public string OperatorSequenceSummary => SelectedProfile?.InstallPosture.OperatorSequenceSummary ?? "No operator sequence summary available.";

    public IReadOnlyList<string> PreflightChecks => SelectedProfile?.InstallPosture.PreflightChecks ?? [];

    public string NextRecommendedAction => SelectedProfile is null
        ? "Pick a profile to continue."
        : !SelectedProfile.IsInstallDetected
            ? "Run Install first for this branch."
            : string.Equals(SelectedProfile.RuntimeState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "Use Update only when you are ready for a maintenance window, or stop/restart from this page."
                : "The install looks ready. Start the server, or update it if you want to refresh the binaries before launch.";

    public string LifecycleGuidance => SelectedProfile?.RuntimePolicySummary ?? "No runtime state available.";

    public string LaunchReadiness => SelectedProfile?.LaunchReadinessSummary ?? "Launch readiness is unavailable.";

    public string PreflightSummary => SelectedProfile?.InstallPreflightSummary ?? "No preflight summary available.";

    public string ExpectedLauncherPath => SelectedProfile?.ExpectedLauncherPath ?? "No launcher path available.";

    public string ConfigFootprintSummary => SelectedProfile is null
        ? "No config footprint available."
        : $"{(SelectedProfile.ConfigDirectoryDetected ? "Config root detected" : "Config root missing")} | {(SelectedProfile.IniDetected ? "INI present" : "INI missing")} | {(SelectedProfile.SandboxDetected ? "Sandbox present" : "Sandbox missing")} | {(SelectedProfile.WorldDetected ? "World save detected" : "World save not created yet")}";

    public IReadOnlyList<InstallReadinessCheckpointViewModel> FootprintCheckpoints => SelectedProfile is null
        ? []
        :
        [
            new("Install Root", InstallSignal, InstallReadiness),
            new("Cache Root", CacheSignal, CacheReadiness),
            new("Launch Mode", LaunchModeLabel, LaunchModeSummary),
            new("Config Files", ConfigStateHeadline, ConfigFootprintSummary),
            new("Recovery", RecoveryStateHeadline, BackupSafety)
        ];

    [ObservableProperty]
    private string editableInstallDirectory = string.Empty;

    [ObservableProperty]
    private string editableCacheDirectory = string.Empty;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        EditableInstallDirectory = profile?.InstallDirectory ?? string.Empty;
        EditableCacheDirectory = profile?.CacheDirectory ?? string.Empty;
        Notify();
    }

    partial void OnEditableInstallDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasPathOverridesDirty));
        OnPropertyChanged(nameof(PathOverrideSummary));
    }

    partial void OnEditableCacheDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasPathOverridesDirty));
        OnPropertyChanged(nameof(PathOverrideSummary));
    }

    private async Task ExecuteProfileCommandAsync(IAsyncRelayCommand<ProfileCardViewModel> command)
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await command.ExecuteAsync(SelectedProfile);
        Notify();
    }

    private async Task SavePathOverridesAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var installDirectory = EditableInstallDirectory.Trim();
        var cacheDirectory = EditableCacheDirectory.Trim();
        if (string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(cacheDirectory))
        {
            Legacy.StatusMessage = "Install and cache directories are both required.";
            return;
        }

        if (!Path.IsPathRooted(installDirectory) || !Path.IsPathRooted(cacheDirectory))
        {
            Legacy.StatusMessage = "Install and cache directories must use absolute paths.";
            return;
        }

        try
        {
            await _hostApiClient.UpdateProfilePathsAsync(SelectedProfile.ProfileId, installDirectory, cacheDirectory, CancellationToken.None);
            await Legacy.RefreshCommand.ExecuteAsync(null);
            Legacy.StatusMessage = $"Updated install and cache paths for {SelectedProfile.DisplayName}.";
        }
        catch (Exception ex)
        {
            Legacy.StatusMessage = ex.Message;
        }
    }

    private void ResetPathOverrides()
    {
        EditableInstallDirectory = SelectedProfile?.InstallDirectory ?? string.Empty;
        EditableCacheDirectory = SelectedProfile?.CacheDirectory ?? string.Empty;
    }

    private async Task BrowseInstallDirectoryAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync("Choose dedicated server install directory");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            EditableInstallDirectory = folder;
        }
    }

    private async Task BrowseCacheDirectoryAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync("Choose profile cache directory");
        if (!string.IsNullOrWhiteSpace(folder))
        {
            EditableCacheDirectory = folder;
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(RuntimeState));
        OnPropertyChanged(nameof(InstallDirectory));
        OnPropertyChanged(nameof(CacheDirectory));
        OnPropertyChanged(nameof(PathOverrideSummary));
        OnPropertyChanged(nameof(HasPathOverridesDirty));
        OnPropertyChanged(nameof(RecentProfileJobs));
        OnPropertyChanged(nameof(HasRecentProfileJobs));
        OnPropertyChanged(nameof(LastJobSummary));
        OnPropertyChanged(nameof(JobHistorySummary));
        OnPropertyChanged(nameof(BranchChannelSummary));
        OnPropertyChanged(nameof(BranchInstallStatus));
        OnPropertyChanged(nameof(InstallSignal));
        OnPropertyChanged(nameof(CacheSignal));
        OnPropertyChanged(nameof(LaunchModeLabel));
        OnPropertyChanged(nameof(LaunchModeSummary));
        OnPropertyChanged(nameof(ConfigStateHeadline));
        OnPropertyChanged(nameof(RecoveryStateHeadline));
        OnPropertyChanged(nameof(RuntimeActionHeadline));
        OnPropertyChanged(nameof(RuntimeActionSummary));
        OnPropertyChanged(nameof(InstallPostureSummary));
        OnPropertyChanged(nameof(UpdatePostureSummary));
        OnPropertyChanged(nameof(SteamCmdCommandSummary));
        OnPropertyChanged(nameof(SteamCmdScriptPreview));
        OnPropertyChanged(nameof(LaunchCommandPreview));
        OnPropertyChanged(nameof(InstallReadiness));
        OnPropertyChanged(nameof(CacheReadiness));
        OnPropertyChanged(nameof(BackupSafety));
        OnPropertyChanged(nameof(LatestBackupLabel));
        OnPropertyChanged(nameof(InstallSignal));
        OnPropertyChanged(nameof(InstallSignalSummary));
        OnPropertyChanged(nameof(CacheSignal));
        OnPropertyChanged(nameof(CacheSignalSummary));
        OnPropertyChanged(nameof(ConfigSignal));
        OnPropertyChanged(nameof(ConfigSignalSummary));
        OnPropertyChanged(nameof(BackupSignal));
        OnPropertyChanged(nameof(BackupSignalSummary));
        OnPropertyChanged(nameof(LaunchModeSignal));
        OnPropertyChanged(nameof(LaunchModeSignalSummary));
        OnPropertyChanged(nameof(MaintenanceSignal));
        OnPropertyChanged(nameof(MaintenanceSignalSummary));
        OnPropertyChanged(nameof(DeploymentPosture));
        OnPropertyChanged(nameof(MaintenanceWindowSummary));
        OnPropertyChanged(nameof(BranchIsolationSummary));
        OnPropertyChanged(nameof(OperatorSequenceSummary));
        OnPropertyChanged(nameof(PreflightChecks));
        OnPropertyChanged(nameof(NextRecommendedAction));
        OnPropertyChanged(nameof(LifecycleGuidance));
        OnPropertyChanged(nameof(LaunchReadiness));
        OnPropertyChanged(nameof(PreflightSummary));
        OnPropertyChanged(nameof(ExpectedLauncherPath));
        OnPropertyChanged(nameof(ConfigFootprintSummary));
        OnPropertyChanged(nameof(FootprintCheckpoints));
        OnPropertyChanged(nameof(RecentInstallJobs));
        OnPropertyChanged(nameof(RecentUpdateJobs));
        OnPropertyChanged(nameof(LastInstallJobSummary));
        OnPropertyChanged(nameof(LastUpdateJobSummary));
        OnPropertyChanged(nameof(InstallJobHistorySummary));
        OnPropertyChanged(nameof(UpdateJobHistorySummary));
    }

    private void OnRecentOperationJobsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RecentProfileJobs));
        OnPropertyChanged(nameof(HasRecentProfileJobs));
        OnPropertyChanged(nameof(LastJobSummary));
        OnPropertyChanged(nameof(JobHistorySummary));
        OnPropertyChanged(nameof(RecentInstallJobs));
        OnPropertyChanged(nameof(RecentUpdateJobs));
        OnPropertyChanged(nameof(LastInstallJobSummary));
        OnPropertyChanged(nameof(LastUpdateJobSummary));
        OnPropertyChanged(nameof(InstallJobHistorySummary));
        OnPropertyChanged(nameof(UpdateJobHistorySummary));
    }
}

public sealed record InstallReadinessCheckpointViewModel(
    string Title,
    string Status,
    string Summary);
