using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Tests.Services;

public sealed class ConfigFileServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteRawFile_RejectsStaleShaValues()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            ServerName = "sha-test",
        };

        var service = new ConfigFileService(new ProjectZomboidServerPlanner());
        var initial = service.ReadRawFile(profile, ConfigFileKind.Ini);
        var saved = service.WriteRawFile(profile, ConfigFileKind.Ini, initial.Sha256, "first-version=true");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.WriteRawFile(profile, ConfigFileKind.Ini, initial.Sha256, "second-version=true"));

        Assert.Contains("changed since it was last read", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(initial.Sha256, saved.Sha256);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
