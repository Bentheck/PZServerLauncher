using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PZServerLauncher.Contracts.Profiles;

namespace PZServerLauncher.App.ViewModels;

public sealed class SandboxPresetOptionViewModel
{
    public SandboxPresetOptionViewModel(string presetId, string label, bool isBuiltIn)
    {
        PresetId = presetId;
        Label = label;
        IsBuiltIn = isBuiltIn;
    }

    public string PresetId { get; }

    public string Label { get; }

    public bool IsBuiltIn { get; }

    public string DisplayLabel => IsBuiltIn ? $"{Label} (Shipped)" : $"{Label} (Custom)";
}

public sealed partial class SandboxCategoryViewModel : ObservableObject
{
    public SandboxCategoryViewModel(
        string categoryId,
        string title,
        string statusText,
        bool matchesPreset,
        bool isExpanded,
        IEnumerable<SandboxSectionViewModel> sections)
    {
        CategoryId = categoryId;
        Title = title;
        StatusText = statusText;
        MatchesPreset = matchesPreset;
        this.isExpanded = isExpanded;
        Sections = new ObservableCollection<SandboxSectionViewModel>(sections);
    }

    public string CategoryId { get; }

    public string Title { get; }

    public string StatusText { get; }

    public bool MatchesPreset { get; }

    public ObservableCollection<SandboxSectionViewModel> Sections { get; }

    [ObservableProperty]
    private bool isExpanded;

    public string ExpandButtonLabel => IsExpanded ? "Hide Sections" : "Open Sections";

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandButtonLabel));
    }
}

public sealed partial class SandboxSectionViewModel : ObservableObject
{
    public SandboxSectionViewModel(
        string sectionKey,
        string title,
        string? description,
        bool isExpanded,
        IEnumerable<SandboxFieldEditorViewModel> fields)
    {
        SectionKey = sectionKey;
        Title = title;
        Description = description ?? string.Empty;
        this.isExpanded = isExpanded;
        Fields = new ObservableCollection<SandboxFieldEditorViewModel>(fields);
    }

    public string SectionKey { get; }

    public string Title { get; }

    public string Description { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public ObservableCollection<SandboxFieldEditorViewModel> Fields { get; }

    [ObservableProperty]
    private bool isExpanded;

    public string ExpandButtonLabel => IsExpanded ? "Collapse" : "Expand";

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandButtonLabel));
    }
}

public sealed class SandboxFieldOptionViewModel
{
    public SandboxFieldOptionViewModel(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }

    public string Label { get; }
}

public sealed partial class SandboxFieldEditorViewModel : ObservableObject
{
    private readonly Action<SandboxFieldEditorViewModel, string?> _valueChanged;
    private bool _suppressCallbacks;

    public SandboxFieldEditorViewModel(
        SandboxFieldPresentation field,
        bool canEdit,
        IEnumerable<string>? errors,
        Action<SandboxFieldEditorViewModel, string?> valueChanged)
    {
        _valueChanged = valueChanged;
        FieldId = field.Field.FieldId;
        Label = field.Field.Label;
        HelpText = field.Field.HelpText ?? string.Empty;
        Control = field.Field.Control;
        RequiresRestart = field.Field.RequiresRestart;
        CanEdit = canEdit && !field.Field.IsReadOnly;
        HasPresetValue = field.HasPresetValue;
        MatchesPreset = field.MatchesPreset;
        PresetValue = field.PresetValue ?? string.Empty;
        Errors = new ObservableCollection<string>(errors ?? Array.Empty<string>());
        Options = new ObservableCollection<SandboxFieldOptionViewModel>(
            field.Options.Select(option => new SandboxFieldOptionViewModel(option.Value, option.Label)));
        currentValue = field.CurrentValue;
        selectedOption = Options.FirstOrDefault(option => string.Equals(option.Value, currentValue, StringComparison.Ordinal));
    }

    public string FieldId { get; }

    public string Label { get; }

    public string HelpText { get; }

    public bool HasHelpText => !string.IsNullOrWhiteSpace(HelpText);

    public SettingsFieldControlKind Control { get; }

    public bool RequiresRestart { get; }

    public bool CanEdit { get; }

    public bool IsReadOnly => !CanEdit;

    public bool HasPresetValue { get; }

    public bool MatchesPreset { get; }

    public string PresetValue { get; }

    public string PresetStatusText => MatchesPreset
        ? "Matches preset"
        : $"Preset: {PresetValue}";

    public ObservableCollection<SandboxFieldOptionViewModel> Options { get; }

    public ObservableCollection<string> Errors { get; }

    public bool HasErrors => Errors.Count > 0;

    public bool IsCheckbox => Control == SettingsFieldControlKind.Checkbox;

    public bool IsSelect => Control == SettingsFieldControlKind.Select;

    public bool IsNumeric => Control == SettingsFieldControlKind.Numeric;

    public bool IsMultiline => Control == SettingsFieldControlKind.MultiLineText;

    public bool IsPassword => Control == SettingsFieldControlKind.Password;

    public bool IsTextBox => !IsCheckbox && !IsSelect && !IsNumeric && !IsMultiline && !IsPassword;

    public string ReadOnlyValue
    {
        get
        {
            if (IsCheckbox && bool.TryParse(CurrentValue, out var parsed))
            {
                return parsed ? "On" : "Off";
            }

            return string.IsNullOrWhiteSpace(CurrentValue) ? "Not set" : CurrentValue;
        }
    }

    public bool IsChecked
    {
        get => bool.TryParse(CurrentValue, out var parsed) && parsed;
        set
        {
            var normalized = value ? "true" : "false";
            if (!string.Equals(CurrentValue, normalized, StringComparison.Ordinal))
            {
                CurrentValue = normalized;
            }
        }
    }

    [ObservableProperty]
    private string currentValue;

    [ObservableProperty]
    private SandboxFieldOptionViewModel? selectedOption;

    partial void OnCurrentValueChanged(string value)
    {
        OnPropertyChanged(nameof(ReadOnlyValue));
        OnPropertyChanged(nameof(IsChecked));

        if (IsSelect)
        {
            _suppressCallbacks = true;
            SelectedOption = Options.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.Ordinal));
            _suppressCallbacks = false;
        }

        if (!_suppressCallbacks)
        {
            _valueChanged(this, value);
        }
    }

    partial void OnSelectedOptionChanged(SandboxFieldOptionViewModel? value)
    {
        if (_suppressCallbacks || value is null || string.Equals(CurrentValue, value.Value, StringComparison.Ordinal))
        {
            return;
        }

        CurrentValue = value.Value;
    }
}
