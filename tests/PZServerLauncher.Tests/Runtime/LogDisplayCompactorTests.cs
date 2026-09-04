using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Runtime;

public sealed class LogDisplayCompactorTests
{
    [Fact]
    public void Append_GroupsConsecutiveDuplicateTextureWarnings()
    {
        var compactor = new LogDisplayCompactor();

        var first = compactor.Append("WARN : Sprite > duplicate texture tile_1 ignore ID=1, use ID=2");
        var second = compactor.Append("WARN : Sprite > duplicate texture tile_2 ignore ID=3, use ID=4");
        var third = compactor.Append("WARN : Sprite > duplicate texture tile_3 ignore ID=5, use ID=6");

        Assert.False(first.ReplacePrevious);
        Assert.True(second.ReplacePrevious);
        Assert.True(third.ReplacePrevious);
        Assert.Contains("2 similar warnings hidden", third.DisplayLine);
    }

    [Fact]
    public void Append_DoesNotGroupDistinctActionableWarnings()
    {
        var compactor = new LogDisplayCompactor();

        var first = compactor.Append("WARN : General > Port 16261 is unavailable");
        var second = compactor.Append("WARN : General > Password is missing");

        Assert.False(first.ReplacePrevious);
        Assert.False(second.ReplacePrevious);
    }

    [Fact]
    public void Append_StopsGroupingAfterAnInformationalLine()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("WARN : General > Could not find icon: Item_Dice");
        compactor.Append("LOG  : General > Server started");
        var warningAfterInfo = compactor.Append("WARN : General > Could not find icon: Item_Axe");

        Assert.False(warningAfterInfo.ReplacePrevious);
    }
}
