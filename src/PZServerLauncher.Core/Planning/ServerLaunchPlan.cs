namespace PZServerLauncher.Core.Planning;

public sealed record ServerLaunchPlan(
    string WorkingDirectory,
    string LauncherPath,
    IReadOnlyList<string> Arguments,
    string Notes);
