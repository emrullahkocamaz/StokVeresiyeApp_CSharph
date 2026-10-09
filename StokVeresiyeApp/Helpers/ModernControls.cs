using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace StokVeresiyeApp.Helpers;

public class StatCard : Panel
{
    private readonly Label _lblTitle = new();
    private readonly Label _lblValue = new();
    private readonly Label _lblSub = new();
    private Color _accentColor = UITheme.Primary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Title { get => _lblTitle.Text; set => _lblTitle.Text = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ValueText { get => _lblValue.Text; set => _lblValue.Text = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SubText { get => _lblSub.Text; set => _lblSub.Text = value; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get => _accentColor; set { _accentColor = value; Invalidate(); } }

    public StatCard()
    {
        Width = 240;
        Height = 100;
        BackColor = UITheme.CardBg;
        Padding = new Padding(16, 12, 12, 12);
        Margin = new Padding(8);

        _lblTitle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _lblTitle.ForeColor = UITheme.TextSecondary;
        _lblTitle.Dock = DockStyle.Top;
        _lblTitle.Height = 20;

        _lblValue.Font = UITheme.KpiValueFont;
        _lblValue.ForeColor = UITheme.TextPrimary;
        _lblValue.Dock = DockStyle.Fill;
        _lblValue.TextAlign = ContentAlignment.MiddleLeft;

        _lblSub.Font = UITheme.SmallFont;
        _lblSub.ForeColor = UITheme.TextMuted;
        _lblSub.Dock = DockStyle.Bottom;
        _lblSub.Height = 18;

        Controls.Add(_lblValue);
        Controls.Add(_lblSub);
        Controls.Add(_lblTitle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // Arka plan & sınır çizimi
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(UITheme.BorderColor, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        // Sol renkli şerit
        using var brush = new SolidBrush(_accentColor);
        e.Graphics.FillRectangle(brush, 0, 0, 5, Height);
    }
}

public class SidebarNavButton : Control
{
    private bool _isHovered = false;
    private bool _isActive = false;
    private string _icon = "📦";
    private string _badgeText = "";
    private Color _badgeColor = UITheme.Danger;
    private bool _isCollapsed = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Icon { get => _icon; set { _icon = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsActive { get => _isActive; set { _isActive = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string BadgeText { get => _badgeText; set { _badgeText = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BadgeColor { get => _badgeColor; set { _badgeColor = value; Invalidate(); } }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCollapsed { get => _isCollapsed; set { _isCollapsed = value; Invalidate(); } }

    private static readonly Font _navFontRegular = new("Segoe UI", 9.5f, FontStyle.Regular);
    private static readonly Font _navFontBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    private static readonly Font _badgeFont = new("Segoe UI", 8f, FontStyle.Bold);
    private static readonly Font _iconFont = UITheme.GetIconFont(12f);

    public SidebarNavButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | 
                 ControlStyles.OptimizedDoubleBuffer | 
                 ControlStyles.UserPaint | 
                 ControlStyles.ResizeRedraw, true);
        Height = 40;
        Width = 200;
        Cursor = Cursors.Hand;
        Font = _navFontRegular;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Arka plan
        Color bgColor = Color.Transparent;
        if (_isActive)
            bgColor = UITheme.SidebarHover;
        else if (_isHovered)
            bgColor = Color.FromArgb(24, 33, 47);

        if (bgColor != Color.Transparent)
        {
            using var brush = new SolidBrush(bgColor);
            g.FillRectangle(brush, 0, 0, Width, Height);
        }

        // Aktif durum sol dikey çizgi
        if (_isActive)
        {
            using var activeBarBrush = new SolidBrush(UITheme.Primary);
            g.FillRectangle(activeBarBrush, 0, 3, 3, Height - 6);
        }

        // İkon (Standart font)
        int iconX = _isCollapsed ? (Width - 20) / 2 : 12;
        Color iconColor = _isActive ? Color.White : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(148, 163, 184));
        using (var iconBrush = new SolidBrush(iconColor))
        {
            var iconRect = new Rectangle(iconX, 0, 24, Height);
            var sfIcon = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(_icon, _iconFont, iconBrush, iconRect, sfIcon);
        }

        // Metin (Geniş modda)
        if (!_isCollapsed)
        {
            Color textColor = _isActive ? Color.White : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(148, 163, 184));
            var font = _isActive ? _navFontBold : _navFontRegular;
            int reservedWidth = 52;
            if (!string.IsNullOrEmpty(_badgeText))
                reservedWidth += 56;

            var textRect = new Rectangle(40, 0, Math.Max(0, Width - reservedWidth), Height);
            TextRenderer.DrawText(
                g,
                Text,
                font,
                textRect,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
            );

            // Rozet (Badge)
            if (!string.IsNullOrEmpty(_badgeText))
            {
                var badgeSize = g.MeasureString(_badgeText, _badgeFont);
                int bWidth = Math.Max((int)badgeSize.Width + 8, 18);
                int bHeight = 16;
                int bX = Width - bWidth - 10;
                int bY = (Height - bHeight) / 2;

                using var badgeBrush = new SolidBrush(_badgeColor);
                using var path = new GraphicsPath();
                int radius = bHeight / 2;
                path.AddArc(bX, bY, radius * 2, radius * 2, 90, 180);
                path.AddArc(bX + bWidth - radius * 2, bY, radius * 2, radius * 2, 270, 180);
                path.CloseFigure();
                g.FillPath(badgeBrush, path);

                using var badgeTextBrush = new SolidBrush(Color.White);
                var sfBadge = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(_badgeText, _badgeFont, badgeTextBrush, new Rectangle(bX, bY, bWidth, bHeight), sfBadge);
            }
        }
    }
}

public class SidebarSectionTitle : Control
{
    private static readonly Font _secFont = new("Segoe UI", 7.5f, FontStyle.Bold);
    private bool _isCollapsed = false;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsCollapsed { get => _isCollapsed; set { _isCollapsed = value; Invalidate(); } }

    public SidebarSectionTitle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Height = 24;
        Width = 206;
        Font = _secFont;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_isCollapsed) return;

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var textBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
        var textRect = new Rectangle(14, 0, Math.Max(0, Width - 28), Height);
        TextRenderer.DrawText(
            g,
            Text.ToUpperInvariant(),
            _secFont,
            textRect,
            Color.FromArgb(100, 116, 139),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix
        );
    }
}

public class CardPanel : Panel
{
    public CardPanel()
    {
        BackColor = UITheme.CardBg;
        Padding = new Padding(16);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(UITheme.BorderColor, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
