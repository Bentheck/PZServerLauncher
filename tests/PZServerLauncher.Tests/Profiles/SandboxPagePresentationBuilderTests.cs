using PZServerLauncher.Contracts.Profiles;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Profiles;

public sealed class SandboxPagePresentationBuilderTests
{
    [Fact]
    public void Build_GroupsSectionsIntoCategoriesAndTracksPresetMatch()
    {
        var page = new SettingsPageDto(
            "sandbox",
            "Sandbox",
            "Sandbox page",
            true,
            true,
            [
                new SettingsSectionDto(
                    "time.setup",
                    "Time",
                    "Timeline",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.day-length",
                            "Day Length",
                            "DayLength",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "1 Hour, 30 Minutes",
                            null,
                            false,
                            false,
                            [new SettingsFieldOptionDto("1 Hour, 30 Minutes", "1 Hour, 30 Minutes", null)])
                    ],
                    "b42.sandbox.category.time",
                    "Time",
                    1),
                new SettingsSectionDto(
                    "zombie.basics",
                    "Zombie",
                    "Population",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.zombies",
                            "Zombie Count",
                            "Zombies",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "Normal",
                            null,
                            false,
                            false,
                            [new SettingsFieldOptionDto("Normal", "Normal", null)])
                    ],
                    "b42.sandbox.category.zombie",
                    "Zombie",
                    2)
            ]);

        var preset = new SandboxPresetDto(
            "b42.apocalypse",
            "Apocalypse",
            true,
            new Dictionary<string, string?>
            {
                ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
                ["b42.sandbox.zombies"] = "Normal",
            });

        var categories = SandboxPagePresentationBuilder.Build(
            page,
            new Dictionary<string, string?>
            {
                ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
                ["b42.sandbox.zombies"] = "High",
            },
            preset,
            null);

        Assert.Equal(["Time", "Zombie"], categories.Select(category => category.Title));
        Assert.True(categories[0].MatchesPreset);
        Assert.False(categories[1].MatchesPreset);
        Assert.Equal(1, categories[1].ComparedFieldCount);
        Assert.Equal(0, categories[1].MatchingFieldCount);
    }

    [Fact]
    public void Build_FiltersCategoriesBySearchText()
    {
        var page = new SettingsPageDto(
            "sandbox",
            "Sandbox",
            "Sandbox page",
            true,
            true,
            [
                new SettingsSectionDto(
                    "time.setup",
                    "Time",
                    "Timeline",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.day-length",
                            "Day Length",
                            "DayLength",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "1 Hour, 30 Minutes",
                            "World pacing",
                            false,
                            false,
                            [new SettingsFieldOptionDto("1 Hour, 30 Minutes", "1 Hour, 30 Minutes", null)])
                    ],
                    "b42.sandbox.category.time",
                    "Time",
                    1),
                new SettingsSectionDto(
                    "zombie.basics",
                    "Zombie",
                    "Population",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.zombies",
                            "Zombie Count",
                            "Zombies",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "Normal",
                            "Zombie pressure",
                            false,
                            false,
                            [new SettingsFieldOptionDto("Normal", "Normal", null)])
                    ],
                    "b42.sandbox.category.zombie",
                    "Zombie",
                    2)
            ]);

        var categories = SandboxPagePresentationBuilder.Build(
            page,
            new Dictionary<string, string?>(),
            null,
            "pressure");

        Assert.Single(categories);
        Assert.Equal("Zombie", categories[0].Title);
    }

    [Fact]
    public void Build_KeepsPresetStatusBasedOnWholeCategoryWhenSearchFiltersVisibleFields()
    {
        var page = new SettingsPageDto(
            "sandbox",
            "Sandbox",
            "Sandbox page",
            true,
            true,
            [
                new SettingsSectionDto(
                    "time.setup",
                    "Time",
                    "Timeline",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.day-length",
                            "Day Length",
                            "DayLength",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "1 Hour, 30 Minutes",
                            null,
                            false,
                            false,
                            [new SettingsFieldOptionDto("1 Hour, 30 Minutes", "1 Hour, 30 Minutes", null)]),
                        new SettingsFieldDto(
                            "b42.sandbox.start-month",
                            "Start Month",
                            "StartMonth",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "July",
                            null,
                            false,
                            false,
                            [new SettingsFieldOptionDto("July", "July", null)])
                    ],
                    "b42.sandbox.category.time",
                    "Time",
                    1)
            ]);

        var preset = new SandboxPresetDto(
            "b42.apocalypse",
            "Apocalypse",
            true,
            new Dictionary<string, string?>
            {
                ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
                ["b42.sandbox.start-month"] = "July",
            });

        var categories = SandboxPagePresentationBuilder.Build(
            page,
            new Dictionary<string, string?>
            {
                ["b42.sandbox.day-length"] = "1 Hour, 30 Minutes",
                ["b42.sandbox.start-month"] = "August",
            },
            preset,
            "day length");

        var category = Assert.Single(categories);
        var section = Assert.Single(category.Sections);
        Assert.Single(section.Fields);
        Assert.False(category.MatchesPreset);
        Assert.Equal(2, category.ComparedFieldCount);
        Assert.Equal(1, category.MatchingFieldCount);
    }

    [Fact]
    public void Build_IncludesCurrentChoiceValueWhenItDoesNotExistInCatalogOptions()
    {
        var page = new SettingsPageDto(
            "sandbox",
            "Sandbox",
            "Sandbox page",
            true,
            true,
            [
                new SettingsSectionDto(
                    "zombie.basics",
                    "Zombie",
                    "Population",
                    [
                        new SettingsFieldDto(
                            "b42.sandbox.zombies",
                            "Zombie Count",
                            "Zombies",
                            ConfigFileKind.SandboxVars,
                            SettingsFieldControlKind.Select,
                            SettingsValueKind.String,
                            "Normal",
                            null,
                            false,
                            false,
                            [
                                new SettingsFieldOptionDto("Normal", "Normal", null),
                                new SettingsFieldOptionDto("High", "High", null),
                            ])
                    ],
                    "b42.sandbox.category.zombie",
                    "Zombie",
                    2)
            ]);

        var categories = SandboxPagePresentationBuilder.Build(
            page,
            new Dictionary<string, string?> { ["b42.sandbox.zombies"] = "Insane" },
            null,
            null);

        var field = Assert.Single(categories[0].Sections[0].Fields);
        Assert.Equal("Insane", field.CurrentValue);
        Assert.Equal("Insane", field.Options[0].Value);
        Assert.Equal("Insane (Current)", field.Options[0].Label);
    }

    [Fact]
    public void ResolvePreset_FallsBackToFirstPresetWhenRequestedPresetIsMissing()
    {
        IReadOnlyList<SandboxPresetDto> presets =
        [
            new("preset-a", "Preset A", true, new Dictionary<string, string?>()),
            new("preset-b", "Preset B", false, new Dictionary<string, string?>()),
        ];

        var preset = SandboxPagePresentationBuilder.ResolvePreset(presets, "missing");

        Assert.NotNull(preset);
        Assert.Equal("preset-a", preset!.PresetId);
    }
}
