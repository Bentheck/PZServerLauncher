namespace PZServerLauncher.App.Services;

public static class ScreenshotCaptureOptions
{
    public static string? OutputDirectory { get; private set; }

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(OutputDirectory);

    public static void Initialize(string[] args)
    {
        OutputDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            if (!string.Equals(args[index], "--capture-screenshots", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                throw new ArgumentException("The --capture-screenshots flag requires an output directory path.");
            }

            OutputDirectory = Path.GetFullPath(args[index + 1]);
            return;
        }
    }
}
