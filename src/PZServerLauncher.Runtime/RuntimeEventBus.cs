using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Runtime;

public sealed class RuntimeEventBus : IRuntimeEventPublisher
{
    public event Func<ServerRuntimeStatus, Task>? StatusChanged;

    public event Func<OperationJob, Task>? JobChanged;

    public event Func<string, string, Task>? LogLineReceived;

    public event Func<ProfileLiveOperationsSnapshot, Task>? LiveOperationsChanged;

    public Task PublishStatusChangedAsync(ServerRuntimeStatus status, CancellationToken cancellationToken = default) =>
        StatusChanged?.Invoke(status) ?? Task.CompletedTask;

    public Task PublishJobChangedAsync(OperationJob job, CancellationToken cancellationToken = default) =>
        JobChanged?.Invoke(job) ?? Task.CompletedTask;

    public Task PublishLogLineAsync(string profileId, string line, CancellationToken cancellationToken = default) =>
        LogLineReceived?.Invoke(profileId, line) ?? Task.CompletedTask;

    public Task PublishLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot, CancellationToken cancellationToken = default) =>
        LiveOperationsChanged?.Invoke(snapshot) ?? Task.CompletedTask;
}
