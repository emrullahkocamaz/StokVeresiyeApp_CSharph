using System.Drawing.Drawing2D;
using System.Linq;

namespace StokVeresiyeApp.Helpers;

public static class UITheme
{
    // Ana renkler: Petrol (açık tema varsayılanı). Apply() ile açık/koyu tema değişir.
    public static Color Primary = Color.FromArgb(15, 118, 110);        // Petrol #0F766E
    public static Color PrimaryDark = Color.FromArgb(17, 94, 89);      // #115E59
    public static Color PrimaryLight = Color.FromArgb(240, 253, 250);  // #F0FDFA
    public static Color Secondary = Color.FromArgb(100, 116, 139);

    // Durum renkleri
    public static Color Success = Color.FromArgb(16, 185, 129);
    public static Color Warning = Color.FromArgb(245, 158, 11);
    public static Color Danger = Color.FromArgb(239, 68, 68);
    public static Color Info = Color.FromArgb(14, 165, 233);

    // Zemin ve metin renkleri
    public static Color Background = Color.FromArgb(248, 250, 252);
    public static Color CardBg = Color.White;
    public static Color SidebarBg = Color.FromArgb(7, 35, 38);          // Petrolün çok koyu tonu
    public static Color SidebarHover = Color.FromArgb(8, 46, 47);
    public static Color SidebarActive = Color.FromArgb(15, 118, 110);
    public static Color BorderColor = Color.FromArgb(226, 232, 240);
    public static Color GridAltRow = Color.FromArgb(250, 252, 255);

    public static Color TextPrimary = Color.FromArgb(15, 23, 42);
    public static Color TextSecondary = Color.FromArgb(71, 85, 105);
    public static Color TextMuted = Color.FromArgb(148, 163, 184);

    public static bool IsDark { get; private set; }

    /// <summary>Açık/koyu paleti uygular. Formlar oluşturulmadan önce çağrılmalıdır.</summary>
    public static void Apply(bool dark)
    {
        IsDark = dark;
        if (!dark)
        {
            Primary = Color.FromArgb(15, 118, 110);
            PrimaryDark = Color.FromArgb(17, 94, 89);
            PrimaryLight = Color.FromArgb(240, 253, 250);
            Background = Color.FromArgb(248, 250, 252);
            CardBg = Color.White;
            SidebarBg = Color.FromArgb(7, 35, 38);
            SidebarHover = Color.FromArgb(8, 46, 47);
            SidebarActive = Primary;
            BorderColor = Color.FromArgb(226, 232, 240);
            GridAltRow = Color.FromArgb(250, 252, 255);
            TextPrimary = Color.FromArgb(15, 23, 42);
            TextSecondary = Color.FromArgb(71, 85, 105);
            TextMuted = Color.FromArgb(148, 163, 184);
        }
        else
        {
            Primary = Color.FromArgb(13, 148, 136);
            PrimaryDark = Color.FromArgb(15, 118, 110);
            PrimaryLight = Color.FromArgb(22, 44, 62);
            Background = Color.FromArgb(15, 23, 42);
            CardBg = Color.FromArgb(23, 32, 51);
            SidebarBg = Color.FromArgb(5, 14, 18);
            SidebarHover = Color.FromArgb(10, 33, 38);
            SidebarActive = Primary;
            BorderColor = Color.FromArgb(38, 50, 74);
            GridAltRow = Color.FromArgb(27, 38, 60);
            TextPrimary = Color.FromArgb(241, 245, 249);
            TextSecondary = Color.FromArgb(169, 182, 203);
            TextMuted = Color.FromArgb(100, 116, 139);
        }
    }

    // Fontlar
    public static readonly Font HeaderFont = new("Segoe UI", 16, FontStyle.Bold);
    public static readonly Font SubHeaderFont = new("Segoe UI", 12, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font BoldFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font RegularFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font KpiValueFont = new("Segoe UI", 16f, FontStyle.Bold);

    public static Font GetIconFont(float size = 10f)
    {
        string[] candidates = ["Segoe UI Emoji", "Segoe UI Symbol", "Noto Color Emoji", "Tahoma", "Segoe UI"];
        foreach (var candidate in candidates)
        {
            if (FontFamily.Families.Any(f => string.Equals(f.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return new Font(candidate, size, FontStyle.Regular);
        }

        return new Font("Segoe UI", size, FontStyle.Regular);
    }

    public static void ApplyGridStyle(DataGridView grid)
    {
        // Donanım hızlandırma ve titremeyi önleme (DoubleBuffering - GDI+ akıcılığı)
        try
        {
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, grid, new object[] { true });
        }
        catch { }

        grid.BackgroundColor = CardBg;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = BorderColor;
        grid.RowHeadersVisible = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.Font = RegularFont;
        grid.RowTemplate.Height = 38; // Ferah, gözü yormayan modern satır yüksekliği
        grid.Dock = DockStyle.Fill;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ScrollBars = ScrollBars.Both;

        // Kolonların aşırı daralıp birbirine girmesini ve yazıların ezilmesini önleyen minimum genişlik koruması
        grid.DataBindingComplete += (s, e) =>
        {
            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Visible)
                {
                    col.MinimumWidth = 75;
                    string nameLower = col.Name.ToLowerInvariant();
                    if (nameLower.Contains("ad") || nameLower.Contains("unvan") || nameLower.Contains("name") || nameLower.Contains("aciklama") || nameLower.Contains("açıklama"))
                    {
                        col.MinimumWidth = 140;
                    }
                }
            }
        };

        // Modern Başlık Stili (Slate 100 yumuşak zemin, tok okunabilir metin)
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Background;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
        grid.ColumnHeadersHeight = 42;

        // Modern Satır Stilleri (Yumuşak seçim rengi, gözü yormayan zeminler)
        grid.DefaultCellStyle.BackColor = CardBg;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = PrimaryLight; // Indigo/Blue 50
        grid.DefaultCellStyle.SelectionForeColor = TextPrimary;   // Tok koyu metin
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

        grid.AlternatingRowsDefaultCellStyle.BackColor = GridAltRow; // Çok hafif zebra
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = PrimaryLight;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;
    }

    public static Button CreateButton(string text, Color bg, Color fg, EventHandler onClick, int width = 120, int height = 36)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = bg,
            ForeColor = fg,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Width = width,
            Height = height,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 4, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(bg, 0.10f);
        btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.20f);
        btn.Click += onClick;
        return btn;
    }
}
