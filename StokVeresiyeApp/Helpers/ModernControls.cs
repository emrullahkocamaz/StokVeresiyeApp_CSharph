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
