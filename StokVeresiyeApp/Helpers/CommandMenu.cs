namespace StokVeresiyeApp.Helpers;

// Eski ribbon yapısının yerine geçen hafif komut modeli: sekme > grup > düğme.
// MainForm bu modeli doldurur, "Tüm İşlemler" menüsü ise ContextMenuStrip olarak gösterir.

public class CmdButton
{
    public string TextLine1 { get; set; } = string.Empty;
    public string TextLine2 { get; set; } = string.Empty;
    public Image? ImageLarge { get; set; }
    public Image? ImageSmall { get; set; }
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public event EventHandler? Click;

    public void Invoke() => Click?.Invoke(this, EventArgs.Empty);
}

public class CmdTriple
{
    public List<CmdButton> Items { get; } = new();
}

public class CmdGroup
{
    public string TextLine1 { get; set; } = string.Empty;
    public List<CmdTriple> Items { get; } = new();
}

public class CmdTab
{
    public string Text { get; set; } = string.Empty;
    public List<CmdGroup> Groups { get; } = new();
}

public class CmdRegistry
{
    public List<CmdTab> RibbonTabs { get; } = new();

    /// <summary>Tüm komutları sekme > grup başlıklı alt menüler halinde ContextMenuStrip olarak üretir.</summary>
    public ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            Font = UITheme.RegularFont,
            ShowImageMargin = true,
            ImageScalingSize = new Size(16, 16),
            BackColor = UITheme.CardBg,
            ForeColor = UITheme.TextPrimary
        };

        // Aynı komut birden çok sekmede tanımlıysa menüde tek sefer gösterilir
        var seen = new HashSet<string>();

        foreach (var tab in RibbonTabs)
        {
            var tabItem = new ToolStripMenuItem(tab.Text);
            foreach (var group in tab.Groups)
            {
                var buttons = group.Items.SelectMany(t => t.Items).Where(b => b.Visible).Where(b => seen.Add(b.TextLine1)).ToList();
                if (buttons.Count == 0) continue;

                if (tabItem.DropDownItems.Count > 0) tabItem.DropDownItems.Add(new ToolStripSeparator());
                tabItem.DropDownItems.Add(new ToolStripLabel(group.TextLine1) { ForeColor = UITheme.TextMuted, Font = UITheme.SmallFont });

                foreach (var b in buttons)
                {
                    string text = string.IsNullOrWhiteSpace(b.TextLine2) ? b.TextLine1 : $"{b.TextLine1}   ·   {b.TextLine2}";
                    var item = new ToolStripMenuItem(text, b.ImageSmall) { Enabled = b.Enabled };
                    var captured = b;
                    item.Click += (s, e) => captured.Invoke();
                    tabItem.DropDownItems.Add(item);
                }
            }
            if (tabItem.DropDownItems.Count > 0) menu.Items.Add(tabItem);
        }
        return menu;
    }
}
