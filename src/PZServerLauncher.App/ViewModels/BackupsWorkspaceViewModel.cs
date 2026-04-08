using System.Collections.ObjectModel;
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

    public bool HasBackups => Backups.Count > 0;

    public bool HasNoBackups => Backups.Count == 0;

    public bool CanRestore => SelectedProfile is not null && !string.IsNullOrWhiteSpace(SelectedBackup) && !IsBusy;

    public bool CanCreateBackup => SelectedProfile is not null && !IsBusy;

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
            foreach (var backup in backups)
            {
                Backups.Add(backup);
            }

            SelectedBackup = Backups.FirstOrDefault();
            LoadStatus = backups.Count == 0
                ? "No backups exist yet. Create the first manual archive from this page."
                : $"Loaded {backups.Count} backup archive(s) for {profile.DisplayName}.";
            NotifyComputedState();
        }, $"Loading backups for {profile.DisplayName}...");
    }

    private void Reset()
    {
        Backups.Clear();
        SelectedBackup = null;
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
        OnPropertyChanged(nameof(BackupPosture));
        OnPropertyChanged(nameof(BackupInventorySummary));
        OnPropertyChanged(nameof(RecoveryGuidance));
        OnPropertyChanged(nameof(LatestBackupSummary));
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
                Backups.ToList(),
                SelectedBackup,
                SelectedProfile.RuntimeState);

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
}
