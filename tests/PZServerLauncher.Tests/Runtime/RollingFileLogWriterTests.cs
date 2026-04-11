using PZServerLauncher.Core.Runtime;

namespace PZServerLauncher.Tests.Runtime;

public sealed class RollingFileLogWriterTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PZServerLauncher.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteLine_CreatesLogFile()
    {
        var path = Path.Combine(_tempRoot, "logs", "app.log");
        var writer = new RollingFileLogWriter(path, maxFileBytes: 1024, archiveCount: 2);

        writer.WriteLine("hello");

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("hello", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteLine_RotatesWhenFileExceedsLimit()
    {
        var path = Path.Combine(_tempRoot, "logs", "host.log");
        var writer = new RollingFileLogWriter(path, maxFileBytes: 1024, archiveCount: 2);
        var payload = new string('x', 600);

        writer.WriteLine(payload);
        writer.WriteLine(payload);
        writer.WriteLine(payload);

        Assert.True(File.Exists(path));
        Assert.True(File.Exists($"{path}.1"));
        Assert.True(new FileInfo(path).Length <= 1024);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
