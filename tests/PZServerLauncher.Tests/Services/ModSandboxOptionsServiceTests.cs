using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Runtime.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class ModSandboxOptionsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Discover_MapsInstalledModOptionsToTypedTranslatedFields()
    {
        var modDirectory = CreateModDirectory("42.19", "TestMod", "Test Mod");
        WriteOptions(modDirectory, """
            option TestMod.Enabled {
                type = boolean,
                default = true,
                page = TestModPage,
                translation = TestModEnabled,
            }
            option TestMod.Amount = {
                type = integer,
                min = 1,
                max = 25,
                default = 5,
                page = TestModPage,
                translation = TestModAmount,
            }
            option TestMod.Multiplier {
                type = double,
                min = 0.5,
                max = 4.5,
                default = 1.5,
                page = TestModPage,
            }
            option TestMod.Mode {
                type = enum,
                numValues = 2,
                default = 1,
                page = TestModPage,
                translation = TestModMode,
                valueTranslation = TestModModeValue,
            }
            option TestMod.Label {
                type = string,
                default = "My Server",
                page = TestModPage,
            }
            """);
        WriteTranslations(modDirectory, """
            Sandbox_EN = {
                Sandbox_TestModPage = "Test Mod Settings",
                Sandbox_TestModEnabled = "Enable Feature",
                Sandbox_TestModEnabled_tooltip = "Turns the feature on.",
                Sandbox_TestModAmount = "Spawn Amount",
                Sandbox_TestModMode = "Mode",
                Sandbox_TestModModeValue_option1 = "Quiet",
                Sandbox_TestModModeValue_option2 = "Loud",
            }
            """);

        var result = new ModSandboxOptionsService().Discover(CreateProfile("42.19"), ["TestMod"]);

        Assert.Equal(1, result.DiscoveredModCount);
        var section = Assert.Single(result.Sections);
        Assert.Equal("Test Mod: Test Mod Settings", section.DisplayName);
        Assert.Equal("Test Mod", section.CategoryTitle);
        Assert.Equal(Path.Combine(modDirectory, "media", "sandbox-options.txt"), section.SourceFilePath);

        var enabled = Assert.Single(section.Fields, field => field.Target.KeyPath == "TestMod.Enabled");
        Assert.Equal(StructuredValueKind.Boolean, enabled.ValueKind);
        Assert.Equal("Enable Feature", enabled.DisplayName);
        Assert.Contains("Turns the feature on", enabled.HelpText);

        var amount = Assert.Single(section.Fields, field => field.Target.KeyPath == "TestMod.Amount");
        Assert.Equal(StructuredValueKind.Integer, amount.ValueKind);
        Assert.Equal(1m, amount.Minimum);
        Assert.Equal(25m, amount.Maximum);
        Assert.Equal("5", amount.DefaultValue);

        var multiplier = Assert.Single(section.Fields, field => field.Target.KeyPath == "TestMod.Multiplier");
        Assert.Equal(StructuredValueKind.Number, multiplier.ValueKind);

        var mode = Assert.Single(section.Fields, field => field.Target.KeyPath == "TestMod.Mode");
        Assert.Collection(
            mode.Options!,
            option => Assert.Equal(("1", "Quiet"), (option.Value, option.Label)),
            option => Assert.Equal(("2", "Loud"), (option.Value, option.Label)));

        var label = Assert.Single(section.Fields, field => field.Target.KeyPath == "TestMod.Label");
        Assert.Equal(StructuredValueKind.Text, label.ValueKind);
        Assert.Equal("My Server", label.DefaultValue);
    }

    [Fact]
    public void Discover_UsesHighestVersionCompatibleWithSelectedBranch()
    {
        var olderDirectory = CreateModDirectory("42.19", "VersionedMod", "Versioned Mod");
        WriteOptions(olderDirectory, "option Versioned.Older { type = boolean, default = true, }");

        var newerDirectory = CreateModDirectory("42.20.1", "VersionedMod", "Versioned Mod");
        WriteOptions(newerDirectory, "option Versioned.Newer { type = boolean, default = true, }");

        var result = new ModSandboxOptionsService().Discover(CreateProfile("42.19"), ["VersionedMod"]);

        var field = Assert.Single(Assert.Single(result.Sections).Fields);
        Assert.Equal("Versioned.Older", field.Target.KeyPath);
    }

    [Fact]
    public void Discover_UsesLegacyRootDefinitionsForBuild41()
    {
        var rootDirectory = CreateModDirectory(null, "LegacyMod", "Legacy Mod");
        WriteOptions(rootDirectory, "option Legacy.RootSetting { type = boolean, default = false, }");

        var build42Directory = CreateModDirectory("42.19", "LegacyMod", "Legacy Mod");
        WriteOptions(build42Directory, "option Legacy.Build42Setting { type = boolean, default = true, }");

        var result = new ModSandboxOptionsService().Discover(CreateProfile("legacy41"), ["LegacyMod"]);

        var field = Assert.Single(Assert.Single(result.Sections).Fields);
        Assert.Equal("Legacy.RootSetting", field.Target.KeyPath);
    }

    [Fact]
    public void ParseSteamLibraryRoots_ReadsEscapedVdfPaths()
    {
        const string content = """
            "libraryfolders"
            {
                "0" { "path" "D:\\Steam" }
                "1" { "path" "E:\\Games\\SteamLibrary" }
            }
            """;

        var roots = ModSandboxOptionsService.ParseSteamLibraryRoots(content);

        Assert.Equal([@"D:\Steam", @"E:\Games\SteamLibrary"], roots);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private ServerProfile CreateProfile(string steamBranch) => new()
    {
        ProfileId = "test-profile",
        DisplayName = "Test Profile",
        ServerName = "servertest",
        InstallDirectory = Path.Combine(_tempRoot, "steamapps", "common", "Project Zomboid Dedicated Server"),
        CacheDirectory = Path.Combine(_tempRoot, "cache"),
        SteamBranch = steamBranch,
    };

    private string CreateModDirectory(string? variant, string modId, string modName)
    {
        var directory = Path.Combine(
            _tempRoot,
            "steamapps",
            "workshop",
            "content",
            "108600",
            "1234567890",
            "mods",
            modId);
        if (!string.IsNullOrWhiteSpace(variant))
        {
            directory = Path.Combine(directory, variant);
        }

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "steamapps", "common", "Project Zomboid Dedicated Server"));
        File.WriteAllText(Path.Combine(directory, "mod.info"), $"id={modId}{Environment.NewLine}name={modName}");
        return directory;
    }

    private static void WriteOptions(string modDirectory, string content)
    {
        var mediaDirectory = Path.Combine(modDirectory, "media");
        Directory.CreateDirectory(mediaDirectory);
        File.WriteAllText(Path.Combine(mediaDirectory, "sandbox-options.txt"), content);
    }

    private static void WriteTranslations(string modDirectory, string content)
    {
        var translationDirectory = Path.Combine(modDirectory, "media", "lua", "shared", "Translate", "EN");
        Directory.CreateDirectory(translationDirectory);
        File.WriteAllText(Path.Combine(translationDirectory, "Sandbox_EN.txt"), content);
    }
}
