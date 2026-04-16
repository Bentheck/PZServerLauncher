using System.Globalization;
using PZServerLauncher.Core.Settings;

namespace PZServerLauncher.Host.Services;

internal static class SandboxValueNormalizer
{
    public static string ToEditorValue(StructuredFieldDefinition field, string value)
    {
        if (field.ValueKind != StructuredValueKind.Choice || field.Options is not { Count: > 0 })
        {
            return value;
        }

        var trimmed = value.Trim();
        var rawMatch = field.Options.FirstOrDefault(option => ChoiceValueMatches(option.Value, trimmed));
        if (rawMatch is not null)
        {
            return rawMatch.Label;
        }

        var labelMatch = field.Options.FirstOrDefault(option => string.Equals(option.Label, trimmed, StringComparison.Ordinal));
        return labelMatch?.Label ?? trimmed;
    }

    public static string? ToPersistedValue(StructuredFieldDefinition field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            field.ValueKind != StructuredValueKind.Choice ||
            field.Options is not { Count: > 0 })
        {
            return value;
        }

        var trimmed = value.Trim();
        var labelMatch = field.Options.FirstOrDefault(option => string.Equals(option.Label, trimmed, StringComparison.Ordinal));
        if (labelMatch is not null)
        {
            return labelMatch.Value;
        }

        var rawMatch = field.Options.FirstOrDefault(option => ChoiceValueMatches(option.Value, trimmed));
        return rawMatch?.Value ?? trimmed;
    }

    public static bool ChoiceValueMatches(string optionValue, string candidateValue)
    {
        if (string.Equals(optionValue, candidateValue, StringComparison.Ordinal))
        {
            return true;
        }

        return decimal.TryParse(optionValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var optionDecimal) &&
               decimal.TryParse(candidateValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var candidateDecimal) &&
               optionDecimal == candidateDecimal;
    }
}
