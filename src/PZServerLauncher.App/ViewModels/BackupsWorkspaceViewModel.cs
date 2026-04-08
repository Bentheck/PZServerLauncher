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
        : $"Backup history and restore actions for {SelectedProfile.DisplayName}.";

    public ObservableCollection<string> Backups { get; } = [];

    public bool HasBackups => Backups.Count > 0;

    public bool HasNoBackups => Backups.Count == 0;

    public bool CanRestore => SelectedProfile is not null && !string.IsNullOrWhiteSpace(SelectedBackup) && !IsBusy;

    public bool CanCreateBackup => SelectedProfile is not null && !IsBusy;

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
            OnPropertyChanged(nameof(HasBackups));
            OnPropertyChanged(nameof(HasNoBackups));
            OnPropertyChanged(nameof(CanRestore));
        }, $"Loading backups for {profile.DisplayName}...");
    }

    private void Reset()
    {
        Backups.Clear();
        SelectedBackup = null;
        RestartAfterRestore = true;
        LoadStatus = "Select a profile to load backups.";
        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(HasNoBackups));
        OnPropertyChanged(nameof(CanRestore));
        OnPropertyChanged(nameof(CanCreateBackup));
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
            OnPropertyChanged(nameof(CanRestore));
            OnPropertyChanged(nameof(CanCreateBackup));
        }
    }

    partial void OnSelectedBackupChanged(string? value)
    {
        OnPropertyChanged(nameof(CanRestore));
    }
}
