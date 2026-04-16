using System.Text;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Core.Profiles;

public static class ServerProfileFactory
{
    public const int DefaultStarterPort = 16261;
    public const int DefaultPreferredMemoryInGigabytes = 6;
    public const int DefaultMaxPlayers = 16;
    public const int MinStarterPort = 1024;
    public const int MaxStarterPort = 65533;
    public const string ManagedServersFolderName = "PZServers";
    public const string InstallFolderName = "Installs";
    public const string ProfilesFolderName = "Profiles";

    private const int UdpPortOffset = 1;
    private const int RconPortOffset = 2;
    private const string DefaultDisplayName = "Main Server";

    public static ServerProfile CreateStarterProfile() =>
        CreateStarterProfile(DefaultDisplayName, DefaultStarterPort, []);

    public static ServerProfile CreateStarterProfile(
        string displayName,
        int defaultPort,
        IEnumerable<string> existingProfileIds,
        string? baseDirectory = null,
        int preferredMemoryInGigabytes = DefaultPreferredMemoryInGigabytes)
    {
        var trimmedDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? DefaultDisplayName
            : displayName.Trim();
        var validatedPort = IsValidStarterPort(defaultPort)
            ? defaultPort
            : DefaultStarterPort;
        var validatedMemory = preferredMemoryInGigabytes > 0
            ? preferredMemoryInGigabytes
            : DefaultPreferredMemoryInGigabytes;
        var baseProfileId = Slugify(trimmedDisplayName);
        var profileId = EnsureUniqueProfileId(baseProfileId, existingProfileIds);

        return new ServerProfile
        {
            ProfileId = profileId,
            DisplayName = trimmedDisplayName,
            ServerName = profileId,
            InstallDirectory = BuildInstallDirectory(profileId, baseDirectory),
            CacheDirectory = BuildCacheDirectory(profileId, baseDirectory),
            Branch = ProjectZomboidBranch.Unstable42,
            DefaultPort = validatedPort,
            UdpPort = validatedPort + UdpPortOffset,
            RconPort = validatedPort + RconPortOffset,
            UseSteam = true,
            AdminUsername = "admin",
            AdminPassword = "change-me-before-first-run",
            PreferredMemoryInGigabytes = validatedMemory,
            StartWithHost = false,
            AutoRestartOnCrash = true,
            WorkshopPreset = WorkshopPreset.Empty,
            BackupPolicy = BackupPolicy.Default,
        };
    }

    public static bool IsValidStarterPort(int port) =>
        port >= MinStarterPort && port <= MaxStarterPort;

    public static IReadOnlyList<int> BuildReservedPorts(int defaultPort) =>
        [defaultPort, defaultPort + UdpPortOffset, defaultPort + RconPortOffset];

    public static int? FindConflictingStarterPort(int defaultPort, IEnumerable<int> reservedPorts)
    {
        if (!IsValidStarterPort(defaultPort))
        {
            return null;
        }

        var reservedPortSet = reservedPorts as ISet<int> ?? reservedPorts.ToHashSet();
        return FindConflictingStarterPort(defaultPort, reservedPortSet);
    }

    public static int FindNextAvailableStarterPort(int preferredPort, IEnumerable<int> reservedPorts)
    {
        var reservedPortSet = reservedPorts as ISet<int> ?? reservedPorts.ToHashSet();
        var candidate = IsValidStarterPort(preferredPort)
            ? preferredPort
            : DefaultStarterPort;

        while (candidate <= MaxStarterPort)
        {
            if (FindConflictingStarterPort(candidate, reservedPortSet) is null)
            {
                return candidate;
            }

            candidate++;
        }

        throw new InvalidOperationException("No starter port range is available in the supported port range.");
    }

    public static string GetManagedServersRoot(string? baseDirectory = null) =>
        Path.Combine(LauncherStorageRootResolver.Resolve(baseDirectory), ManagedServersFolderName);

    public static string GetInstallRoot(string? baseDirectory = null) =>
        Path.Combine(GetManagedServersRoot(baseDirectory), InstallFolderName);

    public static string GetProfilesRoot(string? baseDirectory = null) =>
        Path.Combine(GetManagedServersRoot(baseDirectory), ProfilesFolderName);

    public static string BuildInstallDirectory(string profileId, string? baseDirectory = null) =>
        Path.Combine(GetInstallRoot(baseDirectory), profileId);

    public static string BuildCacheDirectory(string profileId, string? baseDirectory = null) =>
        Path.Combine(GetProfilesRoot(baseDirectory), profileId);

    public static bool IsManagedInstallDirectory(string directory) =>
        IsManagedProfileDirectory(directory, InstallFolderName);

    public static bool IsManagedCacheDirectory(string directory) =>
        IsManagedProfileDirectory(directory, ProfilesFolderName);

    private static int? FindConflictingStarterPort(int defaultPort, ISet<int> reservedPorts)
    {
        foreach (var port in BuildReservedPorts(defaultPort))
        {
            if (reservedPorts.Contains(port))
            {
                return port;
            }
        }

        return null;
    }

    private static string EnsureUniqueProfileId(string baseProfileId, IEnumerable<string> existingProfileIds)
    {
        var existing = existingProfileIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = baseProfileId;
        var suffix = 2;

        while (existing.Contains(candidate))
        {
            candidate = $"{baseProfileId}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (previousWasDash)
            {
                continue;
            }

            builder.Append('-');
            previousWasDash = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug)
            ? "server"
            : slug;
    }

    private static bool IsManagedProfileDirectory(string directory, string expectedParentFolderName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        var normalizedDirectory = Path.GetFullPath(directory);
        var parentDirectory = Directory.GetParent(normalizedDirectory);
        if (parentDirectory is null ||
            !string.Equals(parentDirectory.Name, expectedParentFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var managedRootDirectory = parentDirectory.Parent;
        return managedRootDirectory is not null &&
               string.Equals(managedRootDirectory.Name, ManagedServersFolderName, StringComparison.OrdinalIgnoreCase);
    }
}
