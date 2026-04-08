using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class SettingsCatalogResolverTests
{
    private readonly ISettingsCatalogResolver _resolver = new ProjectZomboidSettingsCatalogResolver();

    [Fact]
    public void Resolve_ReturnsBranchSpecificCatalogMetadata()
    {
        var stable = _resolver.Resolve(ProjectZomboidBranch.Stable41);
        var unstable = _resolver.Resolve(ProjectZomboidBranch.Unstable42);

        Assert.Equal("pz.settings.b41", stable.CatalogId);
        Assert.Equal("pz.settings.b42", unstable.CatalogId);
        Assert.Equal(ProjectZomboidBranch.Stable41, stable.Branch);
        Assert.Equal(ProjectZomboidBranch.Unstable42, unstable.Branch);
        Assert.Contains(stable.Pages, page => page.PageId == "b41.general");
        Assert.Contains(stable.Pages, page => page.PageId == "b41.sandbox");
        Assert.Contains(stable.Pages, page => page.PageId == "b41.mods-and-maps");
        Assert.Contains(stable.Pages, page => page.PageId == "b41.network-and-admin");
        Assert.Contains(unstable.Pages, page => page.PageId == "b42.general");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.runtime.memory");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.server.sleep-allowed");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.server.player-safehouse");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.server.faction-enabled");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombies");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.erosion-speed");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.food-rot-speed");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.alarm");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.helicopter");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombie-lore-speed" && field.Target.KeyPath == "ZombieLore.Speed");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombie-lore-transmission" && field.Target.KeyPath == "ZombieLore.Transmission");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombie-lore-hearing" && field.Target.KeyPath == "ZombieLore.Hearing");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombie-lore-trigger-house-alarm" && field.Target.KeyPath == "ZombieLore.TriggerHouseAlarm");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombie-lore-thump-no-chasing" && field.Target.KeyPath == "ZombieLore.ThumpNoChasing");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.multi-hit");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.fire-spread");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.enable-vehicles");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.steam-vac");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.kick-fast-players");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.display-user-name");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.show-first-last-name");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.safety-system");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.safety-toggle-timer");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.safety-cooldown-timer");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.voice-enabled");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.voice-3d");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.voice-min-distance");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.voice-max-distance");
        Assert.All(
            stable.Pages.Single(page => page.PageId == "b41.mods-and-maps").Sections.SelectMany(section => section.Fields),
            field => Assert.Equal(ConfigFileKind.Ini, field.Target.FileKind));
        Assert.Contains(
            stable.Pages.Single(page => page.PageId == "b41.mods-and-maps").Sections.SelectMany(section => section.Fields),
            field => field.FieldId == "b41.mods.map-folders" && field.Target.KeyPath == "Map");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.network.bind-ip");
    }
}
