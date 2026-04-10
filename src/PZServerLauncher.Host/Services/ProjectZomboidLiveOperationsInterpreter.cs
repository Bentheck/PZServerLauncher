using System.Text.RegularExpressions;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Host.Services;

public sealed class ProjectZomboidLiveOperationsInterpreter
{
    private static readonly Regex[] JoinPatterns =
    [
        PlayerPattern(@"(?i)\bplayer\s+[""']?(?<name>[^""']+)[""']?\s+(?:connected|joined)\b"),
        PlayerPattern(@"(?i)\buser\s+[""']?(?<name>[^""']+)[""']?\s+(?:connected|joined)\b"),
        PlayerPattern(@"(?i)\b(?<name>[A-Za-z0-9_.\-]+)\s+(?:joined the game|has joined)\b"),
    ];

    private static readonly Regex[] LeavePatterns =
    [
        PlayerPattern(@"(?i)\bplayer\s+[""']?(?<name>[^""']+)[""']?\s+(?:disconnected|left)\b"),
        PlayerPattern(@"(?i)\buser\s+[""']?(?<name>[^""']+)[""']?\s+(?:disconnected|left)\b"),
        PlayerPattern(@"(?i)\b(?<name>[A-Za-z0-9_.\-]+)\s+(?:left the game|has disconnected)\b"),
    ];

    public PlayerActivitySignal? TryParse(string line, DateTimeOffset timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        foreach (var pattern in JoinPatterns)
        {
            var match = pattern.Match(line);
            if (match.Success && TryNormalizeName(match.Groups["name"].Value, out var playerName))
            {
                return new PlayerActivitySignal(playerName, "Joined", timestampUtc, line);
            }
        }

        foreach (var pattern in LeavePatterns)
        {
            var match = pattern.Match(line);
            if (match.Success && TryNormalizeName(match.Groups["name"].Value, out var playerName))
            {
                return new PlayerActivitySignal(playerName, "Left", timestampUtc, line);
            }
        }

        return null;
    }

    private static bool TryNormalizeName(string rawValue, out string playerName)
    {
        playerName = rawValue.Trim().Trim('"', '\'', '[', ']', '(', ')', ':', ';');
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return false;
        }

        if (playerName.Contains(" server ", StringComparison.OrdinalIgnoreCase) ||
            playerName.Contains(" connection ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
    private static Regex PlayerPattern(string pattern) => new(pattern, RegexOptions.Compiled);
}
