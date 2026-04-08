using PZServerLauncher.Core.Planning;
using PZServerLauncher.Core.Profiles;
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
        Assert.Contains(unstable.Pages, page => page.PageId == "b42.general");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.runtime.memory");
        Assert.Contains(stable.Pages.SelectMany(page => page.Sections).SelectMany(section => section.Fields), field => field.FieldId == "b41.sandbox.zombies");
    }
}
