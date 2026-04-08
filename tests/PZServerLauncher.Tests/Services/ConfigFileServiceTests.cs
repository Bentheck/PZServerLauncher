using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Tests.Services;

public sealed class ConfigFileServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetCommonConfig_AndApplyCommonConfig_RoundTripProfileBackedFields()
    {
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            ServerName = "alpha-server",
            DefaultPort = 16280,
            UdpPort = 16281,
            RconPort = 27025,
            BindIp = "10.0.0.25",
            AdminUsername = "alpha-admin",
            PreferredMemoryInGigabytes = 12,
            StartWithHost = true,
            AutoRestartOnCrash = false,
        };

        var service = new ConfigFileService(new ProjectZomboidServerPlanner());
        var common = service.GetCommonConfig(profile);

        Assert.Equal(profile.ServerName, common.ServerName);
        Assert.Equal(profile.DefaultPort, common.DefaultPort);
        Assert.Equal(profile.UdpPort, common.UdpPort);
        Assert.Equal(profile.RconPort, common.RconPort);
        Assert.Equal(profile.BindIp, common.BindIp);
        Assert.Equal(profile.AdminUsername, common.AdminUsername);
        Assert.Equal(profile.PreferredMemoryInGigabytes, common.PreferredMemoryInGigabytes);
        Assert.Equal(profile.StartWithHost, common.StartWithHost);
        Assert.Equal(profile.AutoRestartOnCrash, common.AutoRestartOnCrash);

        var updatedProfile = service.ApplyCommonConfig(profile, common with
        {
            ServerName = "beta-server",
            DefaultPort = 16290,
            UdpPort = 16291,
            RconPort = 27035,
            BindIp = "10.0.0.30",
            AdminUsername = "beta-admin",
            PreferredMemoryInGigabytes = 16,
            StartWithHost = false,
            AutoRestartOnCrash = true,
        });

        Assert.Equal("beta-server", updatedProfile.ServerName);
        Assert.Equal(16290, updatedProfile.DefaultPort);
        Assert.Equal(16291, updatedProfile.UdpPort);
        Assert.Equal(27035, updatedProfile.RconPort);
        Assert.Equal("10.0.0.30", updatedProfile.BindIp);
        Assert.Equal("beta-admin", updatedProfile.AdminUsername);
        Assert.Equal(16, updatedProfile.PreferredMemoryInGigabytes);
        Assert.False(updatedProfile.StartWithHost);
        Assert.True(updatedProfile.AutoRestartOnCrash);
        Assert.True(updatedProfile.UpdatedAtUtc > profile.UpdatedAtUtc);
    }

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

    [Fact]
    public void ReadRawFile_ReturnsDiagnosticsForMalformedIni()
    {
        Directory.CreateDirectory(_tempRoot);
        var profile = ServerProfileFactory.CreateStarterProfile() with
        {
            CacheDirectory = Path.Combine(_tempRoot, "cache"),
            ServerName = "diagnostics-test",
        };

        var planner = new ProjectZomboidServerPlanner();
        var paths = planner.ResolvePaths(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.IniFilePath)!);
        File.WriteAllText(paths.IniFilePath, "Public=true\nMalformedLine\nMaxPlayers=32");

        var service = new ConfigFileService(planner);
        var result = service.ReadRawFile(profile, ConfigFileKind.Ini);

        Assert.Contains(result.Diagnostics, message => message.Contains("Line 2", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
