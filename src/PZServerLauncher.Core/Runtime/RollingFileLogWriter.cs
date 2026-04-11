using System.Text;

namespace PZServerLauncher.Core.Runtime;

public sealed class RollingFileLogWriter
{
    private const int DefaultMaxFileBytes = 10 * 1024 * 1024;
    private const int DefaultArchiveCount = 5;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly object _gate = new();
    private readonly string _filePath;
    private readonly int _maxFileBytes;
    private readonly int _archiveCount;

    public RollingFileLogWriter(
        string filePath,
        int maxFileBytes = DefaultMaxFileBytes,
        int archiveCount = DefaultArchiveCount)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Log file path is required.", nameof(filePath));
        }

        if (maxFileBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes), maxFileBytes, "Max file size must be at least 1024 bytes.");
        }

        if (archiveCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(archiveCount), archiveCount, "Archive count must be at least 1.");
        }

        _filePath = Path.GetFullPath(filePath);
        _maxFileBytes = maxFileBytes;
        _archiveCount = archiveCount;
    }

    public void WriteLine(string message)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var normalized = message ?? string.Empty;
        var line = $"{timestamp} {normalized}{Environment.NewLine}";
        var bytes = Utf8WithoutBom.GetBytes(line);

        lock (_gate)
        {
            EnsureDirectory();
            RotateIfNeeded(bytes.Length);

            using var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: false);
        }
    }

    private void EnsureDirectory()
    {
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
    }

    private void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        var currentLength = new FileInfo(_filePath).Length;
        if (currentLength + incomingBytes <= _maxFileBytes)
        {
            return;
        }

        var oldestArchivePath = BuildArchivePath(_archiveCount);
        if (File.Exists(oldestArchivePath))
        {
            File.Delete(oldestArchivePath);
        }

        for (var archiveIndex = _archiveCount - 1; archiveIndex >= 1; archiveIndex--)
        {
            var currentArchivePath = BuildArchivePath(archiveIndex);
            if (!File.Exists(currentArchivePath))
            {
                continue;
            }

            var nextArchivePath = BuildArchivePath(archiveIndex + 1);
            File.Move(currentArchivePath, nextArchivePath);
        }

        File.Move(_filePath, BuildArchivePath(1));
    }

    private string BuildArchivePath(int archiveIndex) =>
        $"{_filePath}.{archiveIndex}";
}
