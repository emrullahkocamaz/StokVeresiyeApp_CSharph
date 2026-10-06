using Krypton.Toolkit;

namespace StokVeresiyeApp.Services;

public class AppThemeOption
{
    public string DisplayName { get; set; } = string.Empty;
    public PaletteMode PaletteMode { get; set; }
    public bool IsDark { get; set; }

    public override string ToString() => DisplayName;
}

public static class AppThemeService
{
    private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme_setting.txt");
    private static readonly KryptonManager _kryptonManager = new();

    public static readonly List<AppThemeOption> AvailableThemes = new()
    {
        new() { DisplayName = "🎨 Microsoft 365 Modern Mavi", PaletteMode = PaletteMode.Microsoft365Blue, IsDark = false },
        new() { DisplayName = "🌙 Microsoft 365 Koyu / Gece Modu", PaletteMode = PaletteMode.Microsoft365BlackDarkMode, IsDark = true },
        new() { DisplayName = "⬛ Microsoft 365 Tam Siyah (OLED)", PaletteMode = PaletteMode.Microsoft365Black, IsDark = true },
        new() { DisplayName = "☀️ Microsoft 365 Sade Beyaz", PaletteMode = PaletteMode.Microsoft365White, IsDark = false },
        new() { DisplayName = "🥈 Microsoft 365 Gümüş (Silver)", PaletteMode = PaletteMode.Microsoft365Silver, IsDark = false },
        new() { DisplayName = "🏢 Office 2010 Klasik Mavi", PaletteMode = PaletteMode.Office2010Blue, IsDark = false },
        new() { DisplayName = "✨ Sparkle Canlı Mavi", PaletteMode = PaletteMode.SparkleBlue, IsDark = false },
        new() { DisplayName = "🍊 Sparkle Turuncu", PaletteMode = PaletteMode.SparkleOrange, IsDark = false },
        new() { DisplayName = "🔮 Sparkle Mor", PaletteMode = PaletteMode.SparklePurple, IsDark = false },
        new() { DisplayName = "📱 Material Tasarım (Açık)", PaletteMode = PaletteMode.MaterialLight, IsDark = false },
        new() { DisplayName = "🕶️ Material Tasarım (Koyu)", PaletteMode = PaletteMode.MaterialDark, IsDark = true },
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
                var found = AvailableThemes.FirstOrDefault(t => t.PaletteMode.ToString() == savedName || t.DisplayName == savedName);
                if (found != null)
                {
                    ApplyTheme(found, false);
                    return;
                }
            }
        }
        catch { }

        // Varsayılan
        ApplyTheme(AvailableThemes[0], false);
    }

    public static void ApplyTheme(AppThemeOption theme, bool save = true)
    {
        CurrentTheme = theme;
        try
        {
            ThemeManager.ApplyTheme(theme.PaletteMode, _kryptonManager);
            if (save)
            {
                File.WriteAllText(SettingsFile, theme.PaletteMode.ToString());
            }
        }
        catch { }

        ThemeChanged?.Invoke(theme);
    }
}
