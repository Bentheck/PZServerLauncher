namespace PZServerLauncher.Contracts.Profiles;

public static class SandboxPagePresentationBuilder
{
    public static IReadOnlyList<SandboxCategoryPresentation> Build(
        SettingsPageDto page,
        IReadOnlyDictionary<string, string?> values,
        SandboxPresetDto? preset,
        string? searchText)
    {
        var query = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim();
        var sections = page.Sections
            .Select((section, index) => BuildSectionPresentation(section, index, values, preset, query))
            .Where(section => section.Fields.Count > 0)
            .ToArray();

        return sections
            .GroupBy(section => new SandboxCategoryKey(section.CategoryId, section.CategoryTitle, section.CategoryOrder))
            .OrderBy(group => group.Key.Order)
            .ThenBy(group => group.Key.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sectionList = group.ToArray();
                var comparedFieldCount = sectionList.Sum(section => section.ComparedFieldCount);
                var matchingFieldCount = sectionList.Sum(section => section.MatchingFieldCount);
                var hasPresetComparison = comparedFieldCount > 0;

                return new SandboxCategoryPresentation(
                    group.Key.CategoryId,
                    group.Key.Title,
                    group.Key.Order,
                    sectionList,
                    hasPresetComparison,
                    comparedFieldCount,
                    matchingFieldCount,
                    !hasPresetComparison || comparedFieldCount == matchingFieldCount);
            })
            .ToArray();
    }

    public static SandboxPresetDto? ResolvePreset(IReadOnlyList<SandboxPresetDto> presets, string? presetId)
    {
        if (presets.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(presetId))
        {
            var match = presets.FirstOrDefault(preset =>
                string.Equals(preset.PresetId, presetId, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return presets[0];
    }

    private static SandboxSectionPresentation BuildSectionPresentation(
        SettingsSectionDto section,
        int index,
        IReadOnlyDictionary<string, string?> values,
        SandboxPresetDto? preset,
        string? searchText)
    {
        var categoryId = string.IsNullOrWhiteSpace(section.CategoryId) ? section.SectionId : section.CategoryId;
        var categoryTitle = string.IsNullOrWhiteSpace(section.CategoryTitle) ? section.Title : section.CategoryTitle;
        var categoryOrder = section.CategoryOrder == 0 ? index + 1 : section.CategoryOrder;

        var allFieldPresentations = section.Fields
            .Select(field => BuildFieldPresentation(section, field, values, preset))
            .ToArray();

        var fieldPresentations = allFieldPresentations
            .Where(field => MatchesSearch(section, field, searchText))
            .ToArray();

        // Keep preset status stable even when the user filters the visible fields.
        var comparedFieldCount = allFieldPresentations.Count(field => field.HasPresetValue);
        var matchingFieldCount = allFieldPresentations.Count(field => field.HasPresetValue && field.MatchesPreset);

        return new SandboxSectionPresentation(
            section,
            categoryId,
            categoryTitle,
            categoryOrder,
            fieldPresentations,
            comparedFieldCount,
            matchingFieldCount);
    }

    private static SandboxFieldPresentation BuildFieldPresentation(
        SettingsSectionDto section,
        SettingsFieldDto field,
        IReadOnlyDictionary<string, string?> values,
        SandboxPresetDto? preset)
    {
        var currentValue = values.TryGetValue(field.FieldId, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value!
            : field.DefaultValue ?? string.Empty;

        var hasPresetValue = preset is not null && preset.Values.ContainsKey(field.FieldId);
        var presetValue = hasPresetValue ? preset!.Values[field.FieldId] : null;
        var options = GetRenderableOptions(field, currentValue);

        return new SandboxFieldPresentation(
            section,
            field,
            currentValue,
            presetValue,
            hasPresetValue,
            !hasPresetValue || string.Equals(currentValue, presetValue, StringComparison.Ordinal),
            options);
    }

    private static bool MatchesSearch(SettingsSectionDto section, SandboxFieldPresentation field, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return Contains(section.Title, searchText)
            || Contains(section.Description, searchText)
            || Contains(field.Field.Label, searchText)
            || Contains(field.Field.HelpText, searchText)
            || Contains(field.CurrentValue, searchText)
            || field.Options.Any(option =>
                Contains(option.Label, searchText) ||
                Contains(option.Value, searchText) ||
                Contains(option.Description, searchText));
    }

    private static IReadOnlyList<SettingsFieldOptionDto> GetRenderableOptions(SettingsFieldDto field, string currentValue)
    {
        var options = (field.Options ?? Array.Empty<SettingsFieldOptionDto>())
            .Select(option => new SettingsFieldOptionDto(
                option.Label,
                option.Label,
                option.Description))
            .ToArray();
        if (string.IsNullOrWhiteSpace(currentValue) ||
            options.Any(option => string.Equals(option.Value, currentValue, StringComparison.Ordinal)))
        {
            return options;
        }

        return
        [
            new SettingsFieldOptionDto(currentValue, $"{currentValue} (Current)", "Loaded from the current file."),
            .. options,
        ];
    }

    private static bool Contains(string? value, string searchText) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private sealed record SandboxCategoryKey(
        string CategoryId,
        string Title,
        int Order);
}

public sealed record SandboxCategoryPresentation(
    string CategoryId,
    string Title,
    int Order,
    IReadOnlyList<SandboxSectionPresentation> Sections,
    bool HasPresetComparison,
    int ComparedFieldCount,
    int MatchingFieldCount,
    bool MatchesPreset);

public sealed record SandboxSectionPresentation(
    SettingsSectionDto Section,
    string CategoryId,
    string CategoryTitle,
    int CategoryOrder,
    IReadOnlyList<SandboxFieldPresentation> Fields,
    int ComparedFieldCount,
    int MatchingFieldCount);

public sealed record SandboxFieldPresentation(
    SettingsSectionDto Section,
    SettingsFieldDto Field,
    string CurrentValue,
    string? PresetValue,
    bool HasPresetValue,
    bool MatchesPreset,
    IReadOnlyList<SettingsFieldOptionDto> Options);
