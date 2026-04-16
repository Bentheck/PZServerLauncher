namespace PZServerLauncher.Core.Runtime;

public interface IRuntimeEventPublisher
{
    Task PublishStatusChangedAsync(ServerRuntimeStatus status, CancellationToken cancellationToken = default);

    Task PublishJobChangedAsync(OperationJob job, CancellationToken cancellationToken = default);

    Task PublishLogLineAsync(string profileId, string line, CancellationToken cancellationToken = default);

    Task PublishLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot, CancellationToken cancellationToken = default);
}
