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
            await EnsureModsMapsDraftSchemaAsync(dbContext, cancellationToken);
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

    private static async Task EnsureModsMapsDraftSchemaAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ModsMapsDrafts (
                ProfileId TEXT NOT NULL PRIMARY KEY,
                Branch INTEGER NOT NULL,
                WorkshopItemIdsJson TEXT NOT NULL DEFAULT '[]',
                EditorMode TEXT NOT NULL DEFAULT 'Browse',
                IsDirty INTEGER NOT NULL DEFAULT 0,
                UpdatedAtUtc TEXT NOT NULL
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ModsMapsDraftModRows (
                ProfileId TEXT NOT NULL,
                RowId INTEGER NOT NULL,
                ModName TEXT NOT NULL DEFAULT '',
                ModId TEXT NOT NULL DEFAULT '',
                WorkshopId TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                DependencyModIdsJson TEXT NOT NULL DEFAULT '[]',
                MapFoldersJson TEXT NOT NULL DEFAULT '[]',
                PRIMARY KEY (ProfileId, RowId)
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ModsMapsDraftMapRows (
                ProfileId TEXT NOT NULL,
                RowId INTEGER NOT NULL,
                Title TEXT NOT NULL DEFAULT '',
                MapFolder TEXT NOT NULL DEFAULT '',
                WorkshopId TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (ProfileId, RowId)
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ModsMapsDrafts_UpdatedAtUtc ON ModsMapsDrafts (UpdatedAtUtc);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ModsMapsDraftModRows_ProfileId_SortOrder ON ModsMapsDraftModRows (ProfileId, SortOrder);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ModsMapsDraftModRows_ModId ON ModsMapsDraftModRows (ModId);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ModsMapsDraftMapRows_ProfileId_SortOrder ON ModsMapsDraftMapRows (ProfileId, SortOrder);",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ModsMapsDraftMapRows_MapFolder ON ModsMapsDraftMapRows (MapFolder);",
            cancellationToken);
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
