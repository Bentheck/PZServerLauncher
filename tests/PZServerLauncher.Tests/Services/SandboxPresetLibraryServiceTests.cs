using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Tests.Services;

public sealed class SandboxPresetLibraryServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void List_ReturnsShippedLuaPresetsAndMapsValuesToEditorLabels()
    {
        CopyBuiltInPreset("Apocalypse.lua");
        CopyBuiltInPreset("Outbreak.lua");

        var service = CreateService();
        var profile = CreateProfile();

        var presets = service.List(profile);

        Assert.Equal(["Apocalypse", "Outbreak"], presets.Select(preset => preset.Label));

        var outbreak = Assert.Single(presets, preset => preset.Label == "Outbreak");
        Assert.True(outbreak.IsBuiltIn);
        Assert.Equal("0 - 2 Months", outbreak.Values["b42.sandbox.water-shut"]);
        Assert.Equal("14 Days - 2 Months", outbreak.Values["b42.sandbox.electricity-shut"]);
        Assert.Equal("true", outbreak.Values["b42.sandbox.allow-mini-map"]);
        Assert.Equal("1.5", outbreak.Values["b42.sandbox.xp-first-aid-multiplier"]);
        Assert.Equal("Very Often", outbreak.Values["b42.sandbox.animal-spawn-chance"]);
    }

    [Fact]
    public void Save_CreatesCustomLuaPresetAndDelete_RemovesIt()
    {
        CopyBuiltInPreset("Apocalypse.lua");

        var service = CreateService();
        var profile = CreateProfile();
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["b42.sandbox.water-shut"] = "0 - 2 Months",
            ["b42.sandbox.electricity-shut"] = "14 Days - 2 Months",
            ["b42.sandbox.allow-mini-map"] = "true",
            ["b42.sandbox.population-peak-day"] = "20",
            ["b42.sandbox.xp-first-aid-multiplier"] = "1.5",
        };

        var saved = service.Save(profile, "Night Shift", values);
        var presetPath = Path.Combine(_tempRoot, "data", "sandbox-presets", "b42", "custom", "Night Shift.lua");
        var presetText = File.ReadAllText(presetPath);

        Assert.False(saved.IsBuiltIn);
        Assert.Equal("Night Shift", saved.Label);
        Assert.Contains("return {", presetText);
        Assert.Contains("WaterShut = 3", presetText);
        Assert.Contains("ElecShut = 3", presetText);
        Assert.Contains("PopulationPeakDay = 20", presetText);
        Assert.Contains("AllowMiniMap = true", presetText);

        var listedCustom = Assert.Single(service.List(profile), preset => !preset.IsBuiltIn);
        Assert.Equal("Night Shift", listedCustom.Label);
        Assert.Equal("0 - 2 Months", listedCustom.Values["b42.sandbox.water-shut"]);
        Assert.Equal("1.5", listedCustom.Values["b42.sandbox.xp-first-aid-multiplier"]);

        Assert.True(service.Delete(profile, listedCustom.PresetId));
        Assert.DoesNotContain(service.List(profile), preset => string.Equals(preset.Label, "Night Shift", StringComparison.Ordinal));
        Assert.False(File.Exists(presetPath));
    }

    private SandboxPresetLibraryService CreateService() =>
        new(
            new AppPaths(_tempRoot),
            new ProjectZomboidSettingsCatalogResolver(),
            new SandboxPresetDocumentService());

    private ServerProfile CreateProfile() =>
        ServerProfileFactory.CreateStarterProfile() with
        {
            ProfileId = "profile-sandbox-presets",
            DisplayName = "Sandbox Preset Test",
            ServerName = "sandbox-preset-test",
            InstallDirectory = Path.Combine(_tempRoot, "PZServers", "Installs", "sandbox-preset-test"),
            CacheDirectory = Path.Combine(_tempRoot, "PZServers", "Profiles", "sandbox-preset-test"),
            Branch = ProjectZomboidBranch.Unstable42,
        };

    private void CopyBuiltInPreset(string fileName)
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "sandbox-presets",
            "b42",
            fileName);
        if (!File.Exists(sourcePath))
        {
            sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "PZServerLauncher.Runtime",
                "Assets",
                "ProjectZomboid",
                "SandboxPresets",
                "b42",
                fileName));
        }

        var destinationPath = Path.Combine(_tempRoot, "sandbox-presets", "b42", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
