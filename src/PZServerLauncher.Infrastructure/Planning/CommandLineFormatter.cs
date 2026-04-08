using System.Text;

namespace PZServerLauncher.Infrastructure.Planning;

internal static class CommandLineFormatter
{
    public static string Format(string executablePath, IEnumerable<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append(Quote(executablePath));

        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(Quote(argument));
        }

        return builder.ToString();
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        if (!value.Contains(' ') && !value.Contains('"'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
