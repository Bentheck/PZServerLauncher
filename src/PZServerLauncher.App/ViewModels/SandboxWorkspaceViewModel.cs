using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;
using PZServerLauncher.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class SandboxWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly ILauncherRuntime _runtime;
    private SettingsCatalogDto? _catalog;
    private SettingsPageDto? _page;
    private string? _sourceSha256;
    private bool _suppressSelectionRefresh;
    private bool _pageMatchesPreset = true;
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SandboxPresetDto> _presetLookup = new(StringComparer.Ordinal);

    public SandboxWorkspaceViewModel(MainWindowViewModel legacy, ILauncherRuntime runtime)
        : base(
            ProfileWorkspacePageIds.Sandbox,
            "Sandbox",
            "Browse SandboxVars.lua by category with shipped and custom preset comparison plus file-safe apply workflows.",
            "Sandbox settings are in sync.",
            legacy,
            ["Time", "Zombie", "Loot", "World", "Nature", "Meta", "Character", "Vehicles", "Livestock"])
    {
        _runtime = runtime;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        ApplyPresetCommand = new RelayCommand(ApplyPreset, () => CanEdit && HasPresets);
        ResetSelectedCategoryCommand = new RelayCommand(ResetSelectedCategory, () => CanEdit && HasPresets && SelectedCategory is not null);
        ResetAllToPresetCommand = new RelayCommand(ResetAllToPreset, () => CanEdit && HasPresets);
        SavePresetCommand = new AsyncRelayCommand(SavePresetAsync, () => CanEdit && !string.IsNullOrWhiteSpace(PresetName));
        DeletePresetCommand = new AsyncRelayCommand(DeletePresetAsync, () => CanEdit && SelectedPreset is not null && !SelectedPreset.IsBuiltIn);
        ResetWorldCommand = new AsyncRelayCommand(ResetWorldAsync, () => SelectedProfile is not null && !IsBusy);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to load the sandbox editor."
        : $"Category-first Sandbox editor for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to browse SandboxVars.lua as game-familiar categories."
        : $"{SelectedProfile.DisplayName} now uses a category-driven sandbox editor with a real preset library, search, and per-category reset flows.";

    public string ActionSummary => RequiresAdvancedFilesFallback
        ? "Structured editing is temporarily unavailable for this file. Use Advanced Files for raw recovery."
        : CanEdit
            ? "Search globally, switch categories from the left rail, compare against shipped or custom presets, then validate and apply only when you are ready."
            : IsLoading
                ? "Loading structured sandbox settings from the host..."
                : "Sandbox settings are not currently editable.";

    public ObservableCollection<string> FieldErrors { get; } = [];

    public ObservableCollection<SandboxPresetOptionViewModel> Presets { get; } = [];

    public ObservableCollection<SandboxCategoryViewModel> Categories { get; } = [];

    public bool HasFieldErrors => FieldErrors.Count > 0;

    public bool HasPresets => Presets.Count > 0;

    public bool HasCategories => Categories.Count > 0;

    public bool HasSelectedCategory => SelectedCategory is not null;

    public bool HasNoSelectedCategory => !HasSelectedCategory;

    public string ActivePresetLabel => SelectedPreset?.Label ?? "No preset";

    public string PresetSummary => HasPresets
        ? PageMatchesPreset
            ? $"The current sandbox values match {ActivePresetLabel}."
            : $"The current sandbox values differ from {ActivePresetLabel}. Apply the preset, reset a category, or keep the local overrides."
        : "This branch has no sandbox preset library loaded yet.";

    public string PresetLibrarySummary => !HasPresets
        ? "No shipped or custom presets are available yet."
        : SelectedPreset?.IsBuiltIn == true
            ? "Shipped preset selected. Type a new name to save the current editor state as a custom preset."
            : $"Custom preset selected. Save with the same name to update it, or delete it if you no longer need it.";

    public string SelectedCategoryTitle => SelectedCategory?.Title ?? "No category selected";

    public string SelectedCategoryStatus => SelectedCategory?.StatusText ?? "Choose a category to browse its settings.";

    public string SearchSummary => string.IsNullOrWhiteSpace(SearchText)
        ? "Showing every sandbox category."
        : $"Showing categories that match \"{SearchText.Trim()}\".";

    public string ValidationSummary => HasFieldErrors
        ? $"{FieldErrors.Count} validation issue(s) are currently blocking a clean save."
        : RequiresAdvancedFilesFallback
            ? FallbackReason
            : HasUnsavedChanges
                ? "No validation issues yet, but the current sandbox edits are still local."
                : "Structured sandbox values are currently clean.";

    public string WorldResetSummary => SelectedProfile is null
        ? "Select a profile before resetting the world."
        : CreateBackupBeforeWorldReset
            ? "The launcher will capture a manual backup, delete the current multiplayer world, bump ResetID, and randomize Seed."
            : "The launcher will delete the current multiplayer world, bump ResetID, and randomize Seed without taking a backup first.";

    public string WorldDirectory => SelectedProfile is null
        ? "Unavailable"
        : Path.Combine(SelectedProfile.CacheDirectory, "Saves", "Multiplayer", SelectedProfile.EditableServerName);

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public IRelayCommand ApplyPresetCommand { get; }

    public IRelayCommand ResetSelectedCategoryCommand { get; }

    public IRelayCommand ResetAllToPresetCommand { get; }

    public IAsyncRelayCommand SavePresetCommand { get; }

    public IAsyncRelayCommand DeletePresetCommand { get; }

    public IAsyncRelayCommand ResetWorldCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load the sandbox editor.";

    [ObservableProperty]
    private string catalogSummary = "No structured catalog loaded.";

    [ObservableProperty]
    private bool requiresAdvancedFilesFallback;

    [ObservableProperty]
    private string fallbackReason = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool canEdit;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private SandboxPresetOptionViewModel? selectedPreset;

    [ObservableProperty]
    private SandboxCategoryViewModel? selectedCategory;

    [ObservableProperty]
    private string presetName = string.Empty;

    [ObservableProperty]
    private bool createBackupBeforeWorldReset = true;

    [ObservableProperty]
    private bool restartAfterWorldReset;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
        NotifyComputedState();
    }

    public override async Task RefreshPageAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null || _page is null || !CanEdit)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            PZServerLauncher.Core.Profiles.ProjectZomboidBranch.Unstable42,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.Sandbox,
            new Dictionary<string, string?>(_values, StringComparer.Ordinal),
            _sourceSha256,
            true,
            DateTimeOffset.UtcNow);

        await _runtime.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox, payload);
        MarkDirty("Saved Sandbox draft.");
        LoadStatus = "Saved a Sandbox draft. Apply it to write SandboxVars.lua.";
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
            await _runtime.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox);
        }
        catch
        {
        }

        await LoadAsync(SelectedProfile);
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedProfile is null || _catalog is null || !CanEdit)
        {
            return;
        }

        var payload = new SettingsValueSetDto(
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.Sandbox,
            new Dictionary<string, string?>(_values, StringComparer.Ordinal),
            _sourceSha256,
            false,
            null);

        var result = await _runtime.SaveSettingsPageAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox, payload);
        if (result is null)
        {
            LoadStatus = "Sandbox settings could not be saved.";
            return;
        }

        ApplyValidation(result.Validation);
        if (!result.Validation.IsValid || result.Validation.RequiresAdvancedFilesFallback)
        {
            LoadStatus = result.Validation.FallbackReason ?? "Sandbox settings need attention before they can be saved.";
            return;
        }

        try
        {
            await _runtime.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox);
        }
        catch
        {
        }

        ApplyValueSet(result.ValueSet, $"Saved Sandbox settings for {SelectedProfile.DisplayName}.");
        await Legacy.RefreshCommand.ExecuteAsync(null);
        NotifyComputedState();
    }

    private async Task ReloadAsync()
    {
        await LoadAsync(SelectedProfile);
    }

    private async Task ResetWorldAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LoadStatus = $"Resetting the world for {SelectedProfile.DisplayName}...";

            var result = await _runtime.ResetWorldAsync(
                SelectedProfile.ProfileId,
                CreateBackupBeforeWorldReset,
                RestartAfterWorldReset);

            await LoadAsync(SelectedProfile);
            await Legacy.RefreshCommand.ExecuteAsync(null);
            LoadStatus = result?.Message ?? "World reset requested.";
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyComputedState();
            RefreshCommandStates();
        }
    }

    private async Task LoadAsync(ProfileCardViewModel? profile)
    {
        if (profile is null)
        {
            Reset();
            return;
        }

        IsLoading = true;
        LoadStatus = $"Loading Sandbox settings for {profile.DisplayName}...";

        try
        {
            _catalog = await _runtime.GetSettingsCatalogAsync(profile.ProfileId);
            var valueSet = await _runtime.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);
            var draft = await _runtime.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);
            var presets = await _runtime.GetSandboxPresetsAsync(profile.ProfileId) ?? [];

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            _page = _catalog?.Pages.FirstOrDefault(page => string.Equals(page.PageId, ProfileWorkspacePageIds.Sandbox, StringComparison.Ordinal));

            if (valueSet is null)
            {
                Reset();
                LoadStatus = "Sandbox settings could not be loaded.";
                return;
            }

            LoadPresetOptions(presets);
            ApplyValueSet(valueSet, "Sandbox settings loaded from the local host.");
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
        }
    }

    private void LoadPresetOptions(IReadOnlyList<SandboxPresetDto> presets)
    {
        var selectedPresetId = SelectedPreset?.PresetId;
        _presetLookup.Clear();
        Presets.Clear();

        if (presets.Count == 0)
        {
            SelectedPreset = null;
            PresetName = string.Empty;
            NotifyComputedState();
            RefreshCommandStates();
            return;
        }

        foreach (var preset in presets)
        {
            _presetLookup[preset.PresetId] = preset;
            Presets.Add(new SandboxPresetOptionViewModel(preset.PresetId, preset.Label, preset.IsBuiltIn));
        }

        _suppressSelectionRefresh = true;
        SelectedPreset = Presets.FirstOrDefault(preset => string.Equals(preset.PresetId, selectedPresetId, StringComparison.Ordinal))
            ?? Presets[0];
        _suppressSelectionRefresh = false;
        PresetName = SelectedPreset is not null && !SelectedPreset.IsBuiltIn ? SelectedPreset.Label : string.Empty;
        NotifyComputedState();
        RefreshCommandStates();
    }

    private void ApplyDraft(SettingsDraftDto draft)
    {
        _values.Clear();
        foreach (var entry in draft.Values)
        {
            _values[entry.Key] = entry.Value;
        }

        RefreshPresentation();
        if (draft.IsDirty)
        {
            MarkDirty("Loaded a saved Sandbox draft.");
            LoadStatus = "Loaded a saved Sandbox draft from workspace state.";
        }
        else
        {
            MarkClean("Loaded saved Sandbox draft.");
        }
    }

    private void ApplyValueSet(SettingsValueSetDto valueSet, string cleanMessage)
    {
        _sourceSha256 = valueSet.SourceSha256;
        RequiresAdvancedFilesFallback = valueSet.RequiresAdvancedFilesFallback;
        FallbackReason = valueSet.FallbackReason ?? string.Empty;
        CanEdit = !valueSet.RequiresAdvancedFilesFallback;
        _values.Clear();
        foreach (var entry in valueSet.Values)
        {
            _values[entry.Key] = entry.Value;
        }

        RefreshPresentation();
        MarkClean(cleanMessage);
        LoadStatus = valueSet.RequiresAdvancedFilesFallback
            ? valueSet.FallbackReason ?? "Structured Sandbox editing is unavailable for this file."
            : cleanMessage;
        NotifyComputedState();
        RefreshCommandStates();
    }

    private void RefreshPresentation()
    {
        var selectedCategoryId = SelectedCategory?.CategoryId;
        Categories.Clear();

        if (_page is null)
        {
            SelectedCategory = null;
            _pageMatchesPreset = true;
            NotifyComputedState();
            RefreshCommandStates();
            return;
        }

        var preset = TryResolveSelectedPreset();
        var allPresentations = SandboxPagePresentationBuilder.Build(_page, _values, preset, null);
        var presentations = SandboxPagePresentationBuilder.Build(_page, _values, preset, SearchText);
        _pageMatchesPreset = !allPresentations.Any(category => category.HasPresetComparison && !category.MatchesPreset);
        foreach (var category in presentations)
        {
            Categories.Add(new SandboxCategoryViewModel(
                category.CategoryId,
                category.Title,
                BuildCategoryStatus(category),
                category.MatchesPreset,
                category.Sections.Select(section => new SandboxSectionViewModel(
                    section.Section.Title,
                    section.Section.Description,
                    section.Fields.Select(field => new SandboxFieldEditorViewModel(
                        field,
                        CanEdit,
                        FieldErrorsFor(field.Field.FieldId),
                        OnFieldValueChanged))))));
        }

        _suppressSelectionRefresh = true;
        SelectedCategory = Categories.FirstOrDefault(category => string.Equals(category.CategoryId, selectedCategoryId, StringComparison.Ordinal))
            ?? Categories.FirstOrDefault();
        _suppressSelectionRefresh = false;
        NotifyComputedState();
        RefreshCommandStates();
    }

    private IEnumerable<string> FieldErrorsFor(string fieldId) =>
        FieldErrors.Where(entry => entry.StartsWith($"{fieldId}:", StringComparison.Ordinal))
            .Select(entry => entry[(fieldId.Length + 1)..].Trim());

    private SandboxPresetDto? TryResolveSelectedPreset() =>
        SelectedPreset is not null &&
        _presetLookup.TryGetValue(SelectedPreset.PresetId, out var preset)
            ? preset
            : null;

    private void OnFieldValueChanged(SandboxFieldEditorViewModel field, string? value)
    {
        _values[field.FieldId] = value;
        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));
        MarkDirty("Sandbox edits are local until you apply them.");
        LoadStatus = HasPresets
            ? $"Edited sandbox values. {PresetSummary}"
            : "Edited sandbox values. Validate, draft, or apply when ready.";
        RefreshPresentation();
    }

    private void ApplyPreset()
    {
        var preset = TryResolveSelectedPreset();
        if (!CanEdit || preset is null)
        {
            return;
        }

        foreach (var entry in preset.Values)
        {
            _values[entry.Key] = entry.Value;
        }

        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));
        MarkDirty($"Applied the {preset.Label} preset locally.");
        LoadStatus = $"Applied the {preset.Label} preset to local editor state.";
        RefreshPresentation();
    }

    private void ResetSelectedCategory()
    {
        var preset = TryResolveSelectedPreset();
        if (!CanEdit || SelectedCategory is null || preset is null)
        {
            return;
        }

        foreach (var fieldId in SelectedCategory.Sections.SelectMany(section => section.Fields).Select(field => field.FieldId))
        {
            if (preset.Values.TryGetValue(fieldId, out var presetValue))
            {
                _values[fieldId] = presetValue;
            }
        }

        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));
        MarkDirty($"Reset {SelectedCategory.Title} to the {preset.Label} preset.");
        LoadStatus = $"Reset {SelectedCategory.Title} to the {preset.Label} preset.";
        RefreshPresentation();
    }

    private void ResetAllToPreset()
    {
        ApplyPreset();
    }

    private async Task SavePresetAsync()
    {
        if (SelectedProfile is null || !CanEdit)
        {
            return;
        }

        try
        {
            IsBusy = true;
            LoadStatus = $"Saving sandbox preset '{PresetName.Trim()}'...";
            var savedPreset = await _runtime.SaveSandboxPresetAsync(SelectedProfile.ProfileId, PresetName, new Dictionary<string, string?>(_values, StringComparer.Ordinal));
            if (savedPreset is null)
            {
                LoadStatus = "Sandbox preset could not be saved.";
                return;
            }

            var presets = await _runtime.GetSandboxPresetsAsync(SelectedProfile.ProfileId) ?? [];
            LoadPresetOptions(presets);
            _suppressSelectionRefresh = true;
            SelectedPreset = Presets.FirstOrDefault(option => string.Equals(option.PresetId, savedPreset.PresetId, StringComparison.Ordinal))
                ?? SelectedPreset;
            _suppressSelectionRefresh = false;
            PresetName = savedPreset.Label;
            RefreshPresentation();
            LoadStatus = $"Saved sandbox preset '{savedPreset.Label}'.";
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyComputedState();
            RefreshCommandStates();
        }
    }

    private async Task DeletePresetAsync()
    {
        if (SelectedProfile is null || SelectedPreset is null || SelectedPreset.IsBuiltIn || !CanEdit)
        {
            return;
        }

        var deletedLabel = SelectedPreset.Label;
        try
        {
            IsBusy = true;
            LoadStatus = $"Deleting sandbox preset '{deletedLabel}'...";
            await _runtime.DeleteSandboxPresetAsync(SelectedProfile.ProfileId, SelectedPreset.PresetId);
            var presets = await _runtime.GetSandboxPresetsAsync(SelectedProfile.ProfileId) ?? [];
            LoadPresetOptions(presets);
            PresetName = string.Empty;
            RefreshPresentation();
            LoadStatus = $"Deleted sandbox preset '{deletedLabel}'.";
        }
        catch (Exception ex)
        {
            LoadStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyComputedState();
            RefreshCommandStates();
        }
    }

    private void ApplyValidation(SettingsValidationResultDto validation)
    {
        FieldErrors.Clear();
        foreach (var pageError in validation.PageErrors)
        {
            FieldErrors.Add(pageError);
        }

        foreach (var entry in validation.FieldErrors.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            foreach (var error in entry.Value)
            {
                FieldErrors.Add($"{entry.Key}: {error}");
            }
        }

        OnPropertyChanged(nameof(HasFieldErrors));
        RefreshPresentation();
    }

    private void Reset()
    {
        _catalog = null;
        _page = null;
        _sourceSha256 = null;
        CatalogSummary = "No structured catalog loaded.";
        RequiresAdvancedFilesFallback = false;
        FallbackReason = string.Empty;
        CanEdit = false;
        _values.Clear();
        _presetLookup.Clear();
        Presets.Clear();
        Categories.Clear();
        _pageMatchesPreset = true;
        SelectedPreset = null;
        SelectedCategory = null;
        PresetName = string.Empty;
        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));
        MarkClean("Sandbox settings are in sync.");
        LoadStatus = "Select a profile to load the sandbox editor.";
        NotifyComputedState();
        RefreshCommandStates();
    }

    private void RefreshCommandStates()
    {
        ApplyPresetCommand.NotifyCanExecuteChanged();
        ResetSelectedCategoryCommand.NotifyCanExecuteChanged();
        ResetAllToPresetCommand.NotifyCanExecuteChanged();
        SavePresetCommand.NotifyCanExecuteChanged();
        DeletePresetCommand.NotifyCanExecuteChanged();
        ResetWorldCommand.NotifyCanExecuteChanged();
    }

    private static string BuildCategoryStatus(SandboxCategoryPresentation category)
    {
        if (!category.HasPresetComparison)
        {
            return $"{category.Sections.Sum(section => section.Fields.Count)} field(s)";
        }

        return category.MatchesPreset
            ? $"Matches preset ({category.MatchingFieldCount}/{category.ComparedFieldCount})"
            : $"Changed ({category.MatchingFieldCount}/{category.ComparedFieldCount})";
    }

    private bool PageMatchesPreset => _pageMatchesPreset;

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(HasPresets));
        OnPropertyChanged(nameof(HasCategories));
        OnPropertyChanged(nameof(HasSelectedCategory));
        OnPropertyChanged(nameof(HasNoSelectedCategory));
        OnPropertyChanged(nameof(ActivePresetLabel));
        OnPropertyChanged(nameof(PresetSummary));
        OnPropertyChanged(nameof(PresetLibrarySummary));
        OnPropertyChanged(nameof(SelectedCategoryTitle));
        OnPropertyChanged(nameof(SelectedCategoryStatus));
        OnPropertyChanged(nameof(SearchSummary));
        OnPropertyChanged(nameof(ValidationSummary));
        OnPropertyChanged(nameof(WorldResetSummary));
        OnPropertyChanged(nameof(WorldDirectory));
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshPresentation();
    }

    partial void OnSelectedPresetChanged(SandboxPresetOptionViewModel? value)
    {
        if (_suppressSelectionRefresh)
        {
            return;
        }

        PresetName = value is not null && !value.IsBuiltIn ? value.Label : string.Empty;
        RefreshPresentation();
        NotifyComputedState();
        RefreshCommandStates();
    }

    partial void OnSelectedCategoryChanged(SandboxCategoryViewModel? value)
    {
        if (_suppressSelectionRefresh)
        {
            return;
        }

        NotifyComputedState();
        RefreshCommandStates();
    }

    partial void OnCreateBackupBeforeWorldResetChanged(bool value)
    {
        OnPropertyChanged(nameof(WorldResetSummary));
    }

    partial void OnRestartAfterWorldResetChanged(bool value)
    {
        OnPropertyChanged(nameof(WorldResetSummary));
    }

    partial void OnPresetNameChanged(string value)
    {
        OnPropertyChanged(nameof(PresetLibrarySummary));
        RefreshCommandStates();
    }
}
