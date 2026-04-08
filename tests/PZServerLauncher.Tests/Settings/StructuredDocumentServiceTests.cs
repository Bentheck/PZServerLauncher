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
}
