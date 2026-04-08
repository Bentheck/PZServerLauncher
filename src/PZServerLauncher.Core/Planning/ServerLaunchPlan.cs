namespace PZServerLauncher.Core.Planning;

public enum ServerLaunchStrategy
{
    DirectJavaTemplate,
    VendorBatchFallback,
}

public sealed record ServerLaunchPlan(
    string WorkingDirectory,
    string LauncherPath,
    IReadOnlyList<string> Arguments,
    string Notes,
    ServerLaunchStrategy Strategy)
{
    public bool UsesVendorBatch => Strategy == ServerLaunchStrategy.VendorBatchFallback;
}
