namespace PZServerLauncher.Core.Planning;

public enum ServerLaunchStrategy
{
    DirectJavaTemplate,
    LaunchBlocked,
}

public sealed record ServerLaunchPlan(
    string WorkingDirectory,
    string LauncherPath,
    IReadOnlyList<string> Arguments,
    string Notes,
    ServerLaunchStrategy Strategy)
{
    public bool IsLaunchable => Strategy == ServerLaunchStrategy.DirectJavaTemplate;

    public bool IsLaunchBlocked => Strategy == ServerLaunchStrategy.LaunchBlocked;
}
