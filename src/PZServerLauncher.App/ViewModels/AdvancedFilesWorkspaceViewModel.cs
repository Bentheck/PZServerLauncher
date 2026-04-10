using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class AdvancedFilesWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private string _loadedContentSnapshot = string.Empty;

    public AdvancedFilesWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.AdvancedFiles,
            "Advanced Files",
            "Raw Project Zomboid config editing for unsupported or intentionally advanced cases.",
            "Advanced Files are in sync.",
            legacy,
            ["Server INI", "SandboxVars.lua", "SpawnRegions.lua", "SpawnPoints.lua"])
    {
        _hostApiClient = hostApiClient;
        FileKinds = ConfigFileOptionViewModel.All;
        SelectedKind = FileKinds.First(static option => option.Kind == ConfigFileKind.SandboxVars);
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        ReloadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to load raw ini or Lua files."
        : $"Raw config access for {SelectedProfile.DisplayName}. Use this page when a structured editor falls back or when you need the source files directly.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to unlock raw file access."
        : $"{SelectedProfile.DisplayName} can edit the underlying Project Zomboid source files when structured pages need a fallback.";

    public string ActionSummary => IsLoaded
        ? ShowKindMismatchWarning
            ? "Load the selected file kind before saving so the active buffer and chooser stay aligned."
            : "You can edit and save the current file buffer directly."
        : "Pick a file kind and load it before editing.";

    public IReadOnlyList<ConfigFileOptionViewModel> FileKinds { get; }

    public ObservableCollection<string> Diagnostics { get; } = [];

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasNoDiagnostics => Diagnostics.Count == 0;

    public bool CanLoad => SelectedProfile is not null && !IsBusy;

    public bool CanSave => SelectedProfile is not null
        && !IsBusy
        && IsLoaded
        && LoadedKind == SelectedKind.Kind;

    public bool IsEditorEnabled => SelectedProfile is not null && !IsBusy && IsLoaded;

    public bool IsLoadedKindSelected => !IsLoaded || LoadedKind == SelectedKind.Kind;

    public bool ShowKindMismatchWarning => IsLoaded && LoadedKind != SelectedKind.Kind;

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty]
    private ConfigFileOptionViewModel selectedKind = ConfigFileOptionViewModel.All[0];

    [ObservableProperty]
    private string editorContent = string.Empty;

    [ObservableProperty]
    private string loadStatus = "Select a profile, then load a raw config file.";

    [ObservableProperty]
    private string currentPath = "No file selected.";

    [ObservableProperty]
    private string currentSha256 = string.Empty;

    [ObservableProperty]
    private ConfigFileKind loadedKind;

    [ObservableProperty]
    private bool isLoaded;

    [ObservableProperty]
    private bool isBusy;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        Reset(profile);
        NotifyComputedState();
    }

    public override async Task RefreshPageAsync()
    {
        if (IsLoaded && SelectedProfile is not null)
        {
            await LoadAsync();
            return;
        }

        Reset(SelectedProfile);
        NotifyComputedState();
    }

    public override async Task SaveDraftAsync()
    {
        if (CanSave && HasUnsavedChanges)
        {
            await SaveAsync();
            return;
        }

        MarkClean();
    }

    public override Task DiscardDraftAsync()
    {
        if (!HasUnsavedChanges)
        {
            MarkClean();
            return Task.CompletedTask;
        }

        EditorContent = _loadedContentSnapshot;
        MarkClean($"Discarded unsaved edits in {SelectedKind.Label}.");
        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var result = await _hostApiClient.GetRawConfigAsync(SelectedProfile.ProfileId, SelectedKind.Kind, CancellationToken.None);
            if (result is null)
            {
                LoadStatus = $"Unable to load {SelectedKind.Label}.";
                return;
            }

            Apply(result);
            LoadStatus = $"Loaded {SelectedKind.Label} for {SelectedProfile.DisplayName}.";
            NotifyComputedState();
        }, $"Loading {SelectedKind.Label} for {SelectedProfile.DisplayName}...");
    }

    private async Task SaveAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (!IsLoaded || LoadedKind != SelectedKind.Kind)
        {
            LoadStatus = $"Load {SelectedKind.Label} before saving it.";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var payload = new RawConfigFileDto(
                SelectedKind.Kind,
                EditorContent,
                CurrentSha256,
                []);

            var result = await _hostApiClient.SaveRawConfigAsync(SelectedProfile.ProfileId, SelectedKind.Kind, payload, CancellationToken.None);
            if (result is null)
            {
                LoadStatus = $"Unable to save {SelectedKind.Label}.";
                return;
            }

            Apply(result);
            LoadStatus = $"Saved {SelectedKind.Label} for {SelectedProfile.DisplayName}.";
            NotifyComputedState();
        }, $"Saving {SelectedKind.Label} for {SelectedProfile.DisplayName}...");
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
            OnPropertyChanged(nameof(CanLoad));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(IsEditorEnabled));
            OnPropertyChanged(nameof(ShowKindMismatchWarning));
            OnPropertyChanged(nameof(ActionSummary));
        }
    }

    private void Apply(RawConfigFileDto file)
    {
        _loadedContentSnapshot = file.Content;
        EditorContent = file.Content;
        CurrentSha256 = file.Sha256;
        LoadedKind = file.Kind;
        IsLoaded = true;
        CurrentPath = ResolveCurrentPath(file.Kind);

        Diagnostics.Clear();
        foreach (var diagnostic in file.Diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }

        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsLoadedKindSelected));
        OnPropertyChanged(nameof(ShowKindMismatchWarning));
        NotifyComputedState();
        MarkClean();
    }

    private void Reset(ProfileCardViewModel? profile)
    {
        _loadedContentSnapshot = string.Empty;
        EditorContent = string.Empty;
        CurrentSha256 = string.Empty;
        IsLoaded = false;
        LoadedKind = default;
        Diagnostics.Clear();
        CurrentPath = profile is null
            ? "No file selected."
            : ResolveCurrentPath(SelectedKind.Kind);
        LoadStatus = profile is null
            ? "Select a profile, then load a raw config file."
            : $"Ready to load {SelectedKind.Label} for {profile.DisplayName}.";

        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsLoadedKindSelected));
        OnPropertyChanged(nameof(ShowKindMismatchWarning));
        NotifyComputedState();
        MarkClean();
    }

    private string ResolveCurrentPath(ConfigFileKind kind)
    {
        if (SelectedProfile is null)
        {
            return "No file selected.";
        }

        var serverDirectory = Path.Combine(SelectedProfile.CacheDirectory, "Server");
        var serverName = SelectedProfile.EditableServerName;
        return kind switch
        {
            ConfigFileKind.Ini => Path.Combine(serverDirectory, $"{serverName}.ini"),
            ConfigFileKind.SandboxVars => Path.Combine(serverDirectory, $"{serverName}_SandboxVars.lua"),
            ConfigFileKind.SpawnRegions => Path.Combine(serverDirectory, $"{serverName}_spawnregions.lua"),
            ConfigFileKind.SpawnPoints => Path.Combine(serverDirectory, $"{serverName}_spawnpoints.lua"),
            _ => serverDirectory,
        };
    }

    partial void OnSelectedKindChanged(ConfigFileOptionViewModel value)
    {
        CurrentPath = ResolveCurrentPath(value.Kind);
        if (SelectedProfile is null)
        {
            LoadStatus = "Select a profile, then load a raw config file.";
        }
        else if (IsLoaded && LoadedKind != value.Kind)
        {
            LoadStatus = $"Selected {value.Label}. Load it before editing or saving; the current buffer still belongs to {Describe(LoadedKind)}.";
        }
        else if (!IsLoaded)
        {
            LoadStatus = $"Ready to load {value.Label} for {SelectedProfile.DisplayName}.";
        }

        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsLoadedKindSelected));
        OnPropertyChanged(nameof(ShowKindMismatchWarning));
        OnPropertyChanged(nameof(ActionSummary));
    }

    partial void OnEditorContentChanged(string value)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (string.Equals(value, _loadedContentSnapshot, StringComparison.Ordinal))
        {
            MarkClean();
            return;
        }

        MarkDirty($"Unsaved changes in {Describe(LoadedKind)}.");
        OnPropertyChanged(nameof(ActionSummary));
    }

    partial void OnIsLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsLoadedKindSelected));
        OnPropertyChanged(nameof(ShowKindMismatchWarning));
    }

    private static string Describe(ConfigFileKind kind) =>
        kind switch
        {
            ConfigFileKind.Ini => "Server INI",
            ConfigFileKind.SandboxVars => "SandboxVars.lua",
            ConfigFileKind.SpawnRegions => "SpawnRegions.lua",
            ConfigFileKind.SpawnPoints => "SpawnPoints.lua",
            _ => kind.ToString(),
        };

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(IsEditorEnabled));
        OnPropertyChanged(nameof(IsLoadedKindSelected));
        OnPropertyChanged(nameof(ShowKindMismatchWarning));
    }
}
