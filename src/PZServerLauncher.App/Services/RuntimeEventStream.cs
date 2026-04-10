using Microsoft.AspNetCore.SignalR.Client;
using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.App.Services;

public sealed class RuntimeEventStream : IAsyncDisposable
{
    private readonly LocalHostApiClient _hostApiClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _connection;

    public RuntimeEventStream(LocalHostApiClient hostApiClient)
    {
        _hostApiClient = hostApiClient;
    }

    public event Func<ServerRuntimeStatus, Task>? StatusChanged;

    public event Func<OperationJob, Task>? JobChanged;

    public event Func<string, string, Task>? LogLineReceived;

    public event Func<ProfileLiveOperationsSnapshot, Task>? LiveOperationsChanged;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { State: HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting })
            {
                return;
            }

            var connectionInfo = await _hostApiClient.GetLoopbackConnectionInfoAsync(cancellationToken);
            _connection = new HubConnectionBuilder()
                .WithUrl(new Uri(connectionInfo.BaseUri, "/hubs/runtime"), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(connectionInfo.Token);
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<ServerRuntimeStatus>("statusChanged", status => DispatchStatusChangedAsync(status));
            _connection.On<OperationJob>("jobChanged", job => DispatchJobChangedAsync(job));
            _connection.On<string, string>("logLine", (profileId, line) => DispatchLogLineAsync(profileId, line));
            _connection.On<ProfileLiveOperationsSnapshot>("liveOperationsChanged", snapshot => DispatchLiveOperationsChangedAsync(snapshot));

            await _connection.StartAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null)
            {
                return;
            }

            await _connection.DisposeAsync();
            _connection = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _gate.Dispose();
    }

    private Task DispatchStatusChangedAsync(ServerRuntimeStatus status) =>
        StatusChanged?.Invoke(status) ?? Task.CompletedTask;

    private Task DispatchJobChangedAsync(OperationJob job) =>
        JobChanged?.Invoke(job) ?? Task.CompletedTask;

    private Task DispatchLogLineAsync(string profileId, string line) =>
        LogLineReceived?.Invoke(profileId, line) ?? Task.CompletedTask;

    private Task DispatchLiveOperationsChangedAsync(ProfileLiveOperationsSnapshot snapshot) =>
        LiveOperationsChanged?.Invoke(snapshot) ?? Task.CompletedTask;
}
