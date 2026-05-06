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
        Assert.Equal(260, recent.Count);
        Assert.Equal("line 1", recent[0]);
        Assert.Equal("line 260", recent[^1]);
    }

    [Fact]
    public void AppendLog_TracksWorkshopDownloadProgressFromConfiguredWorkshopItems()
    {
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter());
        store.BeginWorkshopDownloadSession("alpha", ["111111111", "222222222", "333333333"]);
        store.Update(new ServerRuntimeStatus("alpha", ServerRuntimeState.Running, 42, DateTimeOffset.UtcNow, null, null, null));

        store.AppendLog("alpha", "Downloading workshop content for 222222222 at 1048576 bytes.");

        var status = store.GetOrDefault("alpha");

        Assert.NotNull(status.WorkshopDownloadProgress);
        Assert.Equal(2, status.WorkshopDownloadProgress!.CurrentItemIndex);
        Assert.Equal(3, status.WorkshopDownloadProgress.TotalItemCount);
        Assert.Equal("Downloading workshop item 2/3 | Workshop ID 222222222", status.PinnedLatestSignal);
        Assert.Equal("Downloading workshop content for 222222222 at 1048576 bytes.", status.LatestLogLine);
    }

    [Fact]
    public void ResetLiveOperations_ClearsWorkshopDownloadProgress()
    {
        var store = new RuntimeStateStore(new ProjectZomboidLiveOperationsInterpreter());
        store.BeginWorkshopDownloadSession("alpha", ["111111111"]);
        store.Update(new ServerRuntimeStatus("alpha", ServerRuntimeState.Running, 42, DateTimeOffset.UtcNow, null, null, null));
        store.AppendLog("alpha", "Downloading workshop content for 111111111.");

        store.ResetLiveOperations("alpha");

        var status = store.GetOrDefault("alpha");
        Assert.Null(status.WorkshopDownloadProgress);
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
