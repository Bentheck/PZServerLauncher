using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class BackgroundJobDispatcherTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public BackgroundJobDispatcherTests()
    {
        _databasePath = Path.Combine(_tempRoot, "dispatcher.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task QueueAsync_RejectsConcurrentInstallOrUpdateForSameProfile()
    {
        using var services = BuildServices();
        var dispatcher = services.GetRequiredService<BackgroundJobDispatcher>();
        var releaseJob = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = await dispatcher.QueueAsync(
            OperationJobKind.Install,
            "profile-a",
            "Install profile-a",
            async (_, _, _) => await releaseJob.Task,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.QueueAsync(
                OperationJobKind.Update,
                "profile-a",
                "Update profile-a",
                (_, _, _) => Task.CompletedTask,
                CancellationToken.None));

        Assert.Contains("already running", exception.Message, StringComparison.OrdinalIgnoreCase);
        releaseJob.SetResult();
    }

    private ServiceProvider BuildServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite($"Data Source={_databasePath};Cache=Shared"));
        serviceCollection.AddScoped<JobStore>();
        serviceCollection.AddSingleton<IRuntimeEventPublisher, NullRuntimeEventPublisher>();
        serviceCollection.AddSingleton<BackgroundJobDispatcher>();

        var provider = serviceCollection.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        return provider;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class NullRuntimeEventPublisher : IRuntimeEventPublisher
    {
        public Task PublishStatusChangedAsync(ServerRuntimeStatus status, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishJobChangedAsync(OperationJob job, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishLogLineAsync(string profileId, string line, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PublishLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
