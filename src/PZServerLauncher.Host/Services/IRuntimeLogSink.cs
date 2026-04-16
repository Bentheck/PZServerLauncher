namespace PZServerLauncher.Host.Services;

public interface IRuntimeLogSink
{
    void WriteProfileLine(string profileId, string line);
}
