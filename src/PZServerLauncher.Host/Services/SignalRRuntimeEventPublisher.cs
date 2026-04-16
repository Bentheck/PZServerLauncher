using Microsoft.AspNetCore.SignalR;
using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Hubs;

namespace PZServerLauncher.Host.Services;

public sealed class SignalRRuntimeEventPublisher(IHubContext<RuntimeHub> hubContext) : IRuntimeEventPublisher
{
    public Task PublishStatusChangedAsync(ServerRuntimeStatus status, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("statusChanged", status, cancellationToken);

    public Task PublishJobChangedAsync(OperationJob job, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("jobChanged", job, cancellationToken);

    public Task PublishLogLineAsync(string profileId, string line, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("logLine", profileId, line, cancellationToken);

    public Task PublishLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("liveOperationsChanged", snapshot, cancellationToken);
}
