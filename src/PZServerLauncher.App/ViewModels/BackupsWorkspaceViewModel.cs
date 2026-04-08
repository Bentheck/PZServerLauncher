using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.App.ViewModels;

public partial class BackupsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private static readonly Regex BackupPattern = new(
        "-(manual|preupdate|scheduled)-(\\d{8}-\\d{6})\\.zip$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly ProjectZomboidBackupPostureSummary EmptySummary = new(
        "Pick a profile to inspect recovery coverage.",
        "Latest archive: none captured yet.",
        "No recovery point is currently selected.",
        "Retention posture is unavailable until a profile is selected.",
        "Restore safety is unavailable until a profile is selected.",
        "Recovery continuity will appear once a profile is selected.",
        "Archive mix: none yet.",
        0,
        0,
        0,
        0,
        false,
        false,
        false);

    private readonly LocalHostApiClient _hostApiClient;

    public BackupsWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.Backups,
            "Backups",
            "Backup history, manual backup creation, and restore actions for the selected profile.",
            "Backups are in sync.",
            legacy,
            ["Backup list", "Manual backup", "Restore with restart"])
    {
        _hostApiClient = hostApiClient;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
        RestoreSelectedCommand = new AsyncRelayCommand(RestoreSelectedAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to view backup history and restore options."
        : $"Backup history, recovery posture, and restore actions for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string RecoveryHeroTitle => SelectedProfile is null
        ? "Recovery Center"
        : $"{SelectedProfile.DisplayName} Recovery Center";

    public string RecoveryHeroCopy => SelectedProfile is null
        ? "Select a profile to review archive history, retention policy, and restore behavior."
        : $"Latest archive, retention posture, and restore behavior for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> Backups { get; } = [];

    public ObservableCollection<BackupArchiveRowViewModel> BackupEntries { get; } = [];

    public bool HasBackups => BackupEntries.Count > 0;

    public bool HasNoBackups => BackupEntries.Count == 0;

    public bool CanRestore => SelectedProfile is not null && SelectedBackupArchive is not null && !IsBusy;

    public bool CanCreateBackup => SelectedProfile is not null && !IsBusy;

    public string BackupCountSummary => SelectedProfile is null
        ? "No archive count available."
        : HasBackups
            ? $"{CurrentSummary.TotalBackupCount} total | {CurrentSummary.ManualBackupCount} manual | {CurrentSummary.PreUpdateBackupCount} pre-update | {CurrentSummary.ScheduledBackupCount} scheduled"
            : "No archives yet";

    public string BackupPosture => SelectedProfile is null
        ? "Pick a profile to inspect backup posture."
        : CurrentSummary.CoverageSummary;

    public string BackupInventorySummary => SelectedProfile is null
        ? "Choose a profile to inspect archive history."
        : CurrentSummary.ArchiveMixSummary;

    public string RecoveryGuidance => SelectedProfile is null
        ? "Choose a profile to review restore guidance."
        : $"{CurrentSummary.RestoreSafetySummary} {(RestartAfterRestore
            ? "The host will request a restart after recovery."
            : "The server will stay offline afterward for inspection.")}";

    public string LatestBackupSummary => CurrentSummary.LatestArchiveSummary;

    public string SelectedBackupHeadline => SelectedBackupArchive?.Title ?? "No recovery point selected";

    public string SelectedBackupDetails => CurrentSummary.SelectedArchiveSummary;

    public string PolicySummary => SelectedProfile is null
        ? "No recovery policy loaded."
        : CurrentSummary.RetentionSummary;

    public string RestoreModeSummary => SelectedProfile is null
        ? "Restore behavior not loaded."
        : RestartAfterRestore
            ? "Restore mode: stop the server, unpack the archive, and request a restart."
            : "Restore mode: stop the server, unpack the archive, and leave it offline for inspection.";

    public string OperatorNextStep => SelectedProfile is null
        ? "Select a profile to begin recovery work."
        : CurrentSummary.ContinuitySummary;

    public IAsyncRelayCommand CreateBackupCommand { get; }

    public IAsyncRelayCommand RestoreSelectedCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load backups.";

    [ObservableProperty]
    private string? selectedBackup;

    [ObservableProperty]
    private BackupArchiveRowViewModel? selectedBackupArchive;

    [ObservableProperty]
    private bool restartAfterRestore = true;

    [ObservableProperty]
    private bool isBusy;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        NotifyComputedState();
        _ = LoadAsync(profile);
    }

    public override Task SaveDraftAsync() => Task.CompletedTask;

    public override Task DiscardDraftAsync() => Task.CompletedTask;

    private async Task CreateBackupAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.BackupAsync(SelectedProfile.ProfileId);
            LoadStatus = result?.Message ?? "Manual backup requested.";
            await LoadAsync(SelectedProfile);
            await Legacy.RefreshCommand.ExecuteAsync(null);
            NotifyComputedState();
        }, $"Creating a backup for {SelectedProfile.DisplayName}...");
    }

    private async Task RestoreSelectedAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(SelectedBackup))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.RestoreAsync(SelectedProfile.ProfileId, SelectedBackup, RestartAfterRestore);
            LoadStatus = result?.Message ?? $"Restore requested for {SelectedBackup}.";
            await LoadAsync(SelectedProfile);
            await Legacy.RefreshCommand.ExecuteAsync(null);
            NotifyComputedState();
        }, $"Restoring {SelectedBackup} for {SelectedProfile.DisplayName}...");
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        await RunBusyAsync(async () =>
        {
            var backups = await _hostApiClient.GetBackupsAsync(profile.ProfileId) ?? [];

            Backups.Clear();
            BackupEntries.Clear();
            foreach (var backup in backups)
            {
                Backups.Add(backup);
                BackupEntries.Add(ParseBackupEntry(backup, BackupEntries.Count == 0));
            }

            SelectedBackupArchive = BackupEntries.FirstOrDefault();
            SelectedBackup = SelectedBackupArchive?.ArchiveFileName;
            LoadStatus = backups.Count == 0
                ? "No backups exist yet. Create the first manual archive from this page."
                : $"Loaded {backups.Count} backup archive(s) for {profile.DisplayName}.";
            NotifyComputedState();
        }, $"Loading backups for {profile.DisplayName}...");
    }

    private void Reset()
    {
        Backups.Clear();
        BackupEntries.Clear();
        SelectedBackup = null;
        SelectedBackupArchive = null;
        RestartAfterRestore = true;
        LoadStatus = "Select a profile to load backups.";
        NotifyComputedState();
    }

    private async Task RunBusyAsync(Func<Task> work, string busyMessage)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LoadStatus = busyMessage;
            await work();
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyComputedState();
        }
    }

    partial void OnSelectedBackupChanged(string? value)
    {
        if (!string.Equals(SelectedBackupArchive?.ArchiveFileName, value, StringComparison.Ordinal))
        {
            SelectedBackupArchive = BackupEntries.FirstOrDefault(entry => string.Equals(entry.ArchiveFileName, value, StringComparison.Ordinal));
        }

        NotifyComputedState();
    }

    partial void OnSelectedBackupArchiveChanged(BackupArchiveRowViewModel? value)
    {
        SelectedBackup = value?.ArchiveFileName;
        NotifyComputedState();
    }

    partial void OnRestartAfterRestoreChanged(bool value)
    {
        OnPropertyChanged(nameof(RecoveryGuidance));
        OnPropertyChanged(nameof(RestoreModeSummary));
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(HasNoBackups));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanCreateBackup));
        OnPropertyChanged(nameof(BackupCountSummary));
        OnPropertyChanged(nameof(BackupPosture));
        OnPropertyChanged(nameof(BackupInventorySummary));
        OnPropertyChanged(nameof(RecoveryGuidance));
        OnPropertyChanged(nameof(LatestBackupSummary));
        OnPropertyChanged(nameof(SelectedBackupHeadline));
        OnPropertyChanged(nameof(SelectedBackupDetails));
        OnPropertyChanged(nameof(PolicySummary));
        OnPropertyChanged(nameof(RestoreModeSummary));
        OnPropertyChanged(nameof(OperatorNextStep));
        OnPropertyChanged(nameof(RecoveryHeroTitle));
        OnPropertyChanged(nameof(RecoveryHeroCopy));
    }

    private ProjectZomboidBackupPostureSummary CurrentSummary =>
        SelectedProfile is null
            ? EmptySummary
            : ProjectZomboidBackupPostureSummaryBuilder.Build(
                ToPlanningProfile(SelectedProfile),
                BackupEntries.Select(entry => entry.ArchiveFileName).ToList(),
                SelectedBackup,
                SelectedProfile.RuntimeState);

    private static BackupArchiveRowViewModel ParseBackupEntry(string archiveFileName, bool isLatest)
    {
        var match = BackupPattern.Match(archiveFileName);
        var (title, kindLabel) = match.Success
            ? match.Groups[1].Value.ToLowerInvariant() switch
            {
                "manual" => ("Manual Snapshot", "Manual"),
                "preupdate" => ("Pre-Update Safety Net", "Pre-Update"),
                "scheduled" => ("Scheduled Archive", "Scheduled"),
                _ => ("Recovery Archive", "Archive"),
            }
            : ("Recovery Archive", "Archive");

        var timestampLabel = "Timestamp unavailable.";
        if (match.Success &&
            DateTimeOffset.TryParseExact(
                match.Groups[2].Value,
                "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var createdAtUtc))
        {
            timestampLabel = createdAtUtc.ToLocalTime().ToString("MMM d, yyyy 'at' h:mm tt", CultureInfo.CurrentCulture);
        }

        return new BackupArchiveRowViewModel(
            archiveFileName,
            title,
            kindLabel,
            timestampLabel,
            archiveFileName,
            isLatest);
    }

    private static ServerProfile ToPlanningProfile(ProfileCardViewModel profile) =>
        new()
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            ServerName = profile.EditableServerName,
            InstallDirectory = profile.InstallDirectory,
            CacheDirectory = profile.CacheDirectory,
            Branch = profile.BranchValue,
            BackupPolicy = profile.BackupPolicy,
        };

    public sealed record BackupArchiveRowViewModel(
        string ArchiveFileName,
        string Title,
        string KindLabel,
        string TimestampLabel,
        string FileLabel,
        bool IsLatest);
}
