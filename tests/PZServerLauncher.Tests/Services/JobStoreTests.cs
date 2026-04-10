using Microsoft.Data.Sqlite;
using PZServerLauncher.Host.Data.Entities;
using PZServerLauncher.Host.Services;
using PZServerLauncher.Tests.Testing;

namespace PZServerLauncher.Tests.Services;

public sealed class JobStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public JobStoreTests()
    {
        _databasePath = Path.Combine(_tempRoot, "jobs.db");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task ListRecentAsync_OrdersJobsInMemoryForSqliteCompatibility()
    {
        await using var dbContext = TestDatabaseFactory.Create(_databasePath);
        dbContext.OperationJobs.AddRange(
            new OperationJobEntity
            {
                JobId = Guid.NewGuid(),
                Kind = 1,
                Status = 0,
                Summary = "oldest",
                ProgressPercent = 0,
                CreatedAtUtc = new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            },
            new OperationJobEntity
            {
                JobId = Guid.NewGuid(),
                Kind = 1,
                Status = 0,
                Summary = "newest",
                ProgressPercent = 0,
                CreatedAtUtc = new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            },
            new OperationJobEntity
            {
                JobId = Guid.NewGuid(),
                Kind = 1,
                Status = 0,
                Summary = "middle",
                ProgressPercent = 0,
                CreatedAtUtc = new DateTimeOffset(2026, 4, 8, 11, 0, 0, TimeSpan.Zero),
            });
        await dbContext.SaveChangesAsync();

        var store = new JobStore(dbContext);

        var jobs = await store.ListRecentAsync();

        Assert.Equal(["newest", "middle", "oldest"], jobs.Select(job => job.Summary).ToArray());
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
