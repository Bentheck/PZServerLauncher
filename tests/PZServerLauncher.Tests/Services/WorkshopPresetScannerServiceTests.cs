using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class WorkshopPresetScannerServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scan_NormalizesWorkshopUrlsAndValidatesLocalContent()
    {
        var installDirectory = Path.Combine(_tempRoot, "install");
        var itemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890", "mods", "ExampleMod");
        Directory.CreateDirectory(itemDirectory);
        File.WriteAllText(Path.Combine(itemDirectory, "mod.info"), "id=ExampleMod\nmap=RavenCreek");
        Directory.CreateDirectory(Path.Combine(itemDirectory, "media", "maps", "RavenCreek"));

        var service = new WorkshopPresetScannerService();
        var result = service.Scan(
            installDirectory,
            new WorkshopPreset
            {
                WorkshopItemIds = ["https://steamcommunity.com/sharedfiles/filedetails/?id=1234567890", "1234567890"],
                EnabledModIds = ["ExampleMod"],
                MapFolders = ["RavenCreek", "MissingMap"],
            });

        Assert.Equal(["1234567890"], result.Preset.WorkshopItemIds);
        Assert.Contains(result.Diagnostics, message => message.Contains("listed more than once", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Diagnostics, message => message.Contains("MissingMap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveWorkshopItemIds_UsesEnabledModAndMapOrderAndKeepsUnknownFallbacks()
    {
        var installDirectory = Path.Combine(_tempRoot, "resolve");
        var firstItemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "1234567890", "mods", "ExampleMod");
        Directory.CreateDirectory(Path.Combine(firstItemDirectory, "media", "maps", "RavenCreek"));
        File.WriteAllText(Path.Combine(firstItemDirectory, "mod.info"), "id=ExampleMod\nmap=RavenCreek");

        var secondItemDirectory = Path.Combine(installDirectory, "steamapps", "workshop", "content", "108600", "2345678901", "mods", "AnotherMod");
        Directory.CreateDirectory(secondItemDirectory);
        File.WriteAllText(Path.Combine(secondItemDirectory, "mod.info"), "id=AnotherMod");

        var service = new WorkshopPresetScannerService();

        var resolved = service.ResolveWorkshopItemIds(
            installDirectory,
            new WorkshopPreset
            {
                EnabledModIds = ["AnotherMod", "ExampleMod"],
                MapFolders = ["RavenCreek"],
            },
            ["9999999999", "1234567890"]);

        Assert.Equal(["2345678901", "1234567890", "9999999999"], resolved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
