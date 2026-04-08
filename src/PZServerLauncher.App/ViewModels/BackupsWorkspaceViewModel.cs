using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class BackupsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
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

    public ObservableCollection<string> Backups { get; } = [];

    public bool HasBackups => Backups.Count > 0;

    public bool HasNoBackups => Backups.Count == 0;

    public bool CanRestore => SelectedProfile is not null && !string.IsNullOrWhiteSpace(SelectedBackup) && !IsBusy;

    public bool CanCreateBackup => SelectedProfile is not null && !IsBusy;

    public string BackupPosture => SelectedProfile is null
        ? "Pick a profile to inspect backup posture."
        : Backups.Count == 0
            ? "No archive has been captured yet. Create a manual backup before major config or update work."
            : $"{Backups.Count} archive(s) are currently available for recovery.";

    public string RecoveryGuidance => SelectedProfile is null
        ? "Choose a profile to review restore guidance."
        : RestartAfterRestore
            ? "Restore will stop the server, unpack the selected archive, then request a restart after recovery."
            : "Restore will stop the server and unpack the selected archive without bringing it back up automatically.";

    public string LatestBackupSummary => Backups.Count == 0
        ? "No backup selected."
        : $"Selected recovery point: {SelectedBackup}";

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
        OnPropertyChanged(nameof(RecoveryGuidance));
        OnPropertyChanged(nameof(LatestBackupSummary));
    }
}
