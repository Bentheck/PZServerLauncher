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

    [Theory]
    [InlineData("WARN : General > Could not find icon: Item_Dice")]
    [InlineData("WARN : Script > no such mesh \"vehicles/example\" for Base.Example")]
    [InlineData("WARN : Script > NO SUCH MESH \"vehicles/example\" for Base.Example")]
    [InlineData("ERROR: General > Could not find bone index for node name: \"Bip01_Root\"")]
    [InlineData("ERROR: General > Cannot find bone index for node name: \"Bip01_Root\"")]
    [InlineData("java.base/sun.nio.fs.WindowsException.translateToIOException(Unknown Source)")]
    [InlineData("java.base/java.nio.file.Files.readAttributes(Unknown Source)")]
    [InlineData("zombie.core.skinnedmodel.advancedanimation.AdvancedAnimator.load(AdvancedAnimator.java:928)")]
    [InlineData("ERROR: General f:0 st:123 > A general runtime error")]
    [InlineData("    Stack trace:")]
    public void Append_DiscardsKnownRuntimeNoise(string line)
    {
        var compacted = new LogDisplayCompactor().Append(line);

        Assert.True(compacted.Suppress);
        Assert.Empty(compacted.DisplayLine);
        Assert.True(LogDisplayCompactor.IsDiscardedNoise(line));
    }

    [Fact]
    public void Append_PreservesApplicationMessagesThatOnlyContainStackTraceAsPartOfAName()
    {
        var compacted = new LogDisplayCompactor().Append("LOG : General > ISUIStackTrace: Warning");

        Assert.False(compacted.Suppress);
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

    [Fact]
    public void Append_SuppressesKnownAssetWarningFamilyAfterUnrelatedLogLine()
    {
        var compactor = new LogDisplayCompactor();

        var first = compactor.Append("WARN : Sprite > duplicate texture tile_1 ignore ID=1, use ID=2");
        compactor.Append("LOG  : General > Loading another mod");
        var repeated = compactor.Append("WARN : Sprite > duplicate texture tile_2 ignore ID=3, use ID=4");

        Assert.False(first.Suppress);
        Assert.True(repeated.Suppress);
    }

    [Fact]
    public void Append_SuppressesAlternatingKnownAssetWarningFamilies()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("WARN : Sprite > duplicate texture tile_1 ignore ID=1, use ID=2");
        compactor.Append("WARN : Script > Could not find icon: Item_Dice");
        var repeatedTexture = compactor.Append("WARN : Sprite > duplicate texture tile_2 ignore ID=3, use ID=4");
        var repeatedIcon = compactor.Append("WARN : Script > Could not find icon: Item_Axe");

        Assert.True(repeatedTexture.ReplacePrevious);
        Assert.True(repeatedIcon.Suppress);
    }

    [Fact]
    public void Reset_AllowsKnownAssetWarningSummaryToAppearAgain()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("WARN : Sprite > duplicate texture tile_1 ignore ID=1, use ID=2");
        compactor.Reset();
        var afterReset = compactor.Append("WARN : Sprite > duplicate texture tile_2 ignore ID=3, use ID=4");

        Assert.False(afterReset.Suppress);
    }

    [Fact]
    public void Append_ShowsOneOverrideProgressNoticeAndSuppressesTheRest()
    {
        var compactor = new LogDisplayCompactor();

        var first = compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/lua/shared/translate/en/ui.json");
        var second = compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/lua/shared/translate/fr/ui.json");

        Assert.False(first.ReplacePrevious);
        Assert.False(first.Suppress);
        Assert.Equal("Mods overrides in progress…", first.DisplayLine);
        Assert.True(second.Suppress);
    }

    [Fact]
    public void Append_SuppressesOverrideMessagesFromOtherModsToo()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/tilegeometry.txt");
        var nextMod = compactor.Append("LOG  : Mod > mod \"ModManager\" overrides media/lua/shared/translate/en/ui.json");

        Assert.True(nextMod.Suppress);
    }

    [Fact]
    public void Append_SuppressesRepeatedOverrideBlockForSameModAfterInterruption()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/tilegeometry.txt");
        compactor.Append("LOG  : General > Loading scripts");
        var repeated = compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/tiledepthtextureassignments.txt");

        Assert.True(repeated.Suppress);
    }

    [Fact]
    public void Reset_AllowsOverrideProgressNoticeToAppearAgain()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/tilegeometry.txt");
        compactor.Reset();
        var afterReset = compactor.Append("LOG  : Mod > mod \"Horse\" overrides media/tiledepthtextureassignments.txt");

        Assert.False(afterReset.Suppress);
        Assert.Equal("Mods overrides in progress…", afterReset.DisplayLine);
    }

    [Fact]
    public void Append_CompactsAlternatingWorkshopPendingAndProgressLines()
    {
        var compactor = new LogDisplayCompactor();

        var pending = compactor.Append("LOG : General > Workshop: DownloadPending GetItemState()=NeedsUpdate|DownloadPending ID=3119788162");
        var zeroProgress = compactor.Append("LOG : General > Workshop: download 0/229876960 ID=3119788162");
        var progress = compactor.Append("LOG : General > Workshop: download 114938480/229876960 ID=3119788162");

        Assert.False(pending.ReplacePrevious);
        Assert.Equal("Workshop item 3119788162: download pending", pending.DisplayLine);
        Assert.True(zeroProgress.ReplacePrevious);
        Assert.True(progress.ReplacePrevious);
        Assert.Contains("50.0% downloaded", progress.DisplayLine);
        Assert.Contains("2 repetitive updates compacted", progress.DisplayLine);
    }

    [Fact]
    public void Append_SuppressesWorkshopUpdatesWithinTheSameFivePercentBucket()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG : General > Workshop: download 100/10000 ID=3119788162");
        var pendingPoll = compactor.Append("LOG : General > Workshop: DownloadPending GetItemState()=NeedsUpdate|Downloading|DownloadPending ID=3119788162");
        var smallProgress = compactor.Append("LOG : General > Workshop: download 499/10000 ID=3119788162");
        var meaningfulProgress = compactor.Append("LOG : General > Workshop: download 500/10000 ID=3119788162");

        Assert.True(pendingPoll.Suppress);
        Assert.True(smallProgress.Suppress);
        Assert.False(meaningfulProgress.Suppress);
        Assert.True(meaningfulProgress.ReplacePrevious);
        Assert.Contains("5.0% downloaded", meaningfulProgress.DisplayLine);
    }

    [Fact]
    public void Append_StartsANewSummaryForAnotherWorkshopItem()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG : General > Workshop: download 50/100 ID=3119788162");
        var nextItem = compactor.Append("LOG : General > Workshop: DownloadPending GetItemState()=NeedsUpdate|DownloadPending ID=2896255721");

        Assert.False(nextItem.ReplacePrevious);
        Assert.Contains("2896255721", nextItem.DisplayLine);
    }

    [Fact]
    public void Append_StopsWorkshopCompactionAfterAnotherLogLine()
    {
        var compactor = new LogDisplayCompactor();

        compactor.Append("LOG : General > Workshop: download 50/100 ID=3119788162");
        compactor.Append("LOG : General > Connected to Steam servers");
        var resumed = compactor.Append("LOG : General > Workshop: download 75/100 ID=3119788162");

        Assert.False(resumed.ReplacePrevious);
    }
}
