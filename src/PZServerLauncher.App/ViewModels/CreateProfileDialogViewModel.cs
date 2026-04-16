using CommunityToolkit.Mvvm.ComponentModel;
using PZServerLauncher.App.Services;
using PZServerLauncher.Core.Profiles;

namespace PZServerLauncher.App.ViewModels;

public partial class CreateProfileDialogViewModel : ViewModelBase
{
    private const string SoftBreak = "\u200B";
    private readonly IReadOnlyList<CreateProfilePortReservation> _existingProfiles;
    private readonly IReadOnlyDictionary<int, CreateProfilePortReservation> _reservedPorts;

    public CreateProfileDialogViewModel(IEnumerable<CreateProfilePortReservation> existingProfiles)
    {
        _existingProfiles = existingProfiles.ToArray();
        _reservedPorts = BuildReservedPortLookup(_existingProfiles);
        displayName = "Main Server";
        defaultPortText = ServerProfileFactory.FindNextAvailableStarterPort(
            ServerProfileFactory.DefaultStarterPort,
            _reservedPorts.Keys).ToString();
        preferredMemoryInGigabytesText = ServerProfileFactory.DefaultPreferredMemoryInGigabytes.ToString();
        maxPlayersText = ServerProfileFactory.DefaultMaxPlayers.ToString();
        NotifyComputedState();
    }

    [ObservableProperty]
    private string displayName;

    [ObservableProperty]
    private string defaultPortText;

    [ObservableProperty]
    private string preferredMemoryInGigabytesText;

    [ObservableProperty]
    private string maxPlayersText;

    public string DialogTitle => "Create Server Profile";

    public string DialogSummary => _existingProfiles.Count == 0
        ? $"Pick the server name and base port now, then leave memory at {ServerProfileFactory.DefaultPreferredMemoryInGigabytes} GB and max players at {ServerProfileFactory.DefaultMaxPlayers} if you want the normal setup. The launcher will derive the profile id, server name, install path, data path, UDP port, and RCON port from those inputs."
        : $"Pick the server name and base port now, then leave memory at {ServerProfileFactory.DefaultPreferredMemoryInGigabytes} GB and max players at {ServerProfileFactory.DefaultMaxPlayers} if you want the normal setup. The launcher will derive the profile id, server name, install path, data path, UDP port, and RCON port from those inputs, while skipping port sets already used by other profiles.";

    public string PortReservationSummary => _existingProfiles.Count == 0
        ? "No other managed profiles are using launcher ports yet."
        : $"{_existingProfiles.Count} existing profile(s) already reserve their own base, UDP, and RCON ports. New profiles must use a free three-port set.";

    public bool CanSubmit =>
        !string.IsNullOrWhiteSpace(DisplayName) &&
        TryParsePort(out var requestedPort) &&
        TryParsePositiveInteger(PreferredMemoryInGigabytesText, out _) &&
        TryParsePositiveInteger(MaxPlayersText, out _) &&
        TryFindConflictingPort(requestedPort, out _, out _) is false;

    public string ValidationMessage
    {
        get
        {
            if (!TryParsePort(out var parsedPort))
            {
                return $"Enter a whole number between {ServerProfileFactory.MinStarterPort} and {ServerProfileFactory.MaxStarterPort}.";
            }

            if (!TryParsePositiveInteger(PreferredMemoryInGigabytesText, out _))
            {
                return "Preferred memory must be a whole number greater than zero.";
            }

            if (!TryParsePositiveInteger(MaxPlayersText, out _))
            {
                return "Max players must be a whole number greater than zero.";
            }

            if (TryFindConflictingPort(parsedPort, out var conflictingPort, out var conflictingProfile))
            {
                var nextAvailablePort = ServerProfileFactory.FindNextAvailableStarterPort(parsedPort, _reservedPorts.Keys);
                return $"Base port {parsedPort} would reuse reserved port {conflictingPort} from {conflictingProfile.DisplayName} ({conflictingProfile.PortSummary}). Use base port {nextAvailablePort} instead; the launcher does not reuse ports already claimed by other profiles.";
            }

            return _existingProfiles.Count == 0
                ? $"Base port {parsedPort} will create UDP {parsedPort + 1} and RCON {parsedPort + 2}."
                : $"Base port {parsedPort} will create UDP {parsedPort + 1} and RCON {parsedPort + 2}. Ports already used by other profiles stay reserved.";
        }
    }

    public string ProfileIdPreview =>
        TryBuildPreview(out var profile)
            ? profile.ProfileId
            : "-";

    public string ServerNamePreview =>
        TryBuildPreview(out var profile)
            ? profile.ServerName
            : "-";

    public string InstallPathPreview =>
        TryBuildPreview(out var profile)
            ? FormatPreviewPath(profile.InstallDirectory)
            : FormatPreviewPath(ServerProfileFactory.BuildInstallDirectory("server"));

    public string DataPathPreview =>
        TryBuildPreview(out var profile)
            ? FormatPreviewPath(profile.CacheDirectory)
            : FormatPreviewPath(ServerProfileFactory.BuildCacheDirectory("server"));

    public string PortPreview =>
        TryBuildPreview(out var profile)
            ? $"{profile.DefaultPort} / {profile.UdpPort} / {profile.RconPort}"
            : "- / - / -";

    public string MemoryPreview =>
        TryBuildPreview(out var profile)
            ? $"{profile.PreferredMemoryInGigabytes} GB"
            : $"{ServerProfileFactory.DefaultPreferredMemoryInGigabytes} GB";

    public string MaxPlayersPreview =>
        TryParsePositiveInteger(MaxPlayersText, out var maxPlayers)
            ? maxPlayers.ToString()
            : ServerProfileFactory.DefaultMaxPlayers.ToString();

    public string FooterMessage => _existingProfiles.Count == 0
        ? "Each server gets its own install folder, data folder, exclusive port set, and initial General settings."
        : "Each server gets its own install folder, data folder, exclusive port set, and initial General settings. Ports already used by other profiles will not be reused.";

    public bool TryBuildRequest(out CreateProfileRequest? request)
    {
        request = null;
        if (!CanSubmit ||
            !TryParsePort(out var defaultPort) ||
            !TryParsePositiveInteger(PreferredMemoryInGigabytesText, out var preferredMemoryInGigabytes) ||
            !TryParsePositiveInteger(MaxPlayersText, out var maxPlayers))
        {
            return false;
        }

        request = new CreateProfileRequest(
            DisplayName.Trim(),
            defaultPort,
            preferredMemoryInGigabytes,
            maxPlayers);
        return true;
    }

    partial void OnDisplayNameChanged(string value)
    {
        NotifyComputedState();
    }

    partial void OnDefaultPortTextChanged(string value)
    {
        NotifyComputedState();
    }

    partial void OnPreferredMemoryInGigabytesTextChanged(string value)
    {
        NotifyComputedState();
    }

    partial void OnMaxPlayersTextChanged(string value)
    {
        NotifyComputedState();
    }

    private bool TryBuildPreview(out ServerProfile profile)
    {
        var previewPort = ResolvePreviewPort();
        var previewMemory = ResolvePreviewMemory();
        profile = ServerProfileFactory.CreateStarterProfile(
            DisplayName,
            previewPort,
            _existingProfiles.Select(profile => profile.ProfileId),
            preferredMemoryInGigabytes: previewMemory);
        return true;
    }

    private bool TryParsePort(out int port)
    {
        return int.TryParse(DefaultPortText, out port) &&
               ServerProfileFactory.IsValidStarterPort(port);
    }

    private static bool TryParsePositiveInteger(string value, out int parsed) =>
        int.TryParse(value, out parsed) && parsed > 0;

    private int ResolvePreviewPort()
    {
        if (TryParsePort(out var parsedPort))
        {
            return TryFindConflictingPort(parsedPort, out _, out _)
                ? ServerProfileFactory.FindNextAvailableStarterPort(parsedPort, _reservedPorts.Keys)
                : parsedPort;
        }

        return ServerProfileFactory.FindNextAvailableStarterPort(
            ServerProfileFactory.DefaultStarterPort,
            _reservedPorts.Keys);
    }

    private int ResolvePreviewMemory() =>
        TryParsePositiveInteger(PreferredMemoryInGigabytesText, out var parsedMemory)
            ? parsedMemory
            : ServerProfileFactory.DefaultPreferredMemoryInGigabytes;

    private bool TryFindConflictingPort(
        int requestedPort,
        out int conflictingPort,
        out CreateProfilePortReservation conflictingProfile)
    {
        conflictingPort = 0;
        conflictingProfile = null!;

        var matchedPort = ServerProfileFactory.FindConflictingStarterPort(requestedPort, _reservedPorts.Keys);
        if (matchedPort is null)
        {
            return false;
        }

        if (!_reservedPorts.TryGetValue(matchedPort.Value, out var matchedProfile) || matchedProfile is null)
        {
            return false;
        }

        conflictingProfile = matchedProfile;
        conflictingPort = matchedPort.Value;
        return true;
    }

    private static IReadOnlyDictionary<int, CreateProfilePortReservation> BuildReservedPortLookup(
        IEnumerable<CreateProfilePortReservation> existingProfiles)
    {
        var lookup = new Dictionary<int, CreateProfilePortReservation>();
        foreach (var profile in existingProfiles)
        {
            foreach (var port in profile.ReservedPorts)
            {
                lookup.TryAdd(port, profile);
            }
        }

        return lookup;
    }

    private static string FormatPreviewPath(string path) =>
        path
            .Replace("\\", $"\\{SoftBreak}", StringComparison.Ordinal)
            .Replace("/", $"/{SoftBreak}", StringComparison.Ordinal)
            .Replace("-", $"-{SoftBreak}", StringComparison.Ordinal);

    private void NotifyComputedState()
    {
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(DialogSummary));
        OnPropertyChanged(nameof(PortReservationSummary));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(ProfileIdPreview));
        OnPropertyChanged(nameof(ServerNamePreview));
        OnPropertyChanged(nameof(InstallPathPreview));
        OnPropertyChanged(nameof(DataPathPreview));
        OnPropertyChanged(nameof(PortPreview));
        OnPropertyChanged(nameof(MemoryPreview));
        OnPropertyChanged(nameof(MaxPlayersPreview));
        OnPropertyChanged(nameof(FooterMessage));
    }
}
