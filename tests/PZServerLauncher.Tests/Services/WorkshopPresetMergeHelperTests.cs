using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Tests.Services;

public sealed class WorkshopPresetMergeHelperTests
{
    [Fact]
    public void Append_DeduplicatesAndPreservesExistingOrder()
    {
        var preset = new WorkshopPreset
        {
            WorkshopItemIds = ["123", "234"],
            EnabledModIds = ["Alpha"],
            MapFolders = ["RavenCreek"],
        };

        var merged = WorkshopPresetMergeHelper.Append(
            preset,
            ["234", "345"],
            ["alpha", "Beta"],
            ["RavenCreek", "BedfordFalls"]);

        Assert.Equal(["123", "234", "345"], merged.WorkshopItemIds);
        Assert.Equal(["Alpha", "Beta"], merged.EnabledModIds);
        Assert.Equal(["RavenCreek", "BedfordFalls"], merged.MapFolders);
    }

    [Fact]
    public void IsQueued_ReturnsTrueWhenWorkshopModOrMapAlreadyPresent()
    {
        var preset = new WorkshopPreset
        {
            WorkshopItemIds = ["123"],
            EnabledModIds = ["Alpha"],
            MapFolders = ["RavenCreek"],
        };

        Assert.True(WorkshopPresetMergeHelper.IsQueued(preset, "123", [], []));
        Assert.True(WorkshopPresetMergeHelper.IsQueued(preset, "999", ["alpha"], []));
        Assert.True(WorkshopPresetMergeHelper.IsQueued(preset, "999", [], ["ravencreek"]));
        Assert.False(WorkshopPresetMergeHelper.IsQueued(preset, "999", ["Beta"], ["BedfordFalls"]));
    }
}
