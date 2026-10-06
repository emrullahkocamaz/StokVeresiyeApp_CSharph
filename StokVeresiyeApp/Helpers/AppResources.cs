using System.Drawing;

namespace StokVeresiyeApp.Helpers;

public static class AppResources
{
    private static Icon? _appIcon;
    private static Image? _logoBanner;
    private static Image? _appIconSquare;

    public static Icon AppIcon
    {
        get
        {
            if (_appIcon != null) return _appIcon;

            try
            {
                using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("StokVeresiyeApp.Resources.app.ico");
                if (stream != null)
                {
                    _appIcon = new Icon(stream);
                    return _appIcon;
                }
            }
            catch { }

            // Fallback: dosya sisteminden oku
            try
            {
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(localPath))
                {
                    _appIcon = new Icon(localPath);
                    return _appIcon;
                }
            }
            catch { }

            return SystemIcons.Application;
        }
    }

    public static Image? LogoBanner
    {
        get
        {
            if (_logoBanner != null) return _logoBanner;

            try
            {
                using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("StokVeresiyeApp.Resources.logo_full.png");
                if (stream != null)
                {
                    _logoBanner = Image.FromStream(stream);
                    return _logoBanner;
                }
            }
            catch { }

            try
            {
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo_full.png");
                if (File.Exists(localPath))
                {
                    _logoBanner = Image.FromFile(localPath);
                    return _logoBanner;
                }
            }
            catch { }

            return null;
        }
    }

    public static Image? AppIconSquare
    {
        get
        {
            if (_appIconSquare != null) return _appIconSquare;

            try
            {
                using var stream = typeof(AppResources).Assembly.GetManifestResourceStream("StokVeresiyeApp.Resources.app_icon_square.png");
                if (stream != null)
                {
                    _appIconSquare = Image.FromStream(stream);
                    return _appIconSquare;
                }
            }
            catch { }

            try
            {
                var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon_square.png");
                if (File.Exists(localPath))
                {
                    _appIconSquare = Image.FromFile(localPath);
                    return _appIconSquare;
                }
            }
            catch { }

            return null;
        }
    }
}
