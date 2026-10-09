using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Services;

public class AppThemeOption
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDark { get; set; }

    public override string ToString() => DisplayName;
}

public static class AppThemeService
{
    private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_setting.txt");

    public static readonly List<AppThemeOption> AvailableThemes = new()
    {
        new() { DisplayName = "☀️ Açık Tema", IsDark = false },
        new() { DisplayName = "🌙 Koyu Tema", IsDark = true },
    };

    public static AppThemeOption CurrentTheme { get; private set; } = AvailableThemes[0];

    public static event Action<AppThemeOption>? ThemeChanged;

    public static void InitializeTheme()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string savedName = File.ReadAllText(SettingsFile).Trim();
                var found = AvailableThemes.FirstOrDefault(t => t.DisplayName == savedName);
                if (found != null)
                {
                    ApplyTheme(found, false);
                    return;
                }
            }
        }
        catch { }

        ApplyTheme(AvailableThemes[0], false);
    }

    public static void ApplyTheme(AppThemeOption theme, bool save = true)
    {
        CurrentTheme = theme;
        UITheme.Apply(theme.IsDark);
        try
        {
            if (save) File.WriteAllText(SettingsFile, theme.DisplayName);
        }
        catch { }

        ThemeChanged?.Invoke(theme);
    }
}
