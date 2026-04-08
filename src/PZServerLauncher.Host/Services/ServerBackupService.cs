using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PZServerLauncher.Core.Profiles;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Infrastructure;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ServerBackupService(
    AppPaths appPaths,
    ProfileStore profileStore,
    ProjectZomboidServerPlanner planner,
    AuditStore auditStore)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task<string> CreateBackupAsync(string profileId, BackupTrigger trigger, CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        var paths = planner.ResolvePaths(profile);
        var backupDirectory = Path.Combine(appPaths.BackupsDirectory, profileId);
        Directory.CreateDirectory(backupDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(backupDirectory, $"{profileId}-{trigger.ToString().ToLowerInvariant()}-{timestamp}.zip");
        var entries = new List<BackupManifestEntry>();

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            await AddStringEntryAsync(
                archive,
                "profile.json",
                JsonSerializer.Serialize(profile, SerializerOptions),
                entries,
                cancellationToken);

            AddDirectoryIfExists(archive, paths.ServerConfigDirectory, "Server", entries);
            AddDirectoryIfExists(archive, paths.WorldDirectory, Path.Combine("Saves", "Multiplayer", profile.ServerName), entries);
            AddDirectoryIfExists(archive, Path.Combine(profile.CacheDirectory, "db"), "db", entries);

            var manifest = new BackupManifest(
                profile.ProfileId,
                profile.ServerName,
                trigger,
                DateTimeOffset.UtcNow,
                entries);

            await AddStringEntryAsync(
                archive,
                "manifest.json",
                JsonSerializer.Serialize(manifest, SerializerOptions),
                entries: null,
                cancellationToken);
        }

        ApplyRetentionPolicy(profile, trigger);
        await auditStore.WriteAsync("backup.created", profileId, "local", $"Created backup {Path.GetFileName(zipPath)}.", cancellationToken: cancellationToken);
        return zipPath;
    }

    public async Task RestoreBackupAsync(string profileId, string backupFileName, CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        var zipPath = Path.Combine(appPaths.BackupsDirectory, profileId, Path.GetFileName(backupFileName));
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup archive was not found.", zipPath);
        }

        await ValidateArchiveAsync(zipPath, cancellationToken);

        var paths = planner.ResolvePaths(profile);
        var tempRestoreDirectory = Path.Combine(appPaths.RuntimeDirectory, "restore", profileId, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRestoreDirectory);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempRestoreDirectory);

            ReplaceDirectory(Path.Combine(tempRestoreDirectory, "Server"), paths.ServerConfigDirectory);
            ReplaceDirectory(Path.Combine(tempRestoreDirectory, "Saves", "Multiplayer", profile.ServerName), paths.WorldDirectory);
            ReplaceDirectory(Path.Combine(tempRestoreDirectory, "db"), Path.Combine(profile.CacheDirectory, "db"));
        }
        finally
        {
            if (Directory.Exists(tempRestoreDirectory))
            {
                Directory.Delete(tempRestoreDirectory, recursive: true);
            }
        }

        await auditStore.WriteAsync("backup.restored", profileId, "local", $"Restored backup {backupFileName}.", cancellationToken: cancellationToken);
    }

    public IReadOnlyList<string> ListBackups(string profileId)
    {
        var directory = Path.Combine(appPaths.BackupsDirectory, profileId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, "*.zip")
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>()
            .OrderByDescending(x => x)
            .ToList();
    }

    private static void AddDirectoryIfExists(
        ZipArchive archive,
        string sourceDirectory,
        string entryRoot,
        ICollection<BackupManifestEntry> entries)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var archivePath = Path.Combine(entryRoot, relative).Replace('\\', '/');
            archive.CreateEntryFromFile(file, archivePath);
            entries.Add(new BackupManifestEntry(archivePath, ComputeFileSha256(file), new FileInfo(file).Length));
        }
    }

    private static async Task AddStringEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        ICollection<BackupManifestEntry>? entries,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);

        if (entries is not null)
        {
            entries.Add(new BackupManifestEntry(entryName.Replace('\\', '/'), ComputeContentSha256(content), content.Length));
        }
    }

    private void ApplyRetentionPolicy(ServerProfile profile, BackupTrigger trigger)
    {
        if (trigger == BackupTrigger.Manual || trigger == BackupTrigger.Scheduled && !profile.BackupPolicy.ScheduledBackupsEnabled)
        {
            return;
        }

        var directory = Path.Combine(appPaths.BackupsDirectory, profile.ProfileId);
        if (!Directory.Exists(directory))
        {
            return;
        }

        var pattern = trigger switch
        {
            BackupTrigger.PreUpdate => $"{profile.ProfileId}-preupdate-*.zip",
            BackupTrigger.Scheduled => $"{profile.ProfileId}-scheduled-*.zip",
            _ => null,
        };

        if (pattern is null)
        {
            return;
        }

        var retentionCount = trigger switch
        {
            BackupTrigger.PreUpdate => profile.BackupPolicy.PreUpdateBackupRetentionCount,
            BackupTrigger.Scheduled => profile.BackupPolicy.ScheduledBackupRetentionCount,
            _ => int.MaxValue,
        };

        if (retentionCount < 0)
        {
            retentionCount = 0;
        }

        foreach (var staleBackup in Directory.GetFiles(directory, pattern)
                     .OrderByDescending(path => path)
                     .Skip(retentionCount))
        {
            File.Delete(staleBackup);
        }
    }

    private static async Task ValidateArchiveAsync(string zipPath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("Backup manifest is missing.");

        BackupManifest manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = (await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, SerializerOptions, cancellationToken))
                ?? throw new InvalidOperationException("Backup manifest could not be read.");
        }

        foreach (var entry in manifest.Entries)
        {
            var archiveEntry = archive.GetEntry(entry.ArchivePath.Replace('\\', '/'))
                ?? throw new InvalidOperationException($"Backup entry '{entry.ArchivePath}' is missing from the archive.");

            await using var entryStream = archiveEntry.Open();
            var actualHash = await ComputeSha256Async(entryStream, cancellationToken);
            if (!string.Equals(actualHash, entry.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Backup entry '{entry.ArchivePath}' failed checksum validation.");
            }
        }
    }

    private static void ReplaceDirectory(string sourceDirectory, string targetDirectory)
    {
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, recursive: true);
        }

        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return ComputeStreamSha256(stream);
    }

    private static string ComputeContentSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)));

    private static string ComputeStreamSha256(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private sealed record BackupManifest(
        string ProfileId,
        string ServerName,
        BackupTrigger Trigger,
        DateTimeOffset CreatedAtUtc,
        IReadOnlyList<BackupManifestEntry> Entries);

    private sealed record BackupManifestEntry(
        string ArchivePath,
        string Sha256,
        long Size);
}
