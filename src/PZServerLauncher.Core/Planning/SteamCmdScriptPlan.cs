namespace PZServerLauncher.Core.Planning;

public sealed record SteamCmdScriptPlan(
    string InstallDirectory,
    IReadOnlyList<string> ScriptLines);
