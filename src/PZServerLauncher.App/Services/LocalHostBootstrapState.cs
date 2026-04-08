namespace PZServerLauncher.App.Services;

public sealed record LocalHostBootstrapState
{
    public int LoopbackPort { get; init; } = 48231;

    public string ProtectedLocalApiToken { get; init; } = string.Empty;
}
