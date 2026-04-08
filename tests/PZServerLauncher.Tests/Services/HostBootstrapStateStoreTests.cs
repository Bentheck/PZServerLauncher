using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Tests.Services;

public sealed class HostBootstrapStateStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_CreatesProtectedStateAndPersistsRemoteSettings()
    {
        var appPaths = new AppPaths(_tempRoot);
        var store = new HostBootstrapStateStore(appPaths);

        var initial = await store.LoadAsync();
        var apiToken = await store.GetLocalApiTokenAsync();

        Assert.InRange(initial.LoopbackPort, 48231, 48239);
        Assert.False(string.IsNullOrWhiteSpace(initial.ProtectedLocalApiToken));
        Assert.False(string.IsNullOrWhiteSpace(apiToken));
        Assert.True(File.Exists(appPaths.HostStatePath));

        var updated = await store.UpdateRemoteAccessAsync(
            new RemoteAccessSettings
            {
                IsEnabled = true,
                BindAddress = "192.168.1.40",
                HttpsPort = 8443,
                PublicHostname = "pz.example.test",
                CertificatePath = @"C:\certs\pzserver.pfx",
                CreateFirewallRule = true,
            },
            "top-secret");

        Assert.True(updated.RemoteAccessEnabled);
        Assert.Equal("192.168.1.40", updated.RemoteBindAddress);
        Assert.Equal("top-secret", store.GetCertificatePassword(updated));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
