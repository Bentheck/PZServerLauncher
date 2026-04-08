using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.App.ViewModels;

public partial class ModsAndMapsWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private bool _isApplyingState;
    private SettingsCatalogDto? _catalog;

    public ModsAndMapsWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.ModsAndMaps,
            "Mods & Maps",
            "Workshop items, mod IDs, and map folders from the real Project Zomboid server INI.",
            "Mods & Maps settings are in sync.",
            legacy,
            ["Ordered workshop queue", "Mod load order", "Map load order", "Bulk paste and scanner diagnostics"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        ScanCommand = new AsyncRelayCommand(RunScanAsync);
        AddWorkshopEntryCommand = new RelayCommand(AddWorkshopEntry, () => CanAddWorkshopEntry);
        AddEnabledModEntryCommand = new RelayCommand(AddEnabledModEntry, () => CanAddEnabledModEntry);
        AddMapEntryCommand = new RelayCommand(AddMapEntry, () => CanAddMapEntry);
        MoveEntryUpCommand = new RelayCommand<PresetEntryViewModel>(MoveEntryUp);
        MoveEntryDownCommand = new RelayCommand<PresetEntryViewModel>(MoveEntryDown);
        RemoveEntryCommand = new RelayCommand<PresetEntryViewModel>(RemoveEntry);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to manage workshop items, mods, and maps."
        : $"Workshop, mod, and map settings for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to unlock workshop and map management."
        : $"{SelectedProfile.DisplayName} now manages the actual WorkshopItems, Mods, and Map keys through an ordered preset editor instead of raw text alone.";

    public string ActionSummary => HasUnsavedChanges
        ? "Apply or discard changes before scanning so diagnostics reflect the saved preset."
        : CanScan
            ? "The ordered preset is in sync. Scan local workshop content to validate what is actually installed."
            : "Load a profile to inspect workshop, mod, and map settings.";

    public string WorkshopSummary => WorkshopEntries.Count == 0
        ? "No workshop items queued yet."
        : $"{WorkshopEntries.Count} workshop item(s) in install order.";

    public string EnabledModsSummary => EnabledModEntries.Count == 0
        ? "No enabled mod IDs saved yet."
        : $"{EnabledModEntries.Count} mod ID(s) in load order.";

    public string MapOrderSummary => MapEntries.Count == 0
        ? "No custom map folders listed."
        : $"{MapEntries.Count} map folder(s) in load order.";

    public string ScanReadinessSummary => SelectedProfile is null
        ? "Choose a profile first."
        : HasUnsavedChanges
            ? "Apply or discard local edits before scanning so diagnostics match the live preset."
            : HasDiagnostics
                ? $"{Diagnostics.Count} diagnostic(s) from the last local scan."
                : "Ready to scan the local workshop cache.";

    public string ModsNextStepSummary
    {
        get
        {
            if (SelectedProfile is null)
            {
                return "Select a profile to start building a real preset.";
            }

            if (WorkshopEntries.Count == 0 && EnabledModEntries.Count == 0 && MapEntries.Count == 0)
            {
                return "Start by pasting a workshop URL or ID, then shape the mod and map order from there.";
            }

            if (HasUnsavedChanges)
            {
                return "Apply the current order first, then run a scan so diagnostics match the saved server preset.";
            }

            return Diagnostics.Count > 0
                ? "Resolve the scanner diagnostics or accept them, then keep the saved order aligned with your map stack."
                : "Scan again after any install change so the saved preset stays aligned with local workshop content.";
        }
    }

    public ObservableCollection<string> Diagnostics { get; } = [];

    public ObservableCollection<PresetEntryViewModel> WorkshopEntries { get; } = [];

    public ObservableCollection<PresetEntryViewModel> EnabledModEntries { get; } = [];

    public ObservableCollection<PresetEntryViewModel> MapEntries { get; } = [];

    public bool HasDiagnostics => Diagnostics.Count > 0;

    public bool HasNoDiagnostics => Diagnostics.Count == 0;

    public bool HasWorkshopEntries => WorkshopEntries.Count > 0;

    public bool HasNoWorkshopEntries => WorkshopEntries.Count == 0;

    public bool HasEnabledModEntries => EnabledModEntries.Count > 0;

    public bool HasNoEnabledModEntries => EnabledModEntries.Count == 0;

    public bool HasMapEntries => MapEntries.Count > 0;

    public bool HasNoMapEntries => MapEntries.Count == 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IAsyncRelayCommand ScanCommand { get; }

    public IRelayCommand AddWorkshopEntryCommand { get; }

    public IRelayCommand AddEnabledModEntryCommand { get; }

    public IRelayCommand AddMapEntryCommand { get; }

    public IRelayCommand<PresetEntryViewModel> MoveEntryUpCommand { get; }

    public IRelayCommand<PresetEntryViewModel> MoveEntryDownCommand { get; }

    public IRelayCommand<PresetEntryViewModel> RemoveEntryCommand { get; }

    public bool CanScan => SelectedProfile is not null && !HasUnsavedChanges;

    public bool CanAddWorkshopEntry => !string.IsNullOrWhiteSpace(NewWorkshopEntry);

    public bool CanAddEnabledModEntry => !string.IsNullOrWhiteSpace(NewEnabledModEntry);

    public bool CanAddMapEntry => !string.IsNullOrWhiteSpace(NewMapEntry);

    [ObservableProperty]
    private string loadStatus = "Select a profile to load workshop, mod, and map settings.";

    [ObservableProperty]
    private string catalogSummary = "No structured catalog loaded.";

    [ObservableProperty]
    private string workshopItemIdsText = string.Empty;

    [ObservableProperty]
    private string enabledModIdsText = string.Empty;

    [ObservableProperty]
    private string mapFoldersText = string.Empty;

    [ObservableProperty]
    private string newWorkshopEntry = string.Empty;

    [ObservableProperty]
    private string newEnabledModEntry = string.Empty;

    [ObservableProperty]
    private string newMapEntry = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
        NotifyComputedState();
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            SelectedProfile.Branch.Contains("42", StringComparison.Ordinal) ? ProjectZomboidBranch.Unstable42 : ProjectZomboidBranch.Stable41,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.ModsAndMaps,
            BuildDraftValues(),
            null,
            true,
            DateTimeOffset.UtcNow);

        await _hostApiClient.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps, payload);
        MarkClean("Saved Mods & Maps draft.");
        LoadStatus = "Saved a Mods & Maps draft. Apply settings to update the active profile preset.";
        NotifyComputedState();
    }

    public override async Task DiscardDraftAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
        }
        catch
        {
        }

        await LoadAsync(SelectedProfile);
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        var updatedPreset = await _hostApiClient.UpdateWorkshopPresetAsync(SelectedProfile.ProfileId, BuildPreset());
        if (updatedPreset is null)
        {
            LoadStatus = "Mods & Maps settings could not be saved.";
            return;
        }

        try
        {
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);
        }
        catch
        {
        }

        ApplyPreset(updatedPreset);
        MarkClean($"Saved Mods & Maps settings for {SelectedProfile.DisplayName}.");
        await RunScanCoreAsync(SelectedProfile.ProfileId, overwriteEditors: true);
        await Legacy.RefreshCommand.ExecuteAsync(null);
        NotifyComputedState();
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task RunScanAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (HasUnsavedChanges)
        {
            LoadStatus = "Apply or discard Mods & Maps changes before running a scan so diagnostics match the saved preset.";
            return;
        }

        await RunScanCoreAsync(SelectedProfile.ProfileId, overwriteEditors: true);
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        IsLoading = true;
        LoadStatus = $"Loading Mods & Maps settings for {profile.DisplayName}...";

        try
        {
            _catalog = await _hostApiClient.GetSettingsCatalogAsync(profile.ProfileId);
            var preset = await _hostApiClient.GetWorkshopPresetAsync(profile.ProfileId) ?? WorkshopPreset.Empty;
            var draft = await _hostApiClient.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.ModsAndMaps);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            ApplyPreset(preset);
            MarkClean("Loaded Mods & Maps settings from the local host.");
            Diagnostics.Clear();
            OnPropertyChanged(nameof(HasDiagnostics));
            OnPropertyChanged(nameof(HasNoDiagnostics));
            LoadStatus = "Loaded WorkshopItems, Mods, and Map from the local host. Run a scan after applying changes to validate local workshop content.";
            NotifyComputedState();

            if (draft is not null && draft.Values.Count > 0)
            {
                ApplyDraft(draft);
            }
        }
        catch (Exception ex)
        {
            Reset();
            LoadStatus = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanScan));
        }
    }

    private async Task RunScanCoreAsync(string profileId, bool overwriteEditors)
    {
        var result = await _hostApiClient.ScanWorkshopAsync(profileId);
        if (result is null)
        {
            LoadStatus = "Workshop scan did not return a result.";
            return;
        }

        if (overwriteEditors)
        {
            ApplyPreset(result.Preset);
            MarkClean("Workshop scan normalized the saved preset.");
        }

        Diagnostics.Clear();
        foreach (var diagnostic in result.Diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }

        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));
        LoadStatus = result.Diagnostics.Count == 0
            ? "Workshop scan passed. Saved preset is present in the local workshop cache."
            : $"Workshop scan completed with {result.Diagnostics.Count} issue(s).";
        NotifyComputedState();
    }

    private void ApplyPreset(WorkshopPreset preset)
    {
        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Join(Environment.NewLine, preset.WorkshopItemIds);
            EnabledModIdsText = string.Join(Environment.NewLine, preset.EnabledModIds);
            MapFoldersText = string.Join(Environment.NewLine, preset.MapFolders);
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();
    }

    private void ApplyDraft(SettingsDraftDto draft)
    {
        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = GetDraftValue(draft.Values, ".mods.workshop-items");
            EnabledModIdsText = GetDraftValue(draft.Values, ".mods.enabled-mods");
            MapFoldersText = GetDraftValue(draft.Values, ".mods.map-folders");
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();

        if (draft.IsDirty)
        {
            MarkDirty("Loaded a saved Mods & Maps draft.");
            LoadStatus = "Loaded a saved Mods & Maps draft from SQLite-backed workspace state.";
        }
        else
        {
            MarkClean("Loaded saved Mods & Maps draft.");
        }
    }

    private IReadOnlyDictionary<string, string?> BuildDraftValues()
    {
        var prefix = SelectedProfile?.Branch.Contains("42", StringComparison.Ordinal) == true ? "b42" : "b41";
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{prefix}.mods.workshop-items"] = WorkshopItemIdsText,
            [$"{prefix}.mods.enabled-mods"] = EnabledModIdsText,
            [$"{prefix}.mods.map-folders"] = MapFoldersText,
        };
    }

    private WorkshopPreset BuildPreset() =>
        new()
        {
            WorkshopItemIds = SplitLines(WorkshopItemIdsText),
            EnabledModIds = SplitLines(EnabledModIdsText),
            MapFolders = SplitLines(MapFoldersText),
        };

    private static IReadOnlyList<string> SplitLines(string text) =>
        text.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(line => line.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

    private static string GetDraftValue(IReadOnlyDictionary<string, string?> values, string suffix)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? string.Empty : values[key] ?? string.Empty;
    }

    private void AddWorkshopEntry()
    {
        AddEntry(PresetEntryKind.Workshop, NewWorkshopEntry);
        NewWorkshopEntry = string.Empty;
        AddWorkshopEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddEnabledModEntry()
    {
        AddEntry(PresetEntryKind.EnabledMod, NewEnabledModEntry);
        NewEnabledModEntry = string.Empty;
        AddEnabledModEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddMapEntry()
    {
        AddEntry(PresetEntryKind.MapFolder, NewMapEntry);
        NewMapEntry = string.Empty;
        AddMapEntryCommand.NotifyCanExecuteChanged();
    }

    private void AddEntry(PresetEntryKind kind, string rawValue)
    {
        var value = rawValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var values = GetValues(kind).ToList();
        values.Add(value);
        ReplaceEntries(kind, values);
        NotifyEdited($"Added a new {GetKindLabel(kind).ToLowerInvariant()} entry.");
    }

    private void MoveEntryUp(PresetEntryViewModel? entry)
    {
        if (entry is null || entry.Position <= 0)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        (values[entry.Position - 1], values[entry.Position]) = (values[entry.Position], values[entry.Position - 1]);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Moved {GetKindLabel(entry.Kind).ToLowerInvariant()} entry up.");
    }

    private void MoveEntryDown(PresetEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        if (entry.Position < 0 || entry.Position >= values.Count - 1)
        {
            return;
        }

        (values[entry.Position + 1], values[entry.Position]) = (values[entry.Position], values[entry.Position + 1]);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Moved {GetKindLabel(entry.Kind).ToLowerInvariant()} entry down.");
    }

    private void RemoveEntry(PresetEntryViewModel? entry)
    {
        if (entry is null)
        {
            return;
        }

        var values = GetValues(entry.Kind).ToList();
        if (entry.Position < 0 || entry.Position >= values.Count)
        {
            return;
        }

        values.RemoveAt(entry.Position);
        ReplaceEntries(entry.Kind, values);
        NotifyEdited($"Removed a {GetKindLabel(entry.Kind).ToLowerInvariant()} entry.");
    }

    private void ReplaceEntries(PresetEntryKind kind, IReadOnlyList<string> values)
    {
        _isApplyingState = true;
        try
        {
            SetText(kind, string.Join(Environment.NewLine, values));
        }
        finally
        {
            _isApplyingState = false;
        }

        RebuildEntryCollections();
    }

    private IReadOnlyList<string> GetValues(PresetEntryKind kind) =>
        kind switch
        {
            PresetEntryKind.Workshop => SplitLines(WorkshopItemIdsText),
            PresetEntryKind.EnabledMod => SplitLines(EnabledModIdsText),
            PresetEntryKind.MapFolder => SplitLines(MapFoldersText),
            _ => [],
        };

    private void SetText(PresetEntryKind kind, string text)
    {
        switch (kind)
        {
            case PresetEntryKind.Workshop:
                WorkshopItemIdsText = text;
                break;
            case PresetEntryKind.EnabledMod:
                EnabledModIdsText = text;
                break;
            case PresetEntryKind.MapFolder:
                MapFoldersText = text;
                break;
        }
    }

    private void RebuildEntryCollections()
    {
        ReplaceCollection(WorkshopEntries, SplitLines(WorkshopItemIdsText), PresetEntryKind.Workshop);
        ReplaceCollection(EnabledModEntries, SplitLines(EnabledModIdsText), PresetEntryKind.EnabledMod);
        ReplaceCollection(MapEntries, SplitLines(MapFoldersText), PresetEntryKind.MapFolder);

        OnPropertyChanged(nameof(HasWorkshopEntries));
        OnPropertyChanged(nameof(HasNoWorkshopEntries));
        OnPropertyChanged(nameof(HasEnabledModEntries));
        OnPropertyChanged(nameof(HasNoEnabledModEntries));
        OnPropertyChanged(nameof(HasMapEntries));
        OnPropertyChanged(nameof(HasNoMapEntries));
        OnPropertyChanged(nameof(WorkshopSummary));
        OnPropertyChanged(nameof(EnabledModsSummary));
        OnPropertyChanged(nameof(MapOrderSummary));
        OnPropertyChanged(nameof(ScanReadinessSummary));
        OnPropertyChanged(nameof(ModsNextStepSummary));
    }

    private static void ReplaceCollection(
        ObservableCollection<PresetEntryViewModel> target,
        IReadOnlyList<string> values,
        PresetEntryKind kind)
    {
        target.Clear();
        for (var index = 0; index < values.Count; index++)
        {
            target.Add(new PresetEntryViewModel(kind, index, values[index]));
        }
    }

    private void Reset()
    {
        _catalog = null;
        CatalogSummary = "No structured catalog loaded.";
        Diagnostics.Clear();
        WorkshopEntries.Clear();
        EnabledModEntries.Clear();
        MapEntries.Clear();
        OnPropertyChanged(nameof(HasDiagnostics));
        OnPropertyChanged(nameof(HasNoDiagnostics));

        _isApplyingState = true;
        try
        {
            WorkshopItemIdsText = string.Empty;
            EnabledModIdsText = string.Empty;
            MapFoldersText = string.Empty;
            NewWorkshopEntry = string.Empty;
            NewEnabledModEntry = string.Empty;
            NewMapEntry = string.Empty;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("Mods & Maps settings are in sync.");
        NotifyComputedState();
    }

    partial void OnWorkshopItemIdsTextChanged(string value) => NotifyTextEdited();
    partial void OnEnabledModIdsTextChanged(string value) => NotifyTextEdited();
    partial void OnMapFoldersTextChanged(string value) => NotifyTextEdited();

    partial void OnNewWorkshopEntryChanged(string value)
    {
        AddWorkshopEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddWorkshopEntry));
    }

    partial void OnNewEnabledModEntryChanged(string value)
    {
        AddEnabledModEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddEnabledModEntry));
    }

    partial void OnNewMapEntryChanged(string value)
    {
        AddMapEntryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanAddMapEntry));
    }

    private void NotifyTextEdited()
    {
        if (_isApplyingState)
        {
            return;
        }

        RebuildEntryCollections();
        NotifyEdited("Mods & Maps changed locally. Save a draft or apply the new preset before scanning.");
    }

    private void NotifyEdited(string statusMessage)
    {
        MarkDirty("Unsaved changes in Mods & Maps.");
        LoadStatus = statusMessage;
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(WorkshopSummary));
        OnPropertyChanged(nameof(EnabledModsSummary));
        OnPropertyChanged(nameof(MapOrderSummary));
        OnPropertyChanged(nameof(ScanReadinessSummary));
        OnPropertyChanged(nameof(ModsNextStepSummary));
        OnPropertyChanged(nameof(CanScan));
        OnPropertyChanged(nameof(CanAddWorkshopEntry));
        OnPropertyChanged(nameof(CanAddEnabledModEntry));
        OnPropertyChanged(nameof(CanAddMapEntry));
        OnPropertyChanged(nameof(HasWorkshopEntries));
        OnPropertyChanged(nameof(HasNoWorkshopEntries));
        OnPropertyChanged(nameof(HasEnabledModEntries));
        OnPropertyChanged(nameof(HasNoEnabledModEntries));
        OnPropertyChanged(nameof(HasMapEntries));
        OnPropertyChanged(nameof(HasNoMapEntries));
    }

    private static string GetKindLabel(PresetEntryKind kind) =>
        kind switch
        {
            PresetEntryKind.Workshop => "Workshop",
            PresetEntryKind.EnabledMod => "Mod",
            PresetEntryKind.MapFolder => "Map",
            _ => "Preset",
        };

    public sealed class PresetEntryViewModel(PresetEntryKind kind, int position, string value)
    {
        public PresetEntryKind Kind { get; } = kind;

        public int Position { get; } = position;

        public string OrderLabel => $"{position + 1:00}";

        public string Value { get; } = value;
    }

    public enum PresetEntryKind
    {
        Workshop,
        EnabledMod,
        MapFolder,
    }
}
