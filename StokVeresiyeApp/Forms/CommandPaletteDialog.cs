using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class PaletteItem
{
    public string Icon { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public Action? Run { get; init; }
    public override string ToString() => $"{Icon} {Title}";
}

/// <summary>Ctrl+K arama paleti: menü komutlarını, ürünleri ve carileri tek kutudan arar.</summary>
public class CommandPaletteDialog : Form
{
    private readonly TextBox _txt = new();
    private readonly ListBox _list = new();
    private readonly Label _lblHint = new();
    private readonly IReadOnlyList<PaletteItem> _commands;
    private readonly Func<string, IEnumerable<PaletteItem>> _dataSearch;
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 220 };

    public PaletteItem? Selected { get; private set; }

    public CommandPaletteDialog(IReadOnlyList<PaletteItem> commands, Func<string, IEnumerable<PaletteItem>> dataSearch)
    {
        _commands = commands;
        _dataSearch = dataSearch;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        KeyPreview = true;
        Size = new Size(640, 440);
        BackColor = UITheme.BorderColor;
        Padding = new Padding(1);
        Font = UITheme.RegularFont;

        var body = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.CardBg };

        _txt.Dock = DockStyle.Top;
        _txt.Font = new Font("Segoe UI", 13f);
        _txt.BorderStyle = BorderStyle.FixedSingle;
        _txt.PlaceholderText = "Komut, ürün veya cari ara...";

        var txtHost = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(14, 12, 14, 8), BackColor = UITheme.CardBg };
        txtHost.Controls.Add(_txt);

        _lblHint.Dock = DockStyle.Bottom;
        _lblHint.Height = 28;
        _lblHint.Font = UITheme.SmallFont;
        _lblHint.ForeColor = UITheme.TextMuted;
        _lblHint.TextAlign = ContentAlignment.MiddleLeft;
        _lblHint.Padding = new Padding(14, 0, 0, 0);
        _lblHint.Text = "↑↓ gezin   ·   Enter aç   ·   Esc kapat   ·   Ürün/cari için en az 2 harf yazın";

        _list.Dock = DockStyle.Fill;
        _list.BorderStyle = BorderStyle.None;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 44;
        _list.BackColor = UITheme.CardBg;
        _list.DrawItem += DrawItem;
        _list.DoubleClick += (s, e) => Accept();
        _list.MouseMove += (s, e) =>
        {
            int idx = _list.IndexFromPoint(e.Location);
            if (idx >= 0 && idx != _list.SelectedIndex) _list.SelectedIndex = idx;
        };

        body.Controls.Add(_list);
        body.Controls.Add(_lblHint);
        body.Controls.Add(txtHost);
        Controls.Add(body);

        _txt.TextChanged += (s, e) => { _debounce.Stop(); _debounce.Start(); RebuildCommandsOnly(); };
        _debounce.Tick += (s, e) => { _debounce.Stop(); Rebuild(); };

        _txt.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down) { Move(1); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Move(-1); e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = e.SuppressKeyPress = true; }
        };
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        };
        Deactivate += (s, e) => { if (DialogResult == DialogResult.None) Close(); };
        Shown += (s, e) => _txt.Focus();

        RebuildCommandsOnly();
    }

    public void PositionOver(Form owner)
    {
        var b = owner.Bounds;
        Location = new Point(b.Left + (b.Width - Width) / 2, b.Top + Math.Max(60, b.Height / 8));
    }

    private void Move(int delta)
    {
        if (_list.Items.Count == 0) return;
        int i = Math.Clamp(_list.SelectedIndex + delta, 0, _list.Items.Count - 1);
        _list.SelectedIndex = i;
    }

    private void Accept()
    {
        if (_list.SelectedItem is not PaletteItem item) return;
        Selected = item;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string Fold(string s) =>
        s.ToLowerInvariant().Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');

    private List<PaletteItem> MatchCommands()
    {
        string q = Fold(_txt.Text.Trim());
        if (q.Length == 0) return _commands.Take(12).ToList();

        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return _commands
            .Select(c => (c, hay: Fold(c.Title + " " + c.Subtitle)))
            .Where(x => words.All(w => x.hay.Contains(w)))
            .OrderBy(x => x.hay.StartsWith(words[0]) ? 0 : 1)
            .ThenBy(x => x.c.Title.Length)
            .Take(10)
            .Select(x => x.c)
            .ToList();
    }

    // Yazarken komutlar anında, ürün/cari araması kısa bekleme sonrası gelir.
    private void RebuildCommandsOnly() => Fill(MatchCommands(), Array.Empty<PaletteItem>());

    private void Rebuild()
    {
        var data = new List<PaletteItem>();
        if (_txt.Text.Trim().Length >= 2)
        {
            try { data.AddRange(_dataSearch(_txt.Text.Trim())); } catch { }
        }
        Fill(MatchCommands(), data);
    }

    private void Fill(IEnumerable<PaletteItem> cmds, IEnumerable<PaletteItem> data)
    {
        var prev = (_list.SelectedItem as PaletteItem)?.Title;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var c in cmds) _list.Items.Add(c);
        foreach (var d in data) _list.Items.Add(d);
        int keep = prev == null ? -1 : _list.Items.Cast<PaletteItem>().ToList().FindIndex(p => p.Title == prev);
        _list.EndUpdate();
        if (_list.Items.Count > 0) _list.SelectedIndex = keep >= 0 ? keep : 0;
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var item = (PaletteItem)_list.Items[e.Index];
        bool sel = (e.State & DrawItemState.Selected) != 0;

        using (var bg = new SolidBrush(sel ? UITheme.PrimaryLight : UITheme.CardBg))
            e.Graphics.FillRectangle(bg, e.Bounds);

        using var titleBrush = new SolidBrush(UITheme.TextPrimary);
        using var subBrush = new SolidBrush(UITheme.TextMuted);
        using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold);

        var iconRect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, 34, e.Bounds.Height);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(item.Icon, new Font("Segoe UI Emoji", 12f), titleBrush, iconRect, sf);

        var titleRect = new RectangleF(e.Bounds.X + 50, e.Bounds.Y + 5, e.Bounds.Width - 60, 20);
        var subRect = new RectangleF(e.Bounds.X + 50, e.Bounds.Y + 24, e.Bounds.Width - 60, 18);
        var trim = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        e.Graphics.DrawString(item.Title, titleFont, titleBrush, titleRect, trim);
        e.Graphics.DrawString(item.Subtitle, UITheme.SmallFont, subBrush, subRect, trim);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _debounce.Dispose();
        base.Dispose(disposing);
    }
}
