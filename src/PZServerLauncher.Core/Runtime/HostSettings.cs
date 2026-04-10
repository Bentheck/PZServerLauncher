namespace PZServerLauncher.Core.Runtime;

public sealed record HostSettings
{
    public int LoopbackPort { get; init; } = 48231;

    public bool StartHostWithWindows { get; init; }

    public RemoteAccessSettings RemoteAccess { get; init; } = new();

    public OwnerBootstrapState OwnerBootstrap { get; init; } = new(false, null, null, null);
}
