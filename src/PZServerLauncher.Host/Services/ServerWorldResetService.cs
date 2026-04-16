using System.Globalization;
using System.Security.Cryptography;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Core.Settings;
using PZServerLauncher.Infrastructure.Planning;

namespace PZServerLauncher.Host.Services;

public sealed class ServerWorldResetService(
    ProfileStore profileStore,
    ProjectZomboidServerPlanner planner,
    ConfigFileService configFileService,
    IIniDocumentService iniDocumentService,
    ServerBackupService backupService,
    AuditStore auditStore)
{
    private const string SeedAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public async Task<ServerWorldResetResult> ResetWorldAsync(
        string profileId,
        bool createBackupBeforeReset,
        CancellationToken cancellationToken)
    {
        var profile = await profileStore.GetAsync(profileId, cancellationToken)
            ?? throw new InvalidOperationException($"Profile '{profileId}' was not found.");

        string? backupFileName = null;
        if (createBackupBeforeReset)
        {
            var backupPath = await backupService.CreateBackupAsync(profileId, BackupTrigger.Manual, cancellationToken);
            backupFileName = Path.GetFileName(backupPath);
        }

        var paths = planner.ResolvePaths(profile);
        var worldDirectoryExisted = Directory.Exists(paths.WorldDirectory);
        if (worldDirectoryExisted)
        {
            Directory.Delete(paths.WorldDirectory, recursive: true);
        }

        var iniUpdate = TryUpdateIniResetMarkers(profile);

        var detail = BuildAuditDetail(profile, paths.WorldDirectory, worldDirectoryExisted, backupFileName, iniUpdate);
        await auditStore.WriteAsync("world.reset", profileId, "local", detail, cancellationToken: cancellationToken);

        return new ServerWorldResetResult(
            worldDirectoryExisted,
            paths.WorldDirectory,
            backupFileName,
            iniUpdate.ResetId,
            iniUpdate.Seed,
            iniUpdate.UpdatedIni);
    }

    public static string BuildUserMessage(ServerWorldResetResult result, bool restartAfterReset)
    {
        var pieces = new List<string>
        {
            result.WorldDirectoryExisted
                ? "Deleted the current multiplayer world."
                : "No existing multiplayer world was present, so the next start will generate a fresh one.",
        };

        if (!string.IsNullOrWhiteSpace(result.BackupFileName))
        {
            pieces.Add($"Manual backup created: {result.BackupFileName}.");
        }

        if (result.UpdatedIni)
        {
            pieces.Add($"ResetID updated to {result.ResetId}.");
            pieces.Add($"Seed randomized to {result.Seed}.");
        }
        else
        {
            pieces.Add("World reset completed, but launcher could not update ResetID/Seed because the server INI was missing or unsupported.");
        }

        pieces.Add(restartAfterReset
            ? "The server was started again with a fresh world."
            : "Start the server when you are ready to generate the new world.");

        return string.Join(" ", pieces);
    }

    private ServerIniResetUpdate TryUpdateIniResetMarkers(PZServerLauncher.Core.Profiles.ServerProfile profile)
    {
        var raw = configFileService.ReadRawFile(profile, ConfigFileKind.Ini);
        if (string.IsNullOrWhiteSpace(raw.Content))
        {
            return ServerIniResetUpdate.None;
        }

        var parsed = iniDocumentService.Parse(raw.Content);
        if (!parsed.IsSupported)
        {
            return ServerIniResetUpdate.None;
        }

        var values = iniDocumentService.ReadValues(raw.Content, ["ResetID", "Seed"]);
        var resetId = ComputeNextResetId(values.TryGetValue("ResetID", out var currentResetId) ? currentResetId : null);
        var seed = GenerateSeed();
        var updatedContent = iniDocumentService.ApplyValues(raw.Content, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ResetID"] = resetId.ToString(CultureInfo.InvariantCulture),
            ["Seed"] = seed,
        });

        configFileService.WriteRawFile(profile, ConfigFileKind.Ini, raw.Sha256, updatedContent);
        return new ServerIniResetUpdate(true, resetId, seed);
    }

    private static int ComputeNextResetId(string? currentValue)
    {
        if (int.TryParse(currentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed >= 0 &&
            parsed < int.MaxValue)
        {
            return parsed + 1;
        }

        return RandomNumberGenerator.GetInt32(1, int.MaxValue);
    }

    private static string GenerateSeed()
    {
        Span<char> buffer = stackalloc char[16];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = SeedAlphabet[RandomNumberGenerator.GetInt32(SeedAlphabet.Length)];
        }

        return new string(buffer);
    }

    private static string BuildAuditDetail(
        PZServerLauncher.Core.Profiles.ServerProfile profile,
        string worldDirectory,
        bool worldDirectoryExisted,
        string? backupFileName,
        ServerIniResetUpdate iniUpdate)
    {
        var pieces = new List<string>
        {
            worldDirectoryExisted
                ? $"Deleted world directory '{worldDirectory}' for {profile.DisplayName}."
                : $"No world directory existed at '{worldDirectory}' for {profile.DisplayName}; next start will still generate a fresh world.",
        };

        if (!string.IsNullOrWhiteSpace(backupFileName))
        {
            pieces.Add($"Manual backup {backupFileName} was captured before reset.");
        }

        if (iniUpdate.UpdatedIni)
        {
            pieces.Add($"ResetID set to {iniUpdate.ResetId} and Seed randomized to {iniUpdate.Seed}.");
        }
        else
        {
            pieces.Add("ResetID and Seed could not be updated because the INI was missing or unsupported.");
        }

        return string.Join(" ", pieces);
    }

    public sealed record ServerWorldResetResult(
        bool WorldDirectoryExisted,
        string WorldDirectory,
        string? BackupFileName,
        int? ResetId,
        string? Seed,
        bool UpdatedIni);

    private sealed record ServerIniResetUpdate(
        bool UpdatedIni,
        int? ResetId,
        string? Seed)
    {
        public static ServerIniResetUpdate None { get; } = new(false, null, null);
    }
}
