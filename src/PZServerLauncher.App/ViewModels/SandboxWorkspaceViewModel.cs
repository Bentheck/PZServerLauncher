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
            ["World setup", "Zombie population", "Zombie lore", "Utilities", "Loot and climate", "Survival systems", "World events", "Survivor boosts", "Cleanup and wear", "Player experience"])
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
        : $"{SelectedProfile.DisplayName} is ready for branch-aware gameplay, world, and survival-system tuning.";

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
    private string populationMultiplier = string.Empty;

    [ObservableProperty]
    private string populationStartMultiplier = string.Empty;

    [ObservableProperty]
    private string populationPeakMultiplier = string.Empty;

    [ObservableProperty]
    private string populationPeakDay = string.Empty;

    [ObservableProperty]
    private string respawnHours = string.Empty;

    [ObservableProperty]
    private string respawnUnseenHours = string.Empty;

    [ObservableProperty]
    private string respawnMultiplier = string.Empty;

    [ObservableProperty]
    private string redistributeHours = string.Empty;

    [ObservableProperty]
    private string followSoundDistance = string.Empty;

    [ObservableProperty]
    private string rallyGroupSize = string.Empty;

    [ObservableProperty]
    private string rallyTravelDistance = string.Empty;

    [ObservableProperty]
    private string rallyGroupSeparation = string.Empty;

    [ObservableProperty]
    private string rallyGroupRadius = string.Empty;

    [ObservableProperty]
    private string zombieLoreSpeed = string.Empty;

    [ObservableProperty]
    private string zombieLoreStrength = string.Empty;

    [ObservableProperty]
    private string zombieLoreToughness = string.Empty;

    [ObservableProperty]
    private string zombieLoreTransmission = string.Empty;

    [ObservableProperty]
    private string zombieLoreMortality = string.Empty;

    [ObservableProperty]
    private string zombieLoreReanimate = string.Empty;

    [ObservableProperty]
    private string zombieLoreCognition = string.Empty;

    [ObservableProperty]
    private string zombieLoreMemory = string.Empty;

    [ObservableProperty]
    private string zombieLoreSight = string.Empty;

    [ObservableProperty]
    private string zombieLoreHearing = string.Empty;

    [ObservableProperty]
    private bool zombieLoreTriggerHouseAlarm;

    [ObservableProperty]
    private bool zombieLoreThumpNoChasing;

    [ObservableProperty]
    private string waterShutModifier = string.Empty;

    [ObservableProperty]
    private string electricityShutModifier = string.Empty;

    [ObservableProperty]
    private string erosionSpeed = string.Empty;

    [ObservableProperty]
    private string lootRespawn = string.Empty;

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
    private string alarm = string.Empty;

    [ObservableProperty]
    private string lockedHouses = string.Empty;

    [ObservableProperty]
    private string farming = string.Empty;

    [ObservableProperty]
    private string statsDecrease = string.Empty;

    [ObservableProperty]
    private string natureAbundance = string.Empty;

    [ObservableProperty]
    private string foodRotSpeed = string.Empty;

    [ObservableProperty]
    private string fridgeFactor = string.Empty;

    [ObservableProperty]
    private string plantResilience = string.Empty;

    [ObservableProperty]
    private string plantAbundance = string.Empty;

    [ObservableProperty]
    private string endRegen = string.Empty;

    [ObservableProperty]
    private string helicopter = string.Empty;

    [ObservableProperty]
    private string metaEvent = string.Empty;

    [ObservableProperty]
    private string sleepingEvent = string.Empty;

    [ObservableProperty]
    private string generatorSpawning = string.Empty;

    [ObservableProperty]
    private string characterFreePoints = string.Empty;

    [ObservableProperty]
    private string constructionBonusPoints = string.Empty;

    [ObservableProperty]
    private bool multiHit;

    [ObservableProperty]
    private bool allowExteriorGenerator;

    [ObservableProperty]
    private bool fireSpread;

    [ObservableProperty]
    private string hoursForCorpseRemoval = string.Empty;

    [ObservableProperty]
    private string decayingCorpseHealthImpact = string.Empty;

    [ObservableProperty]
    private string bloodLevel = string.Empty;

    [ObservableProperty]
    private string clothingDegradation = string.Empty;

    [ObservableProperty]
    private bool starterKit;

    [ObservableProperty]
    private bool nutrition;

    [ObservableProperty]
    private bool enableSnowOnGround;

    [ObservableProperty]
    private bool enableVehicles;

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
            PopulationMultiplier = GetValue(values, ".sandbox.population-multiplier");
            PopulationStartMultiplier = GetValue(values, ".sandbox.population-start-multiplier");
            PopulationPeakMultiplier = GetValue(values, ".sandbox.population-peak-multiplier");
            PopulationPeakDay = GetValue(values, ".sandbox.population-peak-day");
            RespawnHours = GetValue(values, ".sandbox.respawn-hours");
            RespawnUnseenHours = GetValue(values, ".sandbox.respawn-unseen-hours");
            RespawnMultiplier = GetValue(values, ".sandbox.respawn-multiplier");
            RedistributeHours = GetValue(values, ".sandbox.redistribute-hours");
            FollowSoundDistance = GetValue(values, ".sandbox.follow-sound-distance");
            RallyGroupSize = GetValue(values, ".sandbox.rally-group-size");
            RallyTravelDistance = GetValue(values, ".sandbox.rally-travel-distance");
            RallyGroupSeparation = GetValue(values, ".sandbox.rally-group-separation");
            RallyGroupRadius = GetValue(values, ".sandbox.rally-group-radius");
            ZombieLoreSpeed = GetValue(values, ".sandbox.zombie-lore-speed");
            ZombieLoreStrength = GetValue(values, ".sandbox.zombie-lore-strength");
            ZombieLoreToughness = GetValue(values, ".sandbox.zombie-lore-toughness");
            ZombieLoreTransmission = GetValue(values, ".sandbox.zombie-lore-transmission");
            ZombieLoreMortality = GetValue(values, ".sandbox.zombie-lore-mortality");
            ZombieLoreReanimate = GetValue(values, ".sandbox.zombie-lore-reanimate");
            ZombieLoreCognition = GetValue(values, ".sandbox.zombie-lore-cognition");
            ZombieLoreMemory = GetValue(values, ".sandbox.zombie-lore-memory");
            ZombieLoreSight = GetValue(values, ".sandbox.zombie-lore-sight");
            ZombieLoreHearing = GetValue(values, ".sandbox.zombie-lore-hearing");
            ZombieLoreTriggerHouseAlarm = bool.TryParse(GetValue(values, ".sandbox.zombie-lore-trigger-house-alarm"), out var zombieLoreTriggerHouseAlarm) && zombieLoreTriggerHouseAlarm;
            ZombieLoreThumpNoChasing = bool.TryParse(GetValue(values, ".sandbox.zombie-lore-thump-no-chasing"), out var zombieLoreThumpNoChasing) && zombieLoreThumpNoChasing;
            WaterShutModifier = GetValue(values, ".sandbox.water-shut-modifier");
            ElectricityShutModifier = GetValue(values, ".sandbox.electricity-shut-modifier");
            ErosionSpeed = GetValue(values, ".sandbox.erosion-speed");
            LootRespawn = GetValue(values, ".sandbox.loot-respawn");
            FoodLoot = GetValue(values, ".sandbox.food-loot");
            WeaponLoot = GetValue(values, ".sandbox.weapon-loot");
            OtherLoot = GetValue(values, ".sandbox.other-loot");
            Temperature = GetValue(values, ".sandbox.temperature");
            Rain = GetValue(values, ".sandbox.rain");
            Alarm = GetValue(values, ".sandbox.alarm");
            LockedHouses = GetValue(values, ".sandbox.locked-houses");
            Farming = GetValue(values, ".sandbox.farming");
            StatsDecrease = GetValue(values, ".sandbox.stats-decrease");
            NatureAbundance = GetValue(values, ".sandbox.nature-abundance");
            FoodRotSpeed = GetValue(values, ".sandbox.food-rot-speed");
            FridgeFactor = GetValue(values, ".sandbox.fridge-factor");
            PlantResilience = GetValue(values, ".sandbox.plant-resilience");
            PlantAbundance = GetValue(values, ".sandbox.plant-abundance");
            EndRegen = GetValue(values, ".sandbox.end-regen");
            Helicopter = GetValue(values, ".sandbox.helicopter");
            MetaEvent = GetValue(values, ".sandbox.meta-event");
            SleepingEvent = GetValue(values, ".sandbox.sleeping-event");
            GeneratorSpawning = GetValue(values, ".sandbox.generator-spawning");
            CharacterFreePoints = GetValue(values, ".sandbox.character-free-points");
            ConstructionBonusPoints = GetValue(values, ".sandbox.construction-bonus-points");
            MultiHit = bool.TryParse(GetValue(values, ".sandbox.multi-hit"), out var multiHit) && multiHit;
            AllowExteriorGenerator = bool.TryParse(GetValue(values, ".sandbox.allow-exterior-generator"), out var allowExteriorGenerator) && allowExteriorGenerator;
            FireSpread = bool.TryParse(GetValue(values, ".sandbox.fire-spread"), out var fireSpread) && fireSpread;
            HoursForCorpseRemoval = GetValue(values, ".sandbox.hours-for-corpse-removal");
            DecayingCorpseHealthImpact = GetValue(values, ".sandbox.decaying-corpse-health-impact");
            BloodLevel = GetValue(values, ".sandbox.blood-level");
            ClothingDegradation = GetValue(values, ".sandbox.clothing-degradation");
            StarterKit = bool.TryParse(GetValue(values, ".sandbox.starter-kit"), out var starterKit) && starterKit;
            Nutrition = bool.TryParse(GetValue(values, ".sandbox.nutrition"), out var nutrition) && nutrition;
            EnableSnowOnGround = bool.TryParse(GetValue(values, ".sandbox.enable-snow-on-ground"), out var enableSnowOnGround) && enableSnowOnGround;
            EnableVehicles = bool.TryParse(GetValue(values, ".sandbox.enable-vehicles"), out var enableVehicles) && enableVehicles;
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
            [$"{prefix}.sandbox.population-multiplier"] = PopulationMultiplier,
            [$"{prefix}.sandbox.population-start-multiplier"] = PopulationStartMultiplier,
            [$"{prefix}.sandbox.population-peak-multiplier"] = PopulationPeakMultiplier,
            [$"{prefix}.sandbox.population-peak-day"] = PopulationPeakDay,
            [$"{prefix}.sandbox.respawn-hours"] = RespawnHours,
            [$"{prefix}.sandbox.respawn-unseen-hours"] = RespawnUnseenHours,
            [$"{prefix}.sandbox.respawn-multiplier"] = RespawnMultiplier,
            [$"{prefix}.sandbox.redistribute-hours"] = RedistributeHours,
            [$"{prefix}.sandbox.follow-sound-distance"] = FollowSoundDistance,
            [$"{prefix}.sandbox.rally-group-size"] = RallyGroupSize,
            [$"{prefix}.sandbox.rally-travel-distance"] = RallyTravelDistance,
            [$"{prefix}.sandbox.rally-group-separation"] = RallyGroupSeparation,
            [$"{prefix}.sandbox.rally-group-radius"] = RallyGroupRadius,
            [$"{prefix}.sandbox.zombie-lore-speed"] = ZombieLoreSpeed,
            [$"{prefix}.sandbox.zombie-lore-strength"] = ZombieLoreStrength,
            [$"{prefix}.sandbox.zombie-lore-toughness"] = ZombieLoreToughness,
            [$"{prefix}.sandbox.zombie-lore-transmission"] = ZombieLoreTransmission,
            [$"{prefix}.sandbox.zombie-lore-mortality"] = ZombieLoreMortality,
            [$"{prefix}.sandbox.zombie-lore-reanimate"] = ZombieLoreReanimate,
            [$"{prefix}.sandbox.zombie-lore-cognition"] = ZombieLoreCognition,
            [$"{prefix}.sandbox.zombie-lore-memory"] = ZombieLoreMemory,
            [$"{prefix}.sandbox.zombie-lore-sight"] = ZombieLoreSight,
            [$"{prefix}.sandbox.zombie-lore-hearing"] = ZombieLoreHearing,
            [$"{prefix}.sandbox.zombie-lore-trigger-house-alarm"] = ZombieLoreTriggerHouseAlarm.ToString(),
            [$"{prefix}.sandbox.zombie-lore-thump-no-chasing"] = ZombieLoreThumpNoChasing.ToString(),
            [$"{prefix}.sandbox.water-shut-modifier"] = WaterShutModifier,
            [$"{prefix}.sandbox.electricity-shut-modifier"] = ElectricityShutModifier,
            [$"{prefix}.sandbox.erosion-speed"] = ErosionSpeed,
            [$"{prefix}.sandbox.loot-respawn"] = LootRespawn,
            [$"{prefix}.sandbox.food-loot"] = FoodLoot,
            [$"{prefix}.sandbox.weapon-loot"] = WeaponLoot,
            [$"{prefix}.sandbox.other-loot"] = OtherLoot,
            [$"{prefix}.sandbox.temperature"] = Temperature,
            [$"{prefix}.sandbox.rain"] = Rain,
            [$"{prefix}.sandbox.alarm"] = Alarm,
            [$"{prefix}.sandbox.locked-houses"] = LockedHouses,
            [$"{prefix}.sandbox.farming"] = Farming,
            [$"{prefix}.sandbox.stats-decrease"] = StatsDecrease,
            [$"{prefix}.sandbox.nature-abundance"] = NatureAbundance,
            [$"{prefix}.sandbox.food-rot-speed"] = FoodRotSpeed,
            [$"{prefix}.sandbox.fridge-factor"] = FridgeFactor,
            [$"{prefix}.sandbox.plant-resilience"] = PlantResilience,
            [$"{prefix}.sandbox.plant-abundance"] = PlantAbundance,
            [$"{prefix}.sandbox.end-regen"] = EndRegen,
            [$"{prefix}.sandbox.helicopter"] = Helicopter,
            [$"{prefix}.sandbox.meta-event"] = MetaEvent,
            [$"{prefix}.sandbox.sleeping-event"] = SleepingEvent,
            [$"{prefix}.sandbox.generator-spawning"] = GeneratorSpawning,
            [$"{prefix}.sandbox.character-free-points"] = CharacterFreePoints,
            [$"{prefix}.sandbox.construction-bonus-points"] = ConstructionBonusPoints,
            [$"{prefix}.sandbox.multi-hit"] = MultiHit.ToString(),
            [$"{prefix}.sandbox.allow-exterior-generator"] = AllowExteriorGenerator.ToString(),
            [$"{prefix}.sandbox.fire-spread"] = FireSpread.ToString(),
            [$"{prefix}.sandbox.hours-for-corpse-removal"] = HoursForCorpseRemoval,
            [$"{prefix}.sandbox.decaying-corpse-health-impact"] = DecayingCorpseHealthImpact,
            [$"{prefix}.sandbox.blood-level"] = BloodLevel,
            [$"{prefix}.sandbox.clothing-degradation"] = ClothingDegradation,
            [$"{prefix}.sandbox.starter-kit"] = StarterKit.ToString(),
            [$"{prefix}.sandbox.nutrition"] = Nutrition.ToString(),
            [$"{prefix}.sandbox.enable-snow-on-ground"] = EnableSnowOnGround.ToString(),
            [$"{prefix}.sandbox.enable-vehicles"] = EnableVehicles.ToString(),
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
            PopulationMultiplier = string.Empty;
            PopulationStartMultiplier = string.Empty;
            PopulationPeakMultiplier = string.Empty;
            PopulationPeakDay = string.Empty;
            RespawnHours = string.Empty;
            RespawnUnseenHours = string.Empty;
            RespawnMultiplier = string.Empty;
            RedistributeHours = string.Empty;
            FollowSoundDistance = string.Empty;
            RallyGroupSize = string.Empty;
            RallyTravelDistance = string.Empty;
            RallyGroupSeparation = string.Empty;
            RallyGroupRadius = string.Empty;
            ZombieLoreSpeed = string.Empty;
            ZombieLoreStrength = string.Empty;
            ZombieLoreToughness = string.Empty;
            ZombieLoreTransmission = string.Empty;
            ZombieLoreMortality = string.Empty;
            ZombieLoreReanimate = string.Empty;
            ZombieLoreCognition = string.Empty;
            ZombieLoreMemory = string.Empty;
            ZombieLoreSight = string.Empty;
            ZombieLoreHearing = string.Empty;
            ZombieLoreTriggerHouseAlarm = false;
            ZombieLoreThumpNoChasing = false;
            WaterShutModifier = string.Empty;
            ElectricityShutModifier = string.Empty;
            ErosionSpeed = string.Empty;
            LootRespawn = string.Empty;
            FoodLoot = string.Empty;
            WeaponLoot = string.Empty;
            OtherLoot = string.Empty;
            Temperature = string.Empty;
            Rain = string.Empty;
            Alarm = string.Empty;
            LockedHouses = string.Empty;
            Farming = string.Empty;
            StatsDecrease = string.Empty;
            NatureAbundance = string.Empty;
            FoodRotSpeed = string.Empty;
            FridgeFactor = string.Empty;
            PlantResilience = string.Empty;
            PlantAbundance = string.Empty;
            EndRegen = string.Empty;
            Helicopter = string.Empty;
            MetaEvent = string.Empty;
            SleepingEvent = string.Empty;
            GeneratorSpawning = string.Empty;
            CharacterFreePoints = string.Empty;
            ConstructionBonusPoints = string.Empty;
            MultiHit = false;
            AllowExteriorGenerator = false;
            FireSpread = false;
            HoursForCorpseRemoval = string.Empty;
            DecayingCorpseHealthImpact = string.Empty;
            BloodLevel = string.Empty;
            ClothingDegradation = string.Empty;
            StarterKit = false;
            Nutrition = false;
            EnableSnowOnGround = false;
            EnableVehicles = false;
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
    partial void OnPopulationMultiplierChanged(string value) => NotifyFieldEdited();
    partial void OnPopulationStartMultiplierChanged(string value) => NotifyFieldEdited();
    partial void OnPopulationPeakMultiplierChanged(string value) => NotifyFieldEdited();
    partial void OnPopulationPeakDayChanged(string value) => NotifyFieldEdited();
    partial void OnRespawnHoursChanged(string value) => NotifyFieldEdited();
    partial void OnRespawnUnseenHoursChanged(string value) => NotifyFieldEdited();
    partial void OnRespawnMultiplierChanged(string value) => NotifyFieldEdited();
    partial void OnRedistributeHoursChanged(string value) => NotifyFieldEdited();
    partial void OnFollowSoundDistanceChanged(string value) => NotifyFieldEdited();
    partial void OnRallyGroupSizeChanged(string value) => NotifyFieldEdited();
    partial void OnRallyTravelDistanceChanged(string value) => NotifyFieldEdited();
    partial void OnRallyGroupSeparationChanged(string value) => NotifyFieldEdited();
    partial void OnRallyGroupRadiusChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreSpeedChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreStrengthChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreToughnessChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreTransmissionChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreMortalityChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreReanimateChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreCognitionChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreMemoryChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreSightChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreHearingChanged(string value) => NotifyFieldEdited();
    partial void OnZombieLoreTriggerHouseAlarmChanged(bool value) => NotifyFieldEdited();
    partial void OnZombieLoreThumpNoChasingChanged(bool value) => NotifyFieldEdited();
    partial void OnWaterShutModifierChanged(string value) => NotifyFieldEdited();
    partial void OnElectricityShutModifierChanged(string value) => NotifyFieldEdited();
    partial void OnErosionSpeedChanged(string value) => NotifyFieldEdited();
    partial void OnLootRespawnChanged(string value) => NotifyFieldEdited();
    partial void OnFoodLootChanged(string value) => NotifyFieldEdited();
    partial void OnWeaponLootChanged(string value) => NotifyFieldEdited();
    partial void OnOtherLootChanged(string value) => NotifyFieldEdited();
    partial void OnTemperatureChanged(string value) => NotifyFieldEdited();
    partial void OnRainChanged(string value) => NotifyFieldEdited();
    partial void OnAlarmChanged(string value) => NotifyFieldEdited();
    partial void OnLockedHousesChanged(string value) => NotifyFieldEdited();
    partial void OnFarmingChanged(string value) => NotifyFieldEdited();
    partial void OnStatsDecreaseChanged(string value) => NotifyFieldEdited();
    partial void OnNatureAbundanceChanged(string value) => NotifyFieldEdited();
    partial void OnFoodRotSpeedChanged(string value) => NotifyFieldEdited();
    partial void OnFridgeFactorChanged(string value) => NotifyFieldEdited();
    partial void OnPlantResilienceChanged(string value) => NotifyFieldEdited();
    partial void OnPlantAbundanceChanged(string value) => NotifyFieldEdited();
    partial void OnEndRegenChanged(string value) => NotifyFieldEdited();
    partial void OnHelicopterChanged(string value) => NotifyFieldEdited();
    partial void OnMetaEventChanged(string value) => NotifyFieldEdited();
    partial void OnSleepingEventChanged(string value) => NotifyFieldEdited();
    partial void OnGeneratorSpawningChanged(string value) => NotifyFieldEdited();
    partial void OnCharacterFreePointsChanged(string value) => NotifyFieldEdited();
    partial void OnConstructionBonusPointsChanged(string value) => NotifyFieldEdited();
    partial void OnMultiHitChanged(bool value) => NotifyFieldEdited();
    partial void OnAllowExteriorGeneratorChanged(bool value) => NotifyFieldEdited();
    partial void OnFireSpreadChanged(bool value) => NotifyFieldEdited();
    partial void OnHoursForCorpseRemovalChanged(string value) => NotifyFieldEdited();
    partial void OnDecayingCorpseHealthImpactChanged(string value) => NotifyFieldEdited();
    partial void OnBloodLevelChanged(string value) => NotifyFieldEdited();
    partial void OnClothingDegradationChanged(string value) => NotifyFieldEdited();
    partial void OnStarterKitChanged(bool value) => NotifyFieldEdited();
    partial void OnNutritionChanged(bool value) => NotifyFieldEdited();
    partial void OnEnableSnowOnGroundChanged(bool value) => NotifyFieldEdited();
    partial void OnEnableVehiclesChanged(bool value) => NotifyFieldEdited();

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
