using PZServerLauncher.Core.Runtime;
using PZServerLauncher.Host.Services;

namespace PZServerLauncher.Tests.Services;

public sealed class RuntimeStateStoreTests
{
    [Fact]
    public void AppendLog_TracksInferredPlayerRoster()
    {
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter());
        store.Update(new ServerRuntimeStatus("alpha", ServerRuntimeState.Running, 42, DateTimeOffset.UtcNow, null, null, null));

        store.AppendLog("alpha", "player Alice connected");
        store.AppendLog("alpha", "Bob joined the game");

        var status = store.GetOrDefault("alpha");
        var snapshot = store.GetLiveOperations("alpha");

        Assert.Equal(2, status.ConnectedPlayerCount);
        Assert.Equal(2, snapshot.ConnectedPlayers.Count);
        Assert.Contains(snapshot.ConnectedPlayers, player => player.UserName == "Alice");
        Assert.Contains(snapshot.ConnectedPlayers, player => player.UserName == "Bob");
    }

    [Fact]
    public void AppendLog_RemovesPlayersWhenLeaveSignalsAppear()
    {
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter());
        store.Update(new ServerRuntimeStatus("alpha", ServerRuntimeState.Running, 42, DateTimeOffset.UtcNow, null, null, null));

        store.AppendLog("alpha", "user Carol connected");
        store.AppendLog("alpha", "player Carol disconnected");

        var status = store.GetOrDefault("alpha");
        var snapshot = store.GetLiveOperations("alpha");

        Assert.Equal(0, status.ConnectedPlayerCount);
        Assert.Empty(snapshot.ConnectedPlayers);
        Assert.Contains(snapshot.RecentPlayerSignals, signal => signal.UserName == "Carol" && signal.Activity == "Left");
    }

    [Fact]
    public void RecordOperatorAction_UpdatesLatestOperatorSummary()
    {
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter());
        store.Update(new ServerRuntimeStatus("alpha", ServerRuntimeState.Running, 42, DateTimeOffset.UtcNow, null, null, null));

        var snapshot = store.RecordOperatorAction("alpha", "Broadcast", "servermsg Restart in five", "Broadcast sent: Restart in five");
        var status = store.GetOrDefault("alpha");

        Assert.Single(snapshot.RecentOperatorActions);
        Assert.Equal("Broadcast sent: Restart in five", status.LastOperatorCommandSummary);
        Assert.Equal("Broadcast", snapshot.RecentOperatorActions[0].Kind);
    }

    [Fact]
    public void AppendLog_PreservesRecentBufferWhileWritingToSink()
    {
        var sink = new InMemoryRuntimeLogSink();
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter(), sink);

        for (var index = 1; index <= 260; index++)
        {
            store.AppendLog("alpha", $"line {index}");
        }

        var recent = store.GetRecentLogs("alpha");

        Assert.Equal(260, sink.LineCount);
        Assert.Equal(250, recent.Count);
        Assert.Equal("line 11", recent[0]);
        Assert.Equal("line 260", recent[^1]);
    }

    private sealed class InMemoryRuntimeLogSink : IRuntimeLogSink
    {
        public int LineCount { get; private set; }

        public void WriteProfileLine(string profileId, string line)
        {
            LineCount++;
        }
    }
}
