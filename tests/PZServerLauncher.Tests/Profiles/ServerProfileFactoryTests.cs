using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.Tests.Profiles;

public sealed class ServerProfileFactoryTests
{
    [Fact]
    public void CreateStarterProfile_UsesPerProfileInstallAndCacheDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
        var profile = ServerProfileFactory.CreateStarterProfile("West Point PvE", 16261, [], root);

        Assert.Equal("west-point-pve", profile.ProfileId);
        Assert.Equal(Path.Combine(root, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.InstallFolderName, "west-point-pve"), profile.InstallDirectory);
        Assert.Equal(Path.Combine(root, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.ProfilesFolderName, "west-point-pve"), profile.CacheDirectory);
    }

    [Fact]
    public void CreateStarterProfile_WhenProfileIdCollides_KeepsDirectoriesUnique()
    {
        var root = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
        var profile = ServerProfileFactory.CreateStarterProfile("Main Server", 16261, ["main-server"], root);

        Assert.Equal("main-server-2", profile.ProfileId);
        Assert.Equal(Path.Combine(root, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.InstallFolderName, "main-server-2"), profile.InstallDirectory);
        Assert.Equal(Path.Combine(root, ServerProfileFactory.ManagedServersFolderName, ServerProfileFactory.ProfilesFolderName, "main-server-2"), profile.CacheDirectory);
    }

    [Fact]
    public void FindNextAvailableStarterPort_SkipsReservedThreePortSets()
    {
        var reservedPorts = new[] { 16261, 16262, 16263 };

        var nextAvailable = ServerProfileFactory.FindNextAvailableStarterPort(16261, reservedPorts);

        Assert.Equal(16264, nextAvailable);
    }

    [Fact]
    public void FindConflictingStarterPort_DetectsOverlapAgainstExistingPorts()
    {
        var reservedPorts = new[] { 16263, 17000 };

        var conflict = ServerProfileFactory.FindConflictingStarterPort(16261, reservedPorts);

        Assert.Equal(16263, conflict);
    }

    [Fact]
    public void CreateStarterProfile_UsesRequestedPreferredMemory()
    {
        var profile = ServerProfileFactory.CreateStarterProfile(
            "Riverside Co-op",
            16261,
            [],
            preferredMemoryInGigabytes: 10);

        Assert.Equal(10, profile.PreferredMemoryInGigabytes);
    }
}
