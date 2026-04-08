using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PZServerLauncher.App.Services;
using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Contracts.Runtime;

namespace PZServerLauncher.App.ViewModels;

public partial class SandboxWorkspaceViewModel : ProfileWorkspacePageViewModelBase
{
    private readonly LocalHostApiClient _hostApiClient;
    private SettingsCatalogDto? _catalog;
    private string? _sourceSha256;
    private bool _isApplyingState;

    public SandboxWorkspaceViewModel(MainWindowViewModel legacy, LocalHostApiClient hostApiClient)
        : base(
            ProfileWorkspacePageIds.Sandbox,
            "Sandbox",
            "Branch-aware gameplay and world settings from SandboxVars.lua.",
            "Sandbox settings are in sync.",
            legacy,
            ["World setup", "Utilities", "Loot and climate", "Player experience"])
    {
        _hostApiClient = hostApiClient;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
    }

    public override string PageSummary => SelectedProfile is null
        ? "Select a profile to load the structured Sandbox editor."
        : $"Structured Sandbox settings for {SelectedProfile.DisplayName}.";

    public string ProfileDisplayName => SelectedProfile?.DisplayName ?? "No profile selected";

    public string Branch => SelectedProfile?.Branch ?? "Unknown";

    public string WorkspaceSummary => SelectedProfile is null
        ? "Choose a profile to unlock Sandbox settings."
        : $"{SelectedProfile.DisplayName} is ready for branch-aware gameplay and world tuning.";

    public string ActionSummary => RequiresAdvancedFilesFallback
        ? "Structured editing is unavailable for this Sandbox file. Use Advanced Files for raw recovery."
        : CanEdit
            ? "Apply changes to write SandboxVars.lua, or keep a draft while you experiment."
            : IsLoading
                ? "Loading structured Sandbox settings from the host..."
                : "Sandbox settings are not currently editable.";

    public ObservableCollection<string> FieldErrors { get; } = [];

    public bool HasFieldErrors => FieldErrors.Count > 0;

    public IAsyncRelayCommand SaveSettingsCommand { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    [ObservableProperty]
    private string loadStatus = "Select a profile to load the structured Sandbox editor.";

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
    private string zombies = string.Empty;

    [ObservableProperty]
    private string distribution = string.Empty;

    [ObservableProperty]
    private string dayLength = string.Empty;

    [ObservableProperty]
    private string startYear = string.Empty;

    [ObservableProperty]
    private string startMonth = string.Empty;

    [ObservableProperty]
    private string startDay = string.Empty;

    [ObservableProperty]
    private string startTime = string.Empty;

    [ObservableProperty]
    private string waterShutModifier = string.Empty;

    [ObservableProperty]
    private string electricityShutModifier = string.Empty;

    [ObservableProperty]
    private string foodLoot = string.Empty;

    [ObservableProperty]
    private string weaponLoot = string.Empty;

    [ObservableProperty]
    private string otherLoot = string.Empty;

    [ObservableProperty]
    private string temperature = string.Empty;

    [ObservableProperty]
    private string rain = string.Empty;

    [ObservableProperty]
    private bool starterKit;

    [ObservableProperty]
    private bool nutrition;

    protected override void OnSelectedProfileChangedCore(ProfileCardViewModel? profile)
    {
        _ = LoadAsync(profile);
        NotifyComputedState();
    }

    public override async Task SaveDraftAsync()
    {
        if (SelectedProfile is null || _catalog is null || !CanEdit)
        {
            return;
        }

        var payload = new SettingsDraftDto(
            SelectedProfile.ProfileId,
            SelectedProfile.Branch.Contains("42", StringComparison.Ordinal) ? PZServerLauncher.Core.Profiles.ProjectZomboidBranch.Unstable42 : PZServerLauncher.Core.Profiles.ProjectZomboidBranch.Stable41,
            _catalog.CatalogId,
            _catalog.CatalogVersion,
            ProfileWorkspacePageIds.Sandbox,
            BuildValues(),
            _sourceSha256,
            true,
            DateTimeOffset.UtcNow);

        await _hostApiClient.SaveSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox, payload);
        MarkClean("Saved Sandbox draft.");
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
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox);
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
            BuildValues(),
            _sourceSha256,
            false,
            null);

        var result = await _hostApiClient.SaveSettingsPageAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox, payload);
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
            await _hostApiClient.DeleteSettingsDraftAsync(SelectedProfile.ProfileId, ProfileWorkspacePageIds.Sandbox);
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
            _catalog = await _hostApiClient.GetSettingsCatalogAsync(profile.ProfileId);
            var valueSet = await _hostApiClient.GetSettingsPageAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);
            var draft = await _hostApiClient.GetSettingsDraftAsync(profile.ProfileId, ProfileWorkspacePageIds.Sandbox);

            CatalogSummary = _catalog is null
                ? "No structured catalog available."
                : $"{_catalog.CatalogId} v{_catalog.CatalogVersion} | {_catalog.Branch}";

            if (valueSet is null)
            {
                Reset();
                LoadStatus = "Sandbox settings could not be loaded.";
                return;
            }

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

    private void ApplyDraft(SettingsDraftDto draft)
    {
        ApplyValues(draft.Values);
        if (draft.IsDirty)
        {
            MarkDirty("Loaded a saved Sandbox draft.");
            LoadStatus = "Loaded a saved Sandbox draft from SQLite-backed workspace state.";
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
        ApplyValues(valueSet.Values);
        MarkClean(cleanMessage);
            LoadStatus = valueSet.RequiresAdvancedFilesFallback
                ? valueSet.FallbackReason ?? "Structured Sandbox editing is unavailable for this file."
                : cleanMessage;
            NotifyComputedState();
    }

    private void ApplyValues(IReadOnlyDictionary<string, string?> values)
    {
        _isApplyingState = true;
        try
        {
            Zombies = GetValue(values, ".sandbox.zombies");
            Distribution = GetValue(values, ".sandbox.distribution");
            DayLength = GetValue(values, ".sandbox.day-length");
            StartYear = GetValue(values, ".sandbox.start-year");
            StartMonth = GetValue(values, ".sandbox.start-month");
            StartDay = GetValue(values, ".sandbox.start-day");
            StartTime = GetValue(values, ".sandbox.start-time");
            WaterShutModifier = GetValue(values, ".sandbox.water-shut-modifier");
            ElectricityShutModifier = GetValue(values, ".sandbox.electricity-shut-modifier");
            FoodLoot = GetValue(values, ".sandbox.food-loot");
            WeaponLoot = GetValue(values, ".sandbox.weapon-loot");
            OtherLoot = GetValue(values, ".sandbox.other-loot");
            Temperature = GetValue(values, ".sandbox.temperature");
            Rain = GetValue(values, ".sandbox.rain");
            StarterKit = bool.TryParse(GetValue(values, ".sandbox.starter-kit"), out var starterKit) && starterKit;
            Nutrition = bool.TryParse(GetValue(values, ".sandbox.nutrition"), out var nutrition) && nutrition;
        }
        finally
        {
            _isApplyingState = false;
        }
    }

    private IReadOnlyDictionary<string, string?> BuildValues()
    {
        var prefix = SelectedProfile?.Branch.Contains("42", StringComparison.Ordinal) == true ? "b42" : "b41";
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [$"{prefix}.sandbox.zombies"] = Zombies,
            [$"{prefix}.sandbox.distribution"] = Distribution,
            [$"{prefix}.sandbox.day-length"] = DayLength,
            [$"{prefix}.sandbox.start-year"] = StartYear,
            [$"{prefix}.sandbox.start-month"] = StartMonth,
            [$"{prefix}.sandbox.start-day"] = StartDay,
            [$"{prefix}.sandbox.start-time"] = StartTime,
            [$"{prefix}.sandbox.water-shut-modifier"] = WaterShutModifier,
            [$"{prefix}.sandbox.electricity-shut-modifier"] = ElectricityShutModifier,
            [$"{prefix}.sandbox.food-loot"] = FoodLoot,
            [$"{prefix}.sandbox.weapon-loot"] = WeaponLoot,
            [$"{prefix}.sandbox.other-loot"] = OtherLoot,
            [$"{prefix}.sandbox.temperature"] = Temperature,
            [$"{prefix}.sandbox.rain"] = Rain,
            [$"{prefix}.sandbox.starter-kit"] = StarterKit.ToString(),
            [$"{prefix}.sandbox.nutrition"] = Nutrition.ToString(),
        };
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
    }

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string suffix)
    {
        var key = values.Keys.FirstOrDefault(candidate => candidate.EndsWith(suffix, StringComparison.Ordinal));
        return key is null ? string.Empty : values[key] ?? string.Empty;
    }

    private void Reset()
    {
        _catalog = null;
        _sourceSha256 = null;
        CatalogSummary = "No structured catalog loaded.";
        RequiresAdvancedFilesFallback = false;
        FallbackReason = string.Empty;
        CanEdit = false;
        FieldErrors.Clear();
        OnPropertyChanged(nameof(HasFieldErrors));

        _isApplyingState = true;
        try
        {
            Zombies = string.Empty;
            Distribution = string.Empty;
            DayLength = string.Empty;
            StartYear = string.Empty;
            StartMonth = string.Empty;
            StartDay = string.Empty;
            StartTime = string.Empty;
            WaterShutModifier = string.Empty;
            ElectricityShutModifier = string.Empty;
            FoodLoot = string.Empty;
            WeaponLoot = string.Empty;
            OtherLoot = string.Empty;
            Temperature = string.Empty;
            Rain = string.Empty;
            StarterKit = false;
            Nutrition = false;
        }
        finally
        {
            _isApplyingState = false;
        }

        MarkClean("Sandbox settings are in sync.");
        NotifyComputedState();
    }

    partial void OnZombiesChanged(string value) => NotifyFieldEdited();
    partial void OnDistributionChanged(string value) => NotifyFieldEdited();
    partial void OnDayLengthChanged(string value) => NotifyFieldEdited();
    partial void OnStartYearChanged(string value) => NotifyFieldEdited();
    partial void OnStartMonthChanged(string value) => NotifyFieldEdited();
    partial void OnStartDayChanged(string value) => NotifyFieldEdited();
    partial void OnStartTimeChanged(string value) => NotifyFieldEdited();
    partial void OnWaterShutModifierChanged(string value) => NotifyFieldEdited();
    partial void OnElectricityShutModifierChanged(string value) => NotifyFieldEdited();
    partial void OnFoodLootChanged(string value) => NotifyFieldEdited();
    partial void OnWeaponLootChanged(string value) => NotifyFieldEdited();
    partial void OnOtherLootChanged(string value) => NotifyFieldEdited();
    partial void OnTemperatureChanged(string value) => NotifyFieldEdited();
    partial void OnRainChanged(string value) => NotifyFieldEdited();
    partial void OnStarterKitChanged(bool value) => NotifyFieldEdited();
    partial void OnNutritionChanged(bool value) => NotifyFieldEdited();

    private void NotifyFieldEdited()
    {
        if (_isApplyingState || !CanEdit)
        {
            return;
        }

        MarkDirty("Unsaved changes in Sandbox.");
        LoadStatus = "Sandbox settings changed locally. Save a draft or apply them to SandboxVars.lua.";
        NotifyComputedState();
    }

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(PageSummary));
        OnPropertyChanged(nameof(ProfileDisplayName));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(WorkspaceSummary));
        OnPropertyChanged(nameof(ActionSummary));
        OnPropertyChanged(nameof(HasFieldErrors));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(RequiresAdvancedFilesFallback));
    }
}
