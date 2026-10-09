using System.Drawing.Drawing2D;
using System.Linq;

namespace StokVeresiyeApp.Helpers;

public static class UITheme
{
    // Ana Renkler (Modern SaaS Slate / Blue Paleti)
    public static readonly Color Primary = Color.FromArgb(37, 99, 235);         // Canlı Tailwind Mavi (#2563EB)
    public static readonly Color PrimaryDark = Color.FromArgb(29, 78, 216);     // Koyu Mavi (#1D4ED8)
    public static readonly Color PrimaryLight = Color.FromArgb(239, 246, 255);  // Açık Mavi Zemin (#EFF6FF)
    public static readonly Color Secondary = Color.FromArgb(100, 116, 139);     // Slate Gri (#64748B)

    // Durum Renkleri
    public static readonly Color Success = Color.FromArgb(16, 185, 129);       // Zümrüt Yeşili (#10B981)
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);       // Kehribar Turuncu (#F59E0B)
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);         // Kırmızı (#EF4444)
    public static readonly Color Info = Color.FromArgb(14, 165, 233);          // Açık Mavi Sky (#0EA5E9)

    // Zemin ve Metin Renkleri
    public static readonly Color Background = Color.FromArgb(248, 250, 252);   // Slate 50 (#F8FAFC)
    public static readonly Color CardBg = Color.White;
    public static readonly Color SidebarBg = Color.FromArgb(15, 23, 42);       // Slate 900 (#0F172A)
    public static readonly Color SidebarHover = Color.FromArgb(30, 41, 59);    // Slate 800 (#1E293B)
    public static readonly Color SidebarActive = Color.FromArgb(37, 99, 235);   // Canlı Mavi
    public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);   // Slate 200 (#E2E8F0)
    
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);     // Slate 900 (#0F172A)
    public static readonly Color TextSecondary = Color.FromArgb(71, 85, 105);  // Slate 600 (#475569)
    public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);    // Slate 400 (#94A3B8)

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

        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(241, 245, 249);
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
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 10, 0);
        grid.ColumnHeadersHeight = 42;

        // Modern Satır Stilleri (Yumuşak seçim rengi, gözü yormayan zeminler)
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255); // Indigo/Blue 50
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);   // Tok koyu metin
        grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);

        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255); // Çok hafif zebra
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255);
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
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

    public static Krypton.Toolkit.KryptonButton CreateKryptonButton(string text, Color bg, Color fg, EventHandler onClick, int width = 120, int height = 36)
    {
        var btn = new Krypton.Toolkit.KryptonButton
        {
            Text = text,
            Width = width,
            Height = height,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 4, 0)
        };
        btn.StateCommon.Back.Color1 = bg;
        btn.StateCommon.Back.Color2 = bg;
        btn.StateCommon.Content.ShortText.Color1 = fg;
        btn.StateCommon.Content.ShortText.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        btn.StateCommon.Border.Rounding = 8;
        btn.Click += onClick;
        return btn;
    }
}
