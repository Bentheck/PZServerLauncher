namespace PZServerLauncher.Host.Security;

public static class HostAuthorizationPolicies
{
    public const string DesktopOnly = "DesktopOnly";
    public const string DesktopOrViewer = "DesktopOrViewer";
    public const string DesktopOrOperator = "DesktopOrOperator";
    public const string DesktopOrAdmin = "DesktopOrAdmin";
}
