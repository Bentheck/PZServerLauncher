using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PZServerLauncher.Host.Data;
using PZServerLauncher.Host.Infrastructure;

namespace PZServerLauncher.Host.Services;

public sealed class DatabaseInitializer(
    AppPaths appPaths,
    ILogger<DatabaseInitializer> logger)
{
    public async Task EnsureReadyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await using var migrationLock = await AcquireLockAsync(cancellationToken);

        var hasExistingDatabase = File.Exists(appPaths.DatabasePath);
        if (hasExistingDatabase)
        {
            File.Copy(appPaths.DatabasePath, appPaths.DatabaseBackupPath, overwrite: true);
        }

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        catch
        {
            logger.LogError("Database migration failed. Attempting to restore {BackupPath}.", appPaths.DatabaseBackupPath);

            await dbContext.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();

            if (hasExistingDatabase && File.Exists(appPaths.DatabaseBackupPath))
            {
                File.Copy(appPaths.DatabaseBackupPath, appPaths.DatabasePath, overwrite: true);
            }

            throw;
        }
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            try
            {
                return new FileStream(
                    appPaths.MigrationLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(250, cancellationToken);
            }
        }
    }
}
