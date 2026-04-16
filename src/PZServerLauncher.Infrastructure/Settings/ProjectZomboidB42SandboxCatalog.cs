using System.Globalization;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Infrastructure.Settings;

internal static class ProjectZomboidB42SandboxCatalog
{
    private const string DefaultWorldItemRemovalList = "Base.Hat, Base.Glasses, Base.Maggots, Base.Slug, Base.Slug2, Base.Snail, Base.Worm, Base.Dung_Mouse, Base.Dung_Rat";

    public static StructuredPageDefinition BuildPage(string branchPrefix)
    {
        return new StructuredPageDefinition(
            $"{branchPrefix}.sandbox",
            "Sandbox",
            [
            BuildTimeSection(branchPrefix),
            BuildZombieBasicsSection(branchPrefix),
            BuildZombieLoreSection(branchPrefix),
            BuildZombieAdvancedSection(branchPrefix),
            BuildLootWorldSection(branchPrefix),
            BuildLootRaritySection(branchPrefix),
            BuildWorldCoreSection(branchPrefix),
            BuildWorldBasementsSection(branchPrefix),
            BuildNatureSection(branchPrefix),
            BuildMetaCoreSection(branchPrefix),
            BuildMetaMapSection(branchPrefix),
            BuildCharacterCoreSection(branchPrefix),
            BuildCharacterXpSection(branchPrefix),
            BuildVehiclesSection(branchPrefix),
            BuildLivestockSection(branchPrefix),
            ]);
    }

    private static StructuredSectionDefinition BuildTimeSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "time.setup",
            "Time",
            "Time",
            1,
            "Start timeline and world pacing.",
            new[]
            {
                ChoiceField(branchPrefix, "day-length", "Day Length (in real time)", "DayLength", "1 Hour, 30 Minutes", DayLengthOptions()),
                ChoiceField(branchPrefix, "time-since-apo", "Months since the Apocalypse", "TimeSinceApo", "0", TimeSinceApocalypseOptions()),
                ChoiceField(branchPrefix, "start-month", "Start Month", "StartMonth", "July", MonthOptions()),
                IntField(branchPrefix, "start-day", "Start Day", "StartDay", "9"),
                ChoiceField(branchPrefix, "start-time", "Start Hour", "StartTime", "9 AM", StartTimeOptions()),
            });

    private static StructuredSectionDefinition BuildZombieBasicsSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "zombie.basics",
            "Zombie",
            "Zombie",
            2,
            "Population, distribution, and baseline world pressure.",
            new[]
            {
                ChoiceField(branchPrefix, "zombies", "Zombie Count", "Zombies", "Normal", ZombieCountOptions()),
                ChoiceField(branchPrefix, "distribution", "Zombie Distribution", "Distribution", "Urban Focused", NumberedOptions("Urban Focused", "Uniform")),
                BoolField(branchPrefix, "voronoi-noise", "Voronoi Noise", "ZombieVoronoiNoise", "true"),
                ChoiceField(branchPrefix, "zombie-respawn", "Zombie Respawn", "ZombieRespawn", "None", Options(
                    ("1", "High"),
                    ("2", "Normal"),
                    ("3", "Low"),
                    ("4", "None"))),
                BoolField(branchPrefix, "zombie-migration", "Zombie Migration", "ZombieMigrate", "true"),
            });

    private static StructuredSectionDefinition BuildZombieLoreSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "zombie.lore",
            "Zombie Lore",
            "Zombie",
            2,
            "Behavior, infection, and special zombie rules.",
            new[]
            {
                ChoiceField(branchPrefix, "zombie-lore-speed", "Speed", "ZombieLore.Speed", "Random", Options(
                    ("1", "Sprinters"),
                    ("2", "Fast Shamblers"),
                    ("3", "Shamblers"),
                    ("4", "Random"))),
                IntField(branchPrefix, "random-sprinter-amount", "Random Sprinter Amount (%)", "ZombieLore.SprinterPercentage", "0"),
                ChoiceField(branchPrefix, "zombie-lore-strength", "Strength", "ZombieLore.Strength", "Normal", Options(
                    ("1", "Superhuman"),
                    ("2", "Normal"),
                    ("3", "Weak"),
                    ("4", "Random"))),
                ChoiceField(branchPrefix, "zombie-lore-toughness", "Toughness", "ZombieLore.Toughness", "Random", Options(
                    ("1", "Tough"),
                    ("2", "Normal"),
                    ("3", "Fragile"),
                    ("4", "Random"))),
                ChoiceField(branchPrefix, "zombie-lore-transmission", "Transmission", "ZombieLore.Transmission", "Blood and Saliva", Options(
                    ("1", "Blood and Saliva"),
                    ("2", "Saliva Only"),
                    ("3", "Everyone's Infected"),
                    ("4", "None"))),
                ChoiceField(branchPrefix, "zombie-lore-mortality", "Infection Mortality", "ZombieLore.Mortality", "2-3 Days", Options(
                    ("1", "Instant"),
                    ("2", "0-30 Seconds"),
                    ("3", "0-1 Minutes"),
                    ("4", "0-12 Hours"),
                    ("5", "2-3 Days"),
                    ("6", "1-2 Weeks"),
                    ("7", "Never"))),
                ChoiceField(branchPrefix, "zombie-lore-reanimate", "Reanimate Time", "ZombieLore.Reanimate", "0-1 Minutes", Options(
                    ("1", "Instant"),
                    ("2", "0-30 Seconds"),
                    ("3", "0-1 Minutes"),
                    ("4", "0-12 Hours"),
                    ("5", "2-3 Days"),
                    ("6", "1-2 Weeks"))),
                ChoiceField(branchPrefix, "zombie-lore-cognition", "Cognition", "ZombieLore.Cognition", "Basic Navigation", Options(
                    ("1", "Navigate and Use Doors"),
                    ("2", "Navigate"),
                    ("3", "Basic Navigation"),
                    ("4", "Random"))),
                IntField(branchPrefix, "random-door-opening-amount", "Random Door Opening Amount (%)", "ZombieLore.DoorOpeningPercentage", "0"),
                ChoiceField(branchPrefix, "crawl-under-vehicle", "Crawl Under Vehicle", "ZombieLore.CrawlUnderVehicle", "Often", CrawlUnderVehicleOptions()),
                ChoiceField(branchPrefix, "zombie-lore-memory", "Memory", "ZombieLore.Memory", "Normal", Options(
                    ("1", "Long"),
                    ("2", "Normal"),
                    ("3", "Short"),
                    ("4", "None"),
                    ("5", "Random"),
                    ("6", "Random between Normal and None"))),
                ChoiceField(branchPrefix, "zombie-lore-sight", "Sight", "ZombieLore.Sight", "Random between Normal and Poor", Options(
                    ("1", "Eagle"),
                    ("2", "Normal"),
                    ("3", "Poor"),
                    ("4", "Random"),
                    ("5", "Random between Normal and Poor"))),
                ChoiceField(branchPrefix, "zombie-lore-hearing", "Hearing", "ZombieLore.Hearing", "Random between Normal and Poor", Options(
                    ("1", "Pinpoint"),
                    ("2", "Normal"),
                    ("3", "Poor"),
                    ("4", "Random"),
                    ("5", "Random between Normal and Poor"))),
                BoolField(branchPrefix, "new-stealth-system", "New Stealth System", "ZombieLore.SpottedLogic", "true"),
                BoolField(branchPrefix, "environmental-attacks", "Environmental Attacks", "ZombieLore.ThumpNoChasing", "false"),
                BoolField(branchPrefix, "damage-construction", "Damage Construction", "ZombieLore.ThumpOnConstruction", "true"),
                ChoiceField(branchPrefix, "day-night-zombie-speed-effect", "Day/Night Zombie Speed Effect", "ZombieLore.ActiveOnly", "Both", Options(
                    ("1", "Both"),
                    ("2", "Night"),
                    ("3", "Day"))),
                BoolField(branchPrefix, "zombie-house-alarm-triggering", "Zombie House Alarm Triggering", "ZombieLore.TriggerHouseAlarm", "true"),
                BoolField(branchPrefix, "drag-down", "Drag Down", "ZombieLore.ZombiesDragDown", "true"),
                BoolField(branchPrefix, "crawlers-drag-down", "Crawlers Drag Down", "ZombieLore.ZombiesCrawlersDragDown", "false"),
                BoolField(branchPrefix, "zombie-lunge", "Zombie Lunge", "ZombieLore.ZombiesFenceLunge", "true"),
                ChoiceField(branchPrefix, "fake-dead-zombie-reanimation", "Fake Dead Zombie Reanimation", "ZombieLore.DisableFakeDead", "World Zombies", Options(
                    ("1", "World Zombies"),
                    ("2", "World and Combat Zombies"),
                    ("3", "Never"))),
                TextField(branchPrefix, "zombie-armor-factor", "Zombie Armor Factor", "ZombieLore.ZombiesArmorFactor", "2.0"),
                IntField(branchPrefix, "maximum-zombie-armor-defense", "Maximum Zombie Armor Defense", "ZombieLore.ZombiesMaxDefense", "85"),
                IntField(branchPrefix, "chance-of-attached-weapon", "Chance Of Attached Weapon", "ZombieLore.ChanceOfAttachedWeapon", "6"),
                TextField(branchPrefix, "zombie-fall-damage-multiplier", "Zombie Fall Damage Multiplier", "ZombieLore.ZombiesFallDamage", "1.0"),
                ChoiceField(branchPrefix, "player-spawn-area", "Player Spawn Area", "ZombieLore.PlayerSpawnZombieRemoval", "Inside the building and around it", Options(
                    ("1", "Inside the building and around it"),
                    ("2", "Inside the building"),
                    ("3", "Inside the room"),
                    ("4", "Zombies can spawn anywhere"))),
            });

    private static StructuredSectionDefinition BuildZombieAdvancedSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "zombie.advanced",
            "Advanced zombie settings",
            "Zombie",
            2,
            "Population curves and rally behavior.",
            new[]
            {
                ChoiceField(branchPrefix, "population-multiplier", "Population Multiplier", "ZombieConfig.PopulationMultiplier", "Normal", ZombiePopulationOptions()),
                ChoiceField(branchPrefix, "population-start-multiplier", "Population Start Multiplier", "ZombieConfig.PopulationStartMultiplier", "Normal", ZombiePopulationStartOptions()),
                ChoiceField(branchPrefix, "population-peak-multiplier", "Population Peak Multiplier", "ZombieConfig.PopulationPeakMultiplier", "High", ZombiePopulationStartOptions()),
                IntField(branchPrefix, "population-peak-day", "Population Peak Day", "ZombieConfig.PopulationPeakDay", "28"),
                TextField(branchPrefix, "respawn-hours", "Respawn Hours", "ZombieConfig.RespawnHours", "0.0"),
                TextField(branchPrefix, "respawn-unseen-hours", "Respawn Unseen Hours", "ZombieConfig.RespawnUnseenHours", "0.0"),
                TextField(branchPrefix, "respawn-multiplier", "Respawn Multiplier", "ZombieConfig.RespawnMultiplier", "0.0"),
                TextField(branchPrefix, "redistribute-hours", "Redistribute Hours", "ZombieConfig.RedistributeHours", "12.0"),
                IntField(branchPrefix, "follow-sound-distance", "Follow Sound Distance", "ZombieConfig.FollowSoundDistance", "100"),
                IntField(branchPrefix, "rally-group-size", "Rally Group Size", "ZombieConfig.RallyGroupSize", "20"),
                IntField(branchPrefix, "rally-group-size-variance", "Rally Group Size Variance", "ZombieConfig.RallyGroupSizeVariance", "50"),
                IntField(branchPrefix, "rally-travel-distance", "Rally Travel Distance", "ZombieConfig.RallyTravelDistance", "20"),
                IntField(branchPrefix, "rally-group-separation", "Rally Group Separation", "ZombieConfig.RallyGroupSeparation", "15"),
                IntField(branchPrefix, "rally-group-radius", "Rally Group Radius", "ZombieConfig.RallyGroupRadius", "3"),
                IntField(branchPrefix, "zombie-count-before-deletion", "Zombie count before deletion", "ZombieConfig.ZombiesCountBeforeDelete", "300"),
            });
    private static StructuredSectionDefinition BuildLootWorldSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "loot.world",
            "Loot",
            "Loot",
            3,
            "Respawn cadence and global world-loot pressure.",
            new[]
            {
                IntField(branchPrefix, "hours-for-loot-respawn", "Hours for Loot Respawn", "HoursForLootRespawn", "0"),
                IntField(branchPrefix, "loot-seen-prevent-hours", "Loot Seen Prevent Hours", "SeenHoursPreventLootRespawn", "0"),
                IntField(branchPrefix, "max-items-for-loot-respawn", "Max Items For Loot Respawn", "MaxItemsForLootRespawn", "5"),
                BoolField(branchPrefix, "construction-prevents-loot-respawn", "Construction Prevents Loot Respawn", "ConstructionPreventsLootRespawn", "true"),
                IntField(branchPrefix, "maximum-looted-building-chance", "Maximum Looted Building Chance", "MaximumLooted", "25"),
                IntField(branchPrefix, "days-until-max-looted-building-chance", "Days Until Max Looted Building Chance", "DaysUntilMaximumLooted", "90"),
                TextField(branchPrefix, "rural-building-looted-chance-multiplier", "Rural Building Looted Chance Multiplier", "RuralLooted", "0.5"),
                IntField(branchPrefix, "maximum-diminished-loot-percentage", "Maximum Diminished Loot Percentage", "MaximumDiminishedLoot", "20"),
                IntField(branchPrefix, "days-until-maximum-diminished-loot", "Days Until Maximum Diminished Loot", "DaysUntilMaximumDiminishedLoot", "3650"),
                IntField(branchPrefix, "maximum-looted-building-rooms", "Maximum Looted Building Rooms", "MaximumLootedBuildingRooms", "50"),
            });

    private static StructuredSectionDefinition BuildLootRaritySection(string branchPrefix) =>
        Section(
            branchPrefix,
            "loot.rarity",
            "Loot rarity",
            "Loot",
            3,
            "Category-specific loot values and cleanup toggles.",
            new[]
            {
                TextField(branchPrefix, "perishable-food-loot", "Perishable Food", "FoodLootNew", "0.8"),
                TextField(branchPrefix, "non-perishable-food-loot", "Non-Perishable Food", "CannedFoodLootNew", "0.6"),
                TextField(branchPrefix, "melee-weapons-loot", "Melee Weapons", "WeaponLootNew", "0.6"),
                TextField(branchPrefix, "ranged-weapons-loot", "Ranged Weapons", "RangedWeaponLootNew", "1.2"),
                TextField(branchPrefix, "ammo-loot", "Ammo", "AmmoLootNew", "0.6"),
                TextField(branchPrefix, "medical-loot", "Medical", "MedicalLootNew", "0.6"),
                TextField(branchPrefix, "survival-essentials-loot", "Survival Essentials", "SurvivalGearsLootNew", "0.6"),
                TextField(branchPrefix, "mechanics-loot", "Mechanics", "MechanicsLootNew", "0.6"),
                TextField(branchPrefix, "skill-books-loot", "Skill Books", "SkillBookLoot", "0.6"),
                TextField(branchPrefix, "recipe-resources-loot", "Recipe Resources", "RecipeResourceLoot", "0.6"),
                TextField(branchPrefix, "other-literature-loot", "Other Literature", "LiteratureLootNew", "0.6"),
                TextField(branchPrefix, "clothing-loot", "Clothing", "ClothingLootNew", "0.6"),
                TextField(branchPrefix, "bags-loot", "Bags", "ContainerLootNew", "0.6"),
                TextField(branchPrefix, "keys-loot", "Keys", "KeyLootNew", "0.4"),
                TextField(branchPrefix, "media-loot", "Media", "MediaLootNew", "0.6"),
                TextField(branchPrefix, "mementos-loot", "Mementos", "MementoLootNew", "0.6"),
                TextField(branchPrefix, "cooking-loot", "Cooking", "CookwareLootNew", "0.6"),
                TextField(branchPrefix, "material-loot", "Material", "MaterialLootNew", "0.6"),
                TextField(branchPrefix, "farming-loot", "Farming", "FarmingLootNew", "0.6"),
                TextField(branchPrefix, "tools-loot", "Tools", "ToolLootNew", "0.6"),
                TextField(branchPrefix, "other-loot", "Other", "OtherLootNew", "0.6"),
                ChoiceField(branchPrefix, "generators-loot", "Generators", "GeneratorSpawning", "Rare", GeneratorSpawningOptions()),
                TextField(branchPrefix, "loot-item-removal-list", "Loot Item Removal List", "LootItemRemovalList", string.Empty),
                BoolField(branchPrefix, "remove-unwanted-story-loot", "Remove Unwanted Story Loot", "RemoveStoryLoot", "false"),
                BoolField(branchPrefix, "remove-unwanted-zombie-loot", "Remove Unwanted Zombie Loot", "RemoveZombieLoot", "false"),
                TextField(branchPrefix, "rolls-multiplier", "Rolls Multiplier [!]", "RollsMultiplier", "1.0"),
                IntField(branchPrefix, "zombie-population-loot-effect", "Zombie Population Loot Effect", "ZombiePopLootEffect", "0"),
            });

    private static StructuredSectionDefinition BuildWorldCoreSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "world.core",
            "World",
            "World",
            4,
            "Utilities, alarms, generators, and decay systems.",
            new[]
            {
                IntField(branchPrefix, "water-shut-modifier", "Water Shutoff", "WaterShutModifier", "14"),
                IntField(branchPrefix, "electricity-shut-modifier", "Electricity Shutoff", "ElecShutModifier", "14"),
                ChoiceField(branchPrefix, "water-shut", "Water Shutoff", "WaterShut", "0 - 30 Days", WaterShutoffOptions()),
                ChoiceField(branchPrefix, "electricity-shut", "Electricity Shutoff", "ElecShut", "14 - 30 Days", ElectricityShutoffOptions()),
                ChoiceField(branchPrefix, "alarm-battery-decay", "Alarm Battery Decay", "AlarmDecay", "0 - 30 Days", AlarmDecayOptions()),
                ChoiceField(branchPrefix, "alarm", "House Alarms Frequency", "Alarm", "Sometimes", FrequencyOptions()),
                ChoiceField(branchPrefix, "locked-houses", "Locked Houses Frequency", "LockedHouses", "Very Often", FrequencyOptions()),
                BoolField(branchPrefix, "fire-spread", "Fire Spread", "FireSpread", "true"),
                BoolField(branchPrefix, "allow-exterior-generator", "Generator Working in Exterior", "AllowExteriorGenerator", "true"),
                IntField(branchPrefix, "generator-tile-range", "Generator tile range", "GeneratorTileRange", "20"),
                IntField(branchPrefix, "generator-vertical-range", "Generator vertical range", "GeneratorVerticalPowerRange", "3"),
                BoolField(branchPrefix, "infinite-gas-pumps", "Infinite Gas Pumps", "FuelStationGasInfinite", "false"),
                TextField(branchPrefix, "initial-minimum-gas-pump-amount", "Initial Minimum Gas Pump Amount", "FuelStationGasMin", "0.0"),
                TextField(branchPrefix, "initial-maximum-gas-pump-amount", "Initial Maximum Gas Pump Amount", "FuelStationGasMax", "0.8"),
                IntField(branchPrefix, "initial-gas-pump-empty-chance", "Initial Gas Pump Empty Chance", "FuelStationGasEmptyChance", "20"),
                TextField(branchPrefix, "light-bulb-lifespan", "Light Bulb Lifespan", "LightBulbLifespan", "2.0"),
                ChoiceField(branchPrefix, "food-spoilage", "Food Spoilage", "FoodRotSpeed", "Normal", Options(
                    ("1", "Very Fast"),
                    ("2", "Fast"),
                    ("3", "Normal"),
                    ("4", "Slow"),
                    ("5", "Very Slow"))),
                ChoiceField(branchPrefix, "refrigeration-effectiveness", "Refrigeration Effectiveness", "FridgeFactor", "Normal", Options(
                    ("1", "Very Low"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"),
                    ("6", "No decay"))),
                IntField(branchPrefix, "rotten-food-removal", "Rotten Food Removal", "DaysForRottenFoodRemoval", "-1"),
                TextField(branchPrefix, "world-item-removal-list", "World Item Removal List", "WorldItemRemovalList", DefaultWorldItemRemovalList),
                TextField(branchPrefix, "hours-for-world-item-removal", "Hours for Removal List", "HoursForWorldItemRemoval", "24.0"),
                BoolField(branchPrefix, "item-removal-list-whitelist-toggle", "Removal List as Whitelist", "ItemRemovalListBlacklistToggle", "false"),
            });

    private static StructuredSectionDefinition BuildWorldBasementsSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "world.basements",
            "Basements",
            "World",
            4,
            "Basement spawn and fire-fuel rules.",
            new[]
            {
                ChoiceField(branchPrefix, "basement-spawn-frequency", "Basement Spawn Frequency", "Basement.SpawnFrequency", "Sometimes", FrequencyWithAlwaysOptions()),
                IntField(branchPrefix, "maximum-fire-fuel-hours", "Maximum Fire Fuel Hours", "MaximumFireFuelHours", "8"),
            });

    private static StructuredSectionDefinition BuildNatureSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "nature.core",
            "Nature",
            "Nature",
            5,
            "Climate, farming, water, vermin, and resources.",
            new[]
            {
                ChoiceField(branchPrefix, "night-darkness", "Darkness during night", "NightDarkness", "Normal", Options(
                    ("1", "Pitch Black"),
                    ("2", "Dark"),
                    ("3", "Normal"),
                    ("4", "Bright"))),
                ChoiceField(branchPrefix, "temperature", "Temperature", "Temperature", "Normal", NumberedOptions("Very Cold", "Cold", "Normal", "Hot", "Very Hot")),
                ChoiceField(branchPrefix, "rain", "Rain", "Rain", "Normal", NumberedOptions("Very Dry", "Dry", "Normal", "Rainy", "Very Rainy")),
                ChoiceField(branchPrefix, "max-fog-intensity", "Maximum Fog Intensity", "MaxFogIntensity", "Normal", Options(
                    ("1", "Normal"),
                    ("2", "Moderate"),
                    ("3", "Low"),
                    ("4", "None"))),
                ChoiceField(branchPrefix, "max-rain-fx-intensity", "Maximum Rain FX Intensity", "MaxRainFxIntensity", "Normal", Options(
                    ("1", "Normal"),
                    ("2", "Moderate"),
                    ("3", "Low"))),
                ChoiceField(branchPrefix, "erosion-speed", "Erosion Speed", "ErosionSpeed", "Slow (200 Days)", Options(
                    ("1", "Very Fast (20 Days)"),
                    ("2", "Fast (50 Days)"),
                    ("3", "Normal (100 Days)"),
                    ("4", "Slow (200 Days)"),
                    ("5", "Very Slow (500 Days)"))),
                IntField(branchPrefix, "erosion-days", "Erosion Days", "ErosionDays", "0"),
                TextField(branchPrefix, "farming", "Farming Speed", "FarmingSpeedNew", "1.0"),
                ChoiceField(branchPrefix, "compost-time", "Compost Time", "CompostTime", "2 Weeks", Options(
                    ("1", "1 Week"),
                    ("2", "2 Weeks"),
                    ("3", "3 Weeks"),
                    ("4", "4 Weeks"),
                    ("5", "6 Weeks"),
                    ("6", "8 Weeks"),
                    ("7", "10 Weeks"),
                    ("8", "12 Weeks"))),
                ChoiceField(branchPrefix, "fishing-abundance", "Fishing Abundance", "FishAbundance", "Poor", NumberedOptions("Very Poor", "Poor", "Normal", "Abundant", "Very Abundant")),
                ChoiceField(branchPrefix, "nature-abundance", "Nature's Abundance", "NatureAbundance", "Normal", NumberedOptions("Very Poor", "Poor", "Normal", "Abundant", "Very Abundant")),
                ChoiceField(branchPrefix, "plant-resilience", "Plant Resilience", "PlantResilience", "Normal", Options(
                    ("1", "Very High"),
                    ("2", "High"),
                    ("3", "Normal"),
                    ("4", "Low"),
                    ("5", "Very Low"))),
                TextField(branchPrefix, "plant-abundance", "Farming Abundance", "FarmingAmountNew", "1.0"),
                BoolField(branchPrefix, "kill-crops-grown-inside", "Kill Crops Grown Inside", "KillInsideCrops", "true"),
                BoolField(branchPrefix, "plant-growing-seasons", "Plant Growing Seasons", "PlantGrowingSeasons", "true"),
                BoolField(branchPrefix, "farms-not-on-ground-level", "Farms not on Ground Level [!]", "PlaceDirtAboveground", "false"),
                BoolField(branchPrefix, "enable-snow-on-ground", "Snow on Ground", "EnableSnowOnGround", "true"),
                BoolField(branchPrefix, "enable-tainted-water-tooltip", "Enable 'Tainted Water' tooltip", "EnableTaintedWaterText", "true"),
                IntField(branchPrefix, "maximum-vermin-index", "Maximum Vermin Index", "MaximumRatIndex", "25"),
                IntField(branchPrefix, "days-until-maximum-vermin-index", "Days Until Maximum Vermin Index", "DaysUntilMaximumRatIndex", "90"),
                TextField(branchPrefix, "clay-chance-lake", "Clay chance - Lake", "ClayLakeChance", "0.05"),
                TextField(branchPrefix, "clay-chance-river", "Clay chance - River", "ClayRiverChance", "0.05"),
            });

    private static StructuredSectionDefinition BuildMetaCoreSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "meta.core",
            "Meta",
            "Meta",
            6,
            "Story events, corpse systems, blood, and fence damage.",
            new[]
            {
                ChoiceField(branchPrefix, "helicopter", "Helicopter", "Helicopter", "Once", Options(
                    ("1", "Never"),
                    ("2", "Once"),
                    ("3", "Sometimes"),
                    ("4", "Often"))),
                ChoiceField(branchPrefix, "meta-event", "Meta Event", "MetaEvent", "Sometimes", Options(
                    ("1", "Never"),
                    ("2", "Sometimes"),
                    ("3", "Often"))),
                ChoiceField(branchPrefix, "sleeping-event", "Sleeping Event", "SleepingEvent", "Never", Options(
                    ("1", "Never"),
                    ("2", "Sometimes"),
                    ("3", "Often"))),
                TextField(branchPrefix, "generator-fuel-consumption", "Generator Fuel Consumption", "GeneratorFuelConsumption", "0.1"),
                ChoiceField(branchPrefix, "survivor-house-chance", "Randomized Building Chance", "SurvivorHouseChance", "Rare", StoryChanceOptions()),
                ChoiceField(branchPrefix, "vehicle-story-chance", "Randomized Road Stories Chance", "VehicleStoryChance", "Rare", StoryChanceOptions()),
                ChoiceField(branchPrefix, "zone-story-chance", "Randomized Zone Stories Chance", "ZoneStoryChance", "Rare", StoryChanceOptions()),
                ChoiceField(branchPrefix, "annotated-map-chance", "Annotated Map Chance", "AnnotatedMapChance", "Sometimes", FrequencyOptions()),
                TextField(branchPrefix, "hours-for-corpse-removal", "Time Before Corpse Removal", "HoursForCorpseRemoval", "216.0"),
                ChoiceField(branchPrefix, "decaying-corpse-health-impact", "Decaying Corpse Health Impact", "DecayingCorpseHealthImpact", "Normal", Options(
                    ("1", "None"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Insane"))),
                BoolField(branchPrefix, "zombie-health-impact", "Zombie Health Impact", "ZombieHealthImpact", "false"),
                ChoiceField(branchPrefix, "blood-level", "Blood Level", "BloodLevel", "Normal", Options(
                    ("1", "None"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Ultra Gore"))),
                IntField(branchPrefix, "blood-splat-lifespan-days", "Blood Splat Lifespan Days", "BloodSplatLifespanDays", "0"),
                ChoiceField(branchPrefix, "corpse-maggot-spawn", "Corpse Maggot Spawn", "MaggotSpawn", "In and Around Bodies", Options(
                    ("1", "In and Around Bodies"),
                    ("2", "In Bodies Only"),
                    ("3", "Never"))),
                ChoiceField(branchPrefix, "media-list-meta-knowledge", "Media List Meta Knowledge", "MetaKnowledge", "Completely hidden", Options(
                    ("1", "Fully revealed"),
                    ("2", "Shown as ???"),
                    ("3", "Completely hidden"))),
                ChoiceField(branchPrefix, "day-night-cycle", "Day / Night Cycle", "DayNightCycle", "Normal", Options(
                    ("1", "Normal"),
                    ("2", "Endless Day"),
                    ("3", "Endless Night"))),
                ChoiceField(branchPrefix, "climate-cycle", "Climate Cycle", "ClimateCycle", "Normal", Options(
                    ("1", "Normal"),
                    ("2", "No Weather"),
                    ("3", "Endless Rain"),
                    ("4", "Endless Storm"),
                    ("5", "Endless Snow"),
                    ("6", "Endless Blizzard"))),
                ChoiceField(branchPrefix, "fog-cycle", "Fog Cycle", "FogCycle", "Normal", Options(
                    ("1", "Normal"),
                    ("2", "No Fog"),
                    ("3", "Endless Fog"))),
                IntField(branchPrefix, "zombies-to-damage-fences", "Zombies To Damage Fences", "ZombieLore.FenceThumpersRequired", "25"),
                TextField(branchPrefix, "fence-damage-multiplier", "Fence Damage Multiplier", "ZombieLore.FenceDamageMultiplier", "1.0"),
            });

    private static StructuredSectionDefinition BuildMetaMapSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "meta.map",
            "In-game Map",
            "Meta",
            6,
            "World map discovery and readability.",
            new[]
            {
                BoolField(branchPrefix, "allow-world-map", "Allow World Map", "Map.AllowWorldMap", "true"),
                BoolField(branchPrefix, "allow-mini-map", "Allow Mini-Map", "Map.AllowMiniMap", "false"),
                BoolField(branchPrefix, "map-all-known", "All Known On Start", "Map.MapAllKnown", "false"),
                BoolField(branchPrefix, "light-needed-to-read-map", "Light Needed To Read Map", "Map.MapNeedsLight", "true"),
            });
    private static StructuredSectionDefinition BuildCharacterCoreSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "character.core",
            "Character",
            "Character",
            7,
            "Character rules, injuries, combat, reading, and progression constraints.",
            new[]
            {
                ChoiceField(branchPrefix, "stats-decrease", "Stats Decrease", "StatsDecrease", "Normal", NumberedOptions("Very Fast", "Fast", "Normal", "Slow", "Very Slow")),
                ChoiceField(branchPrefix, "end-regen", "Endurance Regeneration", "EndRegen", "Normal", NumberedOptions("Very Fast", "Fast", "Normal", "Slow", "Very Slow")),
                BoolField(branchPrefix, "nutrition", "Nutrition System", "Nutrition", "true"),
                BoolField(branchPrefix, "starter-kit", "Starter Kit", "StarterKit", "false"),
                IntField(branchPrefix, "character-free-points", "Free Trait Points", "CharacterFreePoints", "0"),
                ChoiceField(branchPrefix, "player-built-construction-strength", "Player-built Construction Strength", "ConstructionBonusPoints", "Normal", Options(
                    ("1", "Very Low"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"))),
                ChoiceField(branchPrefix, "injury-severity", "Injury Severity", "InjurySeverity", "Normal", Options(
                    ("1", "Low"),
                    ("2", "Normal"),
                    ("3", "High"))),
                BoolField(branchPrefix, "bone-fracture", "Bone Fracture", "BoneFracture", "true"),
                TextField(branchPrefix, "muscle-strain-factor", "Muscle Strain Factor", "MuscleStrainFactor", "0.7"),
                TextField(branchPrefix, "discomfort-factor", "Discomfort Factor", "DiscomfortFactor", "0.8"),
                TextField(branchPrefix, "wound-infection-damage-factor", "Wound Infection Damage Factor", "WoundInfectionFactor", "0.0"),
                ChoiceField(branchPrefix, "clothing-degradation", "Clothing Degradation", "ClothingDegradation", "Normal", Options(
                    ("1", "Disabled"),
                    ("2", "Slow"),
                    ("3", "Normal"),
                    ("4", "Fast"))),
                BoolField(branchPrefix, "no-black-clothes", "No Black Clothes", "NoBlackClothes", "true"),
                ChoiceField(branchPrefix, "rear-vulnerability", "Rear Vulnerability", "RearVulnerability", "High", Options(
                    ("1", "Low"),
                    ("2", "Medium"),
                    ("3", "High"))),
                BoolField(branchPrefix, "multi-hit", "Weapon Multi Hit", "MultiHitZombies", "false"),
                ChoiceField(branchPrefix, "firearms-use-damage-chance", "Firearms Use Damage Chance", "FirearmUseDamageChance", "Zombies only", Options(
                    ("1", "Disabled"),
                    ("2", "Zombies only"),
                    ("3", "All types of target"))),
                TextField(branchPrefix, "firearm-noise-multiplier", "Firearm Noise Multiplier", "FirearmNoiseMultiplier", "1.0"),
                TextField(branchPrefix, "firearm-jam-multiplier", "Firearm Jam Multiplier", "FirearmJamMultiplier", "1.0"),
                TextField(branchPrefix, "firearm-moodle-multiplier", "Firearm Moodle Multiplier", "FirearmMoodleMultiplier", "1.0"),
                TextField(branchPrefix, "firearm-weather-multiplier", "Firearm Weather Multiplier", "FirearmWeatherMultiplier", "1.0"),
                BoolField(branchPrefix, "firearm-headgear-effect", "Firearm Headgear Effect", "FirearmHeadGearEffect", "true"),
                BoolField(branchPrefix, "attack-block-movements", "Melee Movement Disruption", "AttackBlockMovements", "true"),
                BoolField(branchPrefix, "all-clothes-unlocked", "All Clothing Unlocked", "AllClothesUnlocked", "false"),
                ChoiceField(branchPrefix, "enable-poisoning", "Enable Poisoning", "EnablePoisoning", "True", Options(
                    ("1", "True"),
                    ("2", "False"),
                    ("3", "Only bleach poisoning is disabled"))),
                IntField(branchPrefix, "literature-cooldown-days", "Literature Cooldown Days", "LiteratureCooldown", "45"),
                ChoiceField(branchPrefix, "negative-traits-penalty", "Negative Traits Penalty", "NegativeTraitsPenalty", "None", Options(
                    ("1", "None"),
                    ("2", "1 point penalty for every 3 negative traits selected"),
                    ("3", "1 point penalty for every 2 negative traits selected"),
                    ("4", "1 point penalty for every negative trait selected after the first"))),
                TextField(branchPrefix, "minutes-per-page", "Minutes Per Skill Book Page", "MinutesPerPage", "2.0"),
                IntField(branchPrefix, "maximum-dismantling-xp-level", "Maximum Dismantling XP Level", "LevelForDismantleXPCutoff", "0"),
                IntField(branchPrefix, "maximum-media-xp-level", "Maximum Media XP Level", "LevelForMediaXPCutoff", "3"),
                BoolField(branchPrefix, "easy-climbing", "Easy Climbing", "EasyClimbing", "false"),
                BoolField(branchPrefix, "see-not-known-recipes", "See Not Known Recipes", "SeeNotLearntRecipe", "true"),
            });

    private static StructuredSectionDefinition BuildCharacterXpSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "character.xp",
            "XP multipliers",
            "Character",
            7,
            "Global and per-skill multiplier controls.",
            new[]
            {
                TextField(branchPrefix, "xp-global-multiplier", "Global Multiplier", "MultiplierConfig.Global", "1.0"),
                BoolField(branchPrefix, "xp-use-global-multiplier", "Use Global Multiplier", "MultiplierConfig.GlobalToggle", "true"),
                TextField(branchPrefix, "xp-fitness-multiplier", "Fitness Multiplier", "MultiplierConfig.Fitness", "1.0"),
                TextField(branchPrefix, "xp-strength-multiplier", "Strength Multiplier", "MultiplierConfig.Strength", "1.0"),
                TextField(branchPrefix, "xp-sprinting-multiplier", "Sprinting Multiplier", "MultiplierConfig.Sprinting", "1.0"),
                TextField(branchPrefix, "xp-lightfooted-multiplier", "Lightfooted Multiplier", "MultiplierConfig.Lightfoot", "1.0"),
                TextField(branchPrefix, "xp-nimble-multiplier", "Nimble Multiplier", "MultiplierConfig.Nimble", "1.0"),
                TextField(branchPrefix, "xp-sneaking-multiplier", "Sneaking Multiplier", "MultiplierConfig.Sneak", "1.0"),
                TextField(branchPrefix, "xp-axe-multiplier", "Axe Multiplier", "MultiplierConfig.Axe", "1.0"),
                TextField(branchPrefix, "xp-long-blunt-multiplier", "Long Blunt Multiplier", "MultiplierConfig.Blunt", "1.0"),
                TextField(branchPrefix, "xp-short-blunt-multiplier", "Short Blunt Multiplier", "MultiplierConfig.SmallBlunt", "1.0"),
                TextField(branchPrefix, "xp-long-blade-multiplier", "Long Blade Multiplier", "MultiplierConfig.LongBlade", "1.0"),
                TextField(branchPrefix, "xp-short-blade-multiplier", "Short Blade Multiplier", "MultiplierConfig.SmallBlade", "1.0"),
                TextField(branchPrefix, "xp-spear-multiplier", "Spear Multiplier", "MultiplierConfig.Spear", "1.0"),
                TextField(branchPrefix, "xp-maintenance-multiplier", "Maintenance Multiplier", "MultiplierConfig.Maintenance", "1.0"),
                TextField(branchPrefix, "xp-agriculture-multiplier", "Agriculture Multiplier", "MultiplierConfig.Farming", "1.0"),
                TextField(branchPrefix, "xp-animal-care-multiplier", "Animal Care Multiplier", "MultiplierConfig.Husbandry", "1.0"),
                TextField(branchPrefix, "xp-carpentry-multiplier", "Carpentry Multiplier", "MultiplierConfig.Woodwork", "1.0"),
                TextField(branchPrefix, "xp-carving-multiplier", "Carving Multiplier", "MultiplierConfig.Carving", "1.0"),
                TextField(branchPrefix, "xp-cooking-multiplier", "Cooking Multiplier", "MultiplierConfig.Cooking", "1.0"),
                TextField(branchPrefix, "xp-electrical-multiplier", "Electrical Multiplier", "MultiplierConfig.Electricity", "1.0"),
                TextField(branchPrefix, "xp-first-aid-multiplier", "First Aid Multiplier", "MultiplierConfig.Doctor", "1.0"),
                TextField(branchPrefix, "xp-knapping-multiplier", "Knapping Multiplier", "MultiplierConfig.FlintKnapping", "1.0"),
                TextField(branchPrefix, "xp-masonry-multiplier", "Masonry Multiplier", "MultiplierConfig.Masonry", "1.0"),
                TextField(branchPrefix, "xp-mechanics-multiplier", "Mechanics Multiplier", "MultiplierConfig.Mechanics", "1.0"),
                TextField(branchPrefix, "xp-blacksmithing-multiplier", "Blacksmithing Multiplier", "MultiplierConfig.Blacksmith", "1.0"),
                TextField(branchPrefix, "xp-pottery-multiplier", "Pottery Multiplier", "MultiplierConfig.Pottery", "1.0"),
                TextField(branchPrefix, "xp-tailoring-multiplier", "Tailoring Multiplier", "MultiplierConfig.Tailoring", "1.0"),
                TextField(branchPrefix, "xp-welding-multiplier", "Welding Multiplier", "MultiplierConfig.MetalWelding", "1.0"),
                TextField(branchPrefix, "xp-aiming-multiplier", "Aiming Multiplier", "MultiplierConfig.Aiming", "1.0"),
                TextField(branchPrefix, "xp-reloading-multiplier", "Reloading Multiplier", "MultiplierConfig.Reloading", "1.0"),
                TextField(branchPrefix, "xp-fishing-multiplier", "Fishing Multiplier", "MultiplierConfig.Fishing", "1.0"),
                TextField(branchPrefix, "xp-foraging-multiplier", "Foraging Multiplier", "MultiplierConfig.PlantScavenging", "1.0"),
                TextField(branchPrefix, "xp-tracking-multiplier", "Tracking Multiplier", "MultiplierConfig.Tracking", "1.0"),
                TextField(branchPrefix, "xp-trapping-multiplier", "Trapping Multiplier", "MultiplierConfig.Trapping", "1.0"),
                TextField(branchPrefix, "xp-butchering-multiplier", "Butchering Multiplier", "MultiplierConfig.Butchering", "1.0"),
                TextField(branchPrefix, "xp-glassmaking-multiplier", "Glassmaking Multiplier", "MultiplierConfig.Glassmaking", "1.0"),
            });

    private static StructuredSectionDefinition BuildVehiclesSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "vehicles.core",
            "Vehicles",
            "Vehicles",
            8,
            "Vehicle availability, condition, alarms, and collision behavior.",
            new[]
            {
                BoolField(branchPrefix, "enable-vehicles", "Vehicles", "EnableVehicles", "true"),
                BoolField(branchPrefix, "vehicle-easy-use", "Easy Use", "VehicleEasyUse", "false"),
                ChoiceField(branchPrefix, "recently-survivor-vehicles", "Recent Survivor Vehicles", "RecentlySurvivorVehicles", "Low", Options(
                    ("1", "None"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"))),
                TextField(branchPrefix, "zombie-attraction-multiplier", "Zombie Attraction Multiplier", "ZombieAttractionMultiplier", "1.0"),
                ChoiceField(branchPrefix, "car-spawn-rate", "Vehicle Spawn Rate", "CarSpawnRate", "Low", Options(
                    ("1", "None"),
                    ("2", "Very Low"),
                    ("3", "Low"),
                    ("4", "Normal"),
                    ("5", "High"))),
                ChoiceField(branchPrefix, "chance-has-gas", "Chance Has Gas", "ChanceHasGas", "Low", Options(
                    ("1", "Low"),
                    ("2", "Normal"),
                    ("3", "High"))),
                ChoiceField(branchPrefix, "initial-gas", "Initial Gas", "InitialGas", "Low", Options(
                    ("1", "Very Low"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"),
                    ("6", "Full"))),
                TextField(branchPrefix, "car-gas-consumption", "Gas Consumption", "CarGasConsumption", "1.0"),
                ChoiceField(branchPrefix, "locked-car", "Locked Vehicle Frequency", "LockedCar", "Sometimes", FrequencyOptions()),
                ChoiceField(branchPrefix, "car-general-condition", "General Condition", "CarGeneralCondition", "Normal", Options(
                    ("1", "Very Low"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"))),
                BoolField(branchPrefix, "traffic-jam", "Car Wreck Congestion", "TrafficJam", "true"),
                ChoiceField(branchPrefix, "car-alarm", "Vehicle Alarms Frequency", "CarAlarm", "Rare", FrequencyOptions()),
                BoolField(branchPrefix, "player-damage-from-crash", "Player Damage from Crash", "PlayerDamageFromCrash", "true"),
                ChoiceField(branchPrefix, "car-damage-on-impact", "Car Damage on Impact", "CarDamageOnImpact", "Normal", Options(
                    ("1", "Very Low"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"))),
                TextField(branchPrefix, "siren-shutoff-hours", "Siren Shutoff Hours", "SirenShutoffHours", "0.0"),
                ChoiceField(branchPrefix, "damage-to-player-from-hit-by-a-car", "Player Damage From Vehicle Impact", "DamageToPlayerFromHitByACar", "None", Options(
                    ("1", "None"),
                    ("2", "Low"),
                    ("3", "Normal"),
                    ("4", "High"),
                    ("5", "Very High"))),
                BoolField(branchPrefix, "vehicle-sirens-attract-zombies", "Vehicle Sirens Attract Zombies", "SirenEffectsZombies", "true"),
            });

    private static StructuredSectionDefinition BuildLivestockSection(string branchPrefix) =>
        Section(
            branchPrefix,
            "livestock.core",
            "Livestock",
            "Livestock",
            9,
            "Animal pacing, spawning, breeding, and trail behavior.",
            new[]
            {
                ChoiceField(branchPrefix, "livestock-stats-decrease", "Stats Reduction Speed", "AnimalStatsModifier", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "pregnancy-time", "Pregnancy Time", "AnimalPregnancyTime", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "egg-hatch-time", "Egg Hatch Time", "AnimalEggHatch", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "aging-modifier-speed", "Aging Modifier Speed", "AnimalAgeModifier", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "milk-increase-speed", "Milk Increase Speed", "AnimalMilkIncModifier", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "wool-increase-speed", "Wool Increase Speed", "AnimalWoolIncModifier", "Normal", AnimalSpeedOptions()),
                ChoiceField(branchPrefix, "animal-spawn-chance", "Animal Spawn Chance", "AnimalRanchChance", "Often", FrequencyWithAlwaysOptions()),
                IntField(branchPrefix, "grass-regrowth-time", "Grass Regrowth time", "AnimalGrassRegrowTime", "240"),
                BoolField(branchPrefix, "meta-predator", "Meta Predator", "AnimalMetaPredator", "false"),
                BoolField(branchPrefix, "breeding-season", "Breeding Season", "AnimalMatingSeason", "true"),
                BoolField(branchPrefix, "animals-attract-zombies", "Animals Attract Zombies", "AnimalSoundAttractZombies", "true"),
                ChoiceField(branchPrefix, "animal-tracks-chance", "Animal Tracks Chance", "AnimalTrackChance", "Sometimes", FrequencyOptions()),
                ChoiceField(branchPrefix, "animal-paths-chance", "Animal Paths Chance", "AnimalPathChance", "Sometimes", FrequencyOptions()),
            });

    private static StructuredSectionDefinition Section(
        string branchPrefix,
        string sectionSuffix,
        string title,
        string categoryTitle,
        int categoryOrder,
        string description,
        IReadOnlyList<StructuredFieldDefinition> fields)
    {
        var categoryId = $"{branchPrefix}.sandbox.category.{categoryTitle.ToLowerInvariant()}";
        return new StructuredSectionDefinition(
            $"{branchPrefix}.sandbox.{sectionSuffix}",
            title,
            fields,
            description,
            categoryId,
            categoryTitle,
            categoryOrder);
    }

    private static StructuredFieldDefinition ChoiceField(
        string branchPrefix,
        string fieldSuffix,
        string label,
        string keyPath,
        string defaultValue,
        IReadOnlyList<StructuredFieldOptionDefinition> options,
        string? helpText = null) =>
        Field(branchPrefix, fieldSuffix, label, StructuredValueKind.Choice, keyPath, defaultValue, helpText, options);

    private static StructuredFieldDefinition TextField(
        string branchPrefix,
        string fieldSuffix,
        string label,
        string keyPath,
        string defaultValue,
        string? helpText = null) =>
        Field(branchPrefix, fieldSuffix, label, StructuredValueKind.Text, keyPath, defaultValue, helpText);

    private static StructuredFieldDefinition IntField(
        string branchPrefix,
        string fieldSuffix,
        string label,
        string keyPath,
        string defaultValue,
        string? helpText = null) =>
        Field(branchPrefix, fieldSuffix, label, StructuredValueKind.Integer, keyPath, defaultValue, helpText);

    private static StructuredFieldDefinition BoolField(
        string branchPrefix,
        string fieldSuffix,
        string label,
        string keyPath,
        string defaultValue,
        string? helpText = null) =>
        Field(branchPrefix, fieldSuffix, label, StructuredValueKind.Boolean, keyPath, defaultValue, helpText);

    private static StructuredFieldDefinition Field(
        string branchPrefix,
        string fieldSuffix,
        string label,
        StructuredValueKind valueKind,
        string keyPath,
        string defaultValue,
        string? helpText = null,
        IReadOnlyList<StructuredFieldOptionDefinition>? options = null) =>
        new(
            $"{branchPrefix}.sandbox.{fieldSuffix}",
            label,
            valueKind,
            new StructuredConfigTarget(ConfigFileKind.SandboxVars, keyPath),
            defaultValue,
            false,
            helpText,
            options);

    private static IReadOnlyList<StructuredFieldOptionDefinition> OptionRange(int start, int count, Func<int, string> labelFactory) =>
        Enumerable.Range(start, count)
            .Select(value => new StructuredFieldOptionDefinition(
                value.ToString(CultureInfo.InvariantCulture),
                labelFactory(value),
                null))
            .ToArray();

    private static IReadOnlyList<StructuredFieldOptionDefinition> Options(params (string Value, string Label)[] options) =>
        options
            .Select(option => new StructuredFieldOptionDefinition(option.Value, option.Label, null))
            .ToArray();

    private static IReadOnlyList<StructuredFieldOptionDefinition> NumberedOptions(params string[] labels) =>
        labels
            .Select((label, index) => new StructuredFieldOptionDefinition((index + 1).ToString(CultureInfo.InvariantCulture), label, null))
            .ToArray();

    private static IReadOnlyList<StructuredFieldOptionDefinition> DayLengthOptions() =>
        Options(
            ("1", "15 Minutes"),
            ("2", "30 Minutes"),
            ("3", "1 Hour"),
            ("4", "1 Hour, 30 Minutes"),
            ("5", "2 Hours"),
            ("6", "3 Hours"),
            ("7", "4 Hours"),
            ("8", "5 Hours"),
            ("9", "6 Hours"),
            ("10", "7 Hours"),
            ("11", "8 Hours"),
            ("12", "9 Hours"),
            ("13", "10 Hours"),
            ("14", "11 Hours"),
            ("15", "12 Hours"),
            ("16", "13 Hours"),
            ("17", "14 Hours"),
            ("18", "15 Hours"),
            ("19", "16 Hours"),
            ("20", "17 Hours"),
            ("21", "18 Hours"),
            ("22", "19 Hours"),
            ("23", "20 Hours"),
            ("24", "21 Hours"),
            ("25", "22 Hours"),
            ("26", "23 Hours"),
            ("27", "Real-time"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> TimeSinceApocalypseOptions() =>
        OptionRange(1, 13, value => (value - 1).ToString(CultureInfo.InvariantCulture));

    private static IReadOnlyList<StructuredFieldOptionDefinition> MonthOptions() =>
        NumberedOptions("January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December");

    private static IReadOnlyList<StructuredFieldOptionDefinition> StartTimeOptions() =>
        NumberedOptions("7 AM", "9 AM", "12 PM", "2 PM", "5 PM", "9 PM", "12 AM", "2 AM", "5 AM");

    private static IReadOnlyList<StructuredFieldOptionDefinition> ZombieCountOptions() =>
        Options(
            ("1", "Insane"),
            ("2", "Very High"),
            ("3", "High"),
            ("4", "Normal"),
            ("5", "Low"),
            ("6", "None"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> CrawlUnderVehicleOptions() =>
        Options(
            ("1", "Crawlers Only"),
            ("2", "Extremely Rare"),
            ("3", "Rare"),
            ("4", "Sometimes"),
            ("5", "Often"),
            ("6", "Very Often"),
            ("7", "Always"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> ZombiePopulationOptions() =>
        Options(
            ("2.5", "Insane"),
            ("1.6", "Very High"),
            ("1.2", "High"),
            ("0.65", "Normal"),
            ("0.15", "Low"),
            ("0.0", "None"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> ZombiePopulationStartOptions() =>
        Options(
            ("3.0", "Insane"),
            ("2.0", "Very High"),
            ("1.5", "High"),
            ("1.0", "Normal"),
            ("0.5", "Low"),
            ("0.0", "None"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> GeneratorSpawningOptions() =>
        Options(
            ("1", "None"),
            ("2", "Insanely Rare"),
            ("3", "Extremely Rare"),
            ("4", "Rare"),
            ("5", "Normal"),
            ("6", "Common"),
            ("7", "Abundant"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> WaterShutoffOptions() =>
        Options(
            ("1", "Instant"),
            ("2", "0 - 30 Days"),
            ("3", "0 - 2 Months"),
            ("4", "0 - 6 Months"),
            ("5", "0 - 1 Year"),
            ("6", "0 - 5 Years"),
            ("7", "2 - 6 Months"),
            ("8", "6 - 12 Months"),
            ("9", "Disabled"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> ElectricityShutoffOptions() =>
        Options(
            ("1", "Instant"),
            ("2", "14 - 30 Days"),
            ("3", "14 Days - 2 Months"),
            ("4", "14 Days - 6 Months"),
            ("5", "14 Days - 1 Year"),
            ("6", "14 Days - 5 Years"),
            ("7", "2 - 6 Months"),
            ("8", "6 - 12 Months"),
            ("9", "Disabled"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> AlarmDecayOptions() =>
        Options(
            ("1", "Instant"),
            ("2", "0 - 30 Days"),
            ("3", "0 - 2 Months"),
            ("4", "0 - 6 Months"),
            ("5", "0 - 1 Year"),
            ("6", "0 - 5 Years"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> FrequencyOptions() =>
        Options(
            ("1", "Never"),
            ("2", "Extremely Rare"),
            ("3", "Rare"),
            ("4", "Sometimes"),
            ("5", "Often"),
            ("6", "Very Often"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> FrequencyWithAlwaysOptions() =>
        Options(
            ("1", "Never"),
            ("2", "Extremely Rare"),
            ("3", "Rare"),
            ("4", "Sometimes"),
            ("5", "Often"),
            ("6", "Very Often"),
            ("7", "Always"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> StoryChanceOptions() =>
        Options(
            ("1", "Never"),
            ("2", "Extremely Rare"),
            ("3", "Rare"),
            ("4", "Sometimes"),
            ("5", "Often"),
            ("6", "Very Often"),
            ("7", "Always Tries"));

    private static IReadOnlyList<StructuredFieldOptionDefinition> AnimalSpeedOptions() =>
        Options(
            ("1", "Ultra Fast"),
            ("2", "Very Fast"),
            ("3", "Fast"),
            ("4", "Normal"),
            ("5", "Slow"),
            ("6", "Very Slow"));
}
