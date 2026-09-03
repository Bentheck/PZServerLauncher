using Avalonia;
using Avalonia.Styling;

namespace PZServerLauncher.App.Services;

public sealed class ApplicationThemeService
{
    private readonly string _preferencePath;

    public ApplicationThemeService(string rootDirectory)
    {
        var stateDirectory = Path.Combine(rootDirectory, "state");
        Directory.CreateDirectory(stateDirectory);
        _preferencePath = Path.Combine(stateDirectory, "theme.txt");
    }

    public bool IsDarkMode { get; private set; }

    public void Initialize()
    {
        try
        {
            IsDarkMode = File.Exists(_preferencePath) &&
                         string.Equals(File.ReadAllText(_preferencePath).Trim(), "dark", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            IsDarkMode = false;
        }

        ApplyTheme();
    }

    public void SetDarkMode(bool isDarkMode)
    {
        IsDarkMode = isDarkMode;
        ApplyTheme();

        try
        {
            File.WriteAllText(_preferencePath, isDarkMode ? "dark" : "light");
        }
        catch
        {
            // Theme changes remain active for this session if the preference cannot be saved.
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = IsDarkMode
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }
    }
}
