using System.Drawing.Drawing2D;

namespace StokVeresiyeApp.Helpers;

public static class UITheme
{
    // Ana Renkler
    public static readonly Color Primary = Color.FromArgb(14, 154, 167);       // Canlı Mavi (#2563EB)
    public static readonly Color PrimaryDark = Color.FromArgb(10, 120, 133);   // Koyu Mavi (#1D4ED8)
    public static readonly Color PrimaryLight = Color.FromArgb(230, 250, 248); // Açık Mavi Zemin
    public static readonly Color Secondary = Color.FromArgb(100, 116, 139);  // Slate Gri (#64748B)

    // Durum Renkleri
    public static readonly Color Success = Color.FromArgb(16, 185, 129);     // Zümrüt Yeşili (#10B981)
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);     // Kehribar Turuncu (#F59E0B)
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);       // Kırmızı (#EF4444)
    public static readonly Color Info = Color.FromArgb(20, 176, 186);         // Açık Mavi (#0EA5E9)

    // Zemin ve Metin Renkleri
    public static readonly Color Background = Color.FromArgb(248, 250, 252); // Çok açık gri (#F8FAFC)
    public static readonly Color CardBg = Color.White;
    public static readonly Color SidebarBg = Color.FromArgb(15, 23, 42);     // Koyu Lacivert/Siyah (#0F172A)
    public static readonly Color SidebarHover = Color.FromArgb(30, 41, 59);  // (#1E293B)
    public static readonly Color SidebarActive = Color.FromArgb(14, 154, 167);
    public static readonly Color BorderColor = Color.FromArgb(226, 232, 240); // (#E2E8F0)
    
    public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);   // (#0F172A)
    public static readonly Color TextSecondary = Color.FromArgb(100, 116, 139);
    public static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

    // Fontlar
    public static readonly Font HeaderFont = new("Segoe UI", 16, FontStyle.Bold);
    public static readonly Font SubHeaderFont = new("Segoe UI", 12, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font RegularFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font KpiValueFont = new("Segoe UI", 15f, FontStyle.Bold);

    public static void ApplyGridStyle(DataGridView grid)
    {
        // Donanım hızlandırma ve titremeyi önleme (DoubleBuffering)
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
        grid.RowTemplate.Height = 36;
        grid.Dock = DockStyle.Fill;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;

        // Başlık stili
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        grid.ColumnHeadersHeight = 40;

        // Satır stilleri
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 246, 244);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(8, 74, 84);
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 246, 244);
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.FromArgb(8, 74, 84);
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
