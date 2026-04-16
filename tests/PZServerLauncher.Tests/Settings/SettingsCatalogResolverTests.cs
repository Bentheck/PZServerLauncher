using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Infrastructure.Settings;

namespace PZServerLauncher.Tests.Settings;

public sealed class SettingsCatalogResolverTests
{
    private readonly ISettingsCatalogResolver _resolver = new ProjectZomboidSettingsCatalogResolver();

    [Fact]
    public void Resolve_ReturnsB42CatalogMetadata()
    {
        var catalog = _resolver.Resolve(ProjectZomboidBranch.Unstable42);

        Assert.Equal("pz.settings.b42", catalog.CatalogId);
        Assert.Equal(4, catalog.CatalogVersion);
        Assert.Equal(ProjectZomboidBranch.Unstable42, catalog.Branch);
        Assert.Contains(catalog.Pages, page => page.PageId == "b42.general");
        Assert.Contains(catalog.Pages, page => page.PageId == "b42.sandbox");
        Assert.Contains(catalog.Pages, page => page.PageId == "b42.mods-and-maps");
        Assert.Contains(catalog.Pages, page => page.PageId == "b42.network-and-admin");
    }

    [Fact]
    public void Resolve_SandboxIncludesCategoryMetadataAndFieldDefaults()
    {
        var sandbox = _resolver.Resolve(ProjectZomboidBranch.Unstable42)
            .Pages
            .Single(page => page.PageId == "b42.sandbox");

        var dayLength = sandbox.Sections.SelectMany(section => section.Fields).Single(field => field.FieldId == "b42.sandbox.day-length");
        Assert.Equal("DayLength", dayLength.Target.KeyPath);
        Assert.Equal(StructuredValueKind.Choice, dayLength.ValueKind);
        Assert.Contains(dayLength.Options ?? [], option => option.Value == "4" && option.Label == "1 Hour, 30 Minutes");
        Assert.Equal("1 Hour, 30 Minutes", dayLength.DefaultValue);

        var zombieSection = sandbox.Sections.First(section => section.SectionId == "b42.sandbox.zombie.basics");
        Assert.Equal("Zombie", zombieSection.CategoryTitle);
        Assert.Equal(2, zombieSection.CategoryOrder);

        var metaMapSection = sandbox.Sections.First(section => section.SectionId == "b42.sandbox.meta.map");
        Assert.Equal("Meta", metaMapSection.CategoryTitle);
        Assert.Equal(6, metaMapSection.CategoryOrder);

        Assert.Contains(
            sandbox.Sections.SelectMany(section => section.Fields),
            field => field.FieldId == "b42.sandbox.allow-world-map" && field.Target.KeyPath == "Map.AllowWorldMap");
        Assert.Contains(
            sandbox.Sections.SelectMany(section => section.Fields),
            field => field.FieldId == "b42.sandbox.xp-global-multiplier" && field.Target.KeyPath == "MultiplierConfig.Global");
        Assert.Contains(
            sandbox.Sections.SelectMany(section => section.Fields),
            field => field.FieldId == "b42.sandbox.world-item-removal-list" && field.DefaultValue!.Contains("Base.Glasses", StringComparison.Ordinal));
        Assert.All(
            _resolver.Resolve(ProjectZomboidBranch.Unstable42)
                .Pages
                .Single(page => page.PageId == "b42.mods-and-maps")
                .Sections
                .SelectMany(section => section.Fields),
            field => Assert.Equal(ConfigFileKind.Ini, field.Target.FileKind));
    }

    [Fact]
    public void Resolve_LegacyBranchValueFallsBackToB42Catalog()
    {
        var catalog = _resolver.Resolve((ProjectZomboidBranch)0);

        Assert.Equal("pz.settings.b42", catalog.CatalogId);
        Assert.Equal(ProjectZomboidBranch.Unstable42, catalog.Branch);
    }
}
