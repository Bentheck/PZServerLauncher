using Microsoft.Data.Sqlite;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class AuditStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public AuditStoreTests()
    {
        _databasePath = Path.Combine(_tempRoot, "audit.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ListAsync_OrdersEntriesInMemoryForSqliteCompatibility()
    {
        await using var dbContext = TestDatabaseFactory.Create(_databasePath);
        dbContext.AuditEntries.AddRange(
            new AuditEntryEntity
            {
                EntryId = Guid.NewGuid(),
                OccurredAtUtc = new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
                Action = "oldest",
                Subject = "host",
                ActorType = "local",
                Detail = "oldest detail",
            },
            new AuditEntryEntity
            {
                EntryId = Guid.NewGuid(),
                OccurredAtUtc = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
                Action = "newest",
                Subject = "host",
                ActorType = "local",
                Detail = "newest detail",
            },
            new AuditEntryEntity
            {
                EntryId = Guid.NewGuid(),
                OccurredAtUtc = new DateTimeOffset(2026, 4, 8, 11, 0, 0, TimeSpan.Zero),
                Action = "middle",
                Subject = "host",
                ActorType = "local",
                Detail = "middle detail",
            });
        await dbContext.SaveChangesAsync();

        var store = new AuditStore(dbContext);

        var entries = await store.ListAsync();

        Assert.Equal(["newest", "middle", "oldest"], entries.Select(entry => entry.Action).ToArray());
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
}
