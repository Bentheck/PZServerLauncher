using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class StructuredDocumentServiceTests
{
    private readonly IniDocumentService _iniService = new();
    private readonly SandboxVarsDocumentService _sandboxService = new();

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
}
