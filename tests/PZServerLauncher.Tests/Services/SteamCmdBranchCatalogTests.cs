using PZServerLauncher.Runtime.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class SteamCmdBranchCatalogTests
{
    [Fact]
    public void ParseBranchCatalog_ReturnsAdvertisedBranchesAndMetadata()
    {
        string[] output =
        [
            "\"branches\"",
            "{",
            "  \"public\"",
            "  {",
            "    \"buildid\"  \"24909836\"",
            "  }",
            "  \"legacy41\"",
            "  {",
            "    \"buildid\"  \"24928750\"",
            "    \"description\"  \"Build 41.78.21\"",
            "  }",
            "  \"private-test\"",
            "  {",
            "    \"buildid\"  \"123\"",
            "    \"pwdrequired\"  \"1\"",
            "  }",
            "}",
        ];

        var branches = SteamCmdToolService.ParseBranchCatalog(output);

        Assert.Equal(3, branches.Count);
        Assert.Equal("public", branches[0].Name);
        Assert.Equal("Latest stable (public)", branches[0].DisplayName);
        Assert.Equal("24909836", branches[0].BuildId);
        Assert.Contains(branches, branch => branch.Name == "legacy41" && branch.DisplayName == "Build 41.78.21 (legacy41)");
        Assert.Contains(branches, branch => branch.Name == "private-test" && branch.RequiresPassword);
    }
}
