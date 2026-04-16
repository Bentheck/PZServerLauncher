using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class StructuredDocumentServiceTests
{
    private readonly IniDocumentService _iniService = new();
    private readonly SandboxVarsDocumentService _sandboxService = new();

    [Fact]
    public void IniDocumentService_RoundTripsValidTextExactly()
    {
        var source = """
            ; Project Zomboid server settings
            [Server]
            ServerName=alpha42
            DefaultPort=16261

            # Keep this comment
            BindIP=0.0.0.0
            """;

        var document = _iniService.Parse(source);

        Assert.True(document.IsSupported);
        Assert.Empty(document.Issues);
        Assert.Equal(source, _iniService.Format(document));
    }

    [Fact]
    public void IniDocumentService_PreservesTextAndFlagsInvalidLines()
    {
        var document = _iniService.Parse("""
            [Server]
            ServerName=alpha
            InvalidLine
            """);

        Assert.False(document.IsSupported);
        Assert.Single(document.Issues);
        Assert.Equal("Expected a key=value entry.", document.Issues[0].Message);
        Assert.Contains("ServerName=alpha", _iniService.Format(document));
    }

    [Fact]
    public void IniDocumentService_ReadsAndUpdatesStructuredValues()
    {
        const string source = """
            # Dedicated server
            PublicName=Alpha 42
            Public=true
            ServerWelcomeMessage=Welcome survivor! <LINE> Stay alive.
            """;

        var values = _iniService.ReadValues(source, ["PublicName", "Public", "ServerWelcomeMessage"]);
        Assert.Equal("Alpha 42", values["PublicName"]);
        Assert.Equal("true", values["Public"]);
        Assert.Equal("Welcome survivor! <LINE> Stay alive.", values["ServerWelcomeMessage"]);

        var updated = _iniService.ApplyValues(source, new Dictionary<string, string?>
        {
            ["PublicName"] = "Bravo 42",
            ["Public"] = "false",
            ["MaxPlayers"] = "24",
        });

        Assert.Contains("PublicName=Bravo 42", updated);
        Assert.Contains("Public=false", updated);
        Assert.Contains("MaxPlayers=24", updated);
        Assert.Contains("# Dedicated server", updated);
    }

    [Fact]
    public void IniDocumentService_InsertsWorkshopItemsImmediatelyAfterMods()
    {
        const string source = """
            # Mods block
            Mods=QuickRestart
            Map=Muldraugh, KY
            """;

        var updated = _iniService.ApplyValues(source, new Dictionary<string, string?>
        {
            ["Mods"] = "QuickRestart",
            ["WorkshopItems"] = "3699503439",
            ["Map"] = "Muldraugh, KY",
        });

        var modsIndex = updated.IndexOf("Mods=QuickRestart", StringComparison.Ordinal);
        var workshopIndex = updated.IndexOf("WorkshopItems=3699503439", StringComparison.Ordinal);
        var mapIndex = updated.IndexOf("Map=Muldraugh, KY", StringComparison.Ordinal);

        Assert.True(modsIndex >= 0);
        Assert.True(workshopIndex > modsIndex);
        Assert.True(mapIndex > workshopIndex);
        Assert.Contains("List Workshop Mod IDs for the server to download.", updated);
    }

    [Fact]
    public void IniDocumentService_BuildsModsBlockWithWorkshopItemsBetweenModsAndMap()
    {
        var updated = _iniService.ApplyValues(string.Empty, new Dictionary<string, string?>
        {
            ["Mods"] = "QuickRestart",
            ["WorkshopItems"] = "3699503439",
            ["Map"] = "Muldraugh, KY",
        });

        var modsIndex = updated.IndexOf("Mods=QuickRestart", StringComparison.Ordinal);
        var workshopIndex = updated.IndexOf("WorkshopItems=3699503439", StringComparison.Ordinal);
        var mapIndex = updated.IndexOf("Map=Muldraugh, KY", StringComparison.Ordinal);

        Assert.True(modsIndex >= 0);
        Assert.True(workshopIndex > modsIndex);
        Assert.True(mapIndex > workshopIndex);
        Assert.Contains("List Workshop Mod IDs for the server to download.", updated);
    }

    [Fact]
    public void SandboxVarsDocumentService_PreservesTextAndFlagsMissingSandboxVars()
    {
        var document = _sandboxService.Parse("""
            return {
                ZombieCount = 3
            }
            """);

        Assert.False(document.IsSupported);
        Assert.Contains(document.Issues, issue => issue.Message.Contains("SandboxVars table", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ZombieCount", _sandboxService.Format(document));
    }

    [Fact]
    public void SandboxVarsDocumentService_ReadsAndUpdatesStructuredValues()
    {
        const string source = """
            SandboxVars = {
                VERSION = 4,
                Zombies = 4, -- Spawn rate
                StarterKit = false,
                ZombieLore = {
                    Speed = 3,
                }
            }
            """;

        var values = _sandboxService.ReadValues(source, ["Zombies", "StarterKit", "ZombieLore.Speed"]);
        Assert.Equal("4", values["Zombies"]);
        Assert.Equal("false", values["StarterKit"]);
        Assert.Equal("3", values["ZombieLore.Speed"]);

        var updated = _sandboxService.ApplyValues(source, new Dictionary<string, string?>
        {
            ["Zombies"] = "2",
            ["StarterKit"] = "true",
            ["WaterShutModifier"] = "500",
        });

        Assert.Contains("Zombies = 2, -- Spawn rate", updated);
        Assert.Contains("StarterKit = true,", updated);
        Assert.Contains("WaterShutModifier = 500,", updated);
        Assert.Contains("ZombieLore = {", updated);
    }

    [Fact]
    public void SandboxVarsDocumentService_InsertsMissingNestedValuesIntoExistingTable()
    {
        const string source = """
            SandboxVars = {
                VERSION = 4,
                ZombieLore = {
                    Speed = 3,
                }
            }
            """;

        var updated = _sandboxService.ApplyValues(source, new Dictionary<string, string?>
        {
            ["ZombieLore.Speed"] = "2",
            ["ZombieLore.Strength"] = "4",
            ["ZombieLore.Cognition"] = "2",
        });

        Assert.Contains("Speed = 2,", updated);
        Assert.Contains("Strength = 4,", updated);
        Assert.Contains("Cognition = 2,", updated);
    }

    [Fact]
    public void SandboxVarsDocumentService_BuildsMissingTopLevelTableForNestedValues()
    {
        var updated = _sandboxService.ApplyValues(string.Empty, new Dictionary<string, string?>
        {
            ["ZombieLore.Speed"] = "2",
            ["ZombieLore.Strength"] = "3",
            ["StarterKit"] = "true",
        });

        Assert.Contains("StarterKit = true,", updated);
        Assert.Contains("ZombieLore = {", updated);
        Assert.Contains("Speed = 2,", updated);
        Assert.Contains("Strength = 3,", updated);
    }

    [Fact]
    public void SandboxVarsDocumentService_ReadsAndWritesQuotedStringValues()
    {
        const string source = """
            SandboxVars = {
                VERSION = 4,
                DayLength = "1 Hour, 30 Minutes",
                CorpseMaggotSpawn = "In and Around Bodies",
            }
            """;

        var values = _sandboxService.ReadValues(source, ["DayLength", "CorpseMaggotSpawn"]);

        Assert.Equal("1 Hour, 30 Minutes", values["DayLength"]);
        Assert.Equal("In and Around Bodies", values["CorpseMaggotSpawn"]);

        var updated = _sandboxService.ApplyValues(source, new Dictionary<string, string?>
        {
            ["DayLength"] = "Real-Time",
            ["CorpseMaggotSpawn"] = "None",
        });

        Assert.Contains("DayLength = \"Real-Time\",", updated);
        Assert.Contains("CorpseMaggotSpawn = \"None\",", updated);
    }
}
