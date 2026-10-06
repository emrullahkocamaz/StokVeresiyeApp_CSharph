using Krypton.Toolkit;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class BaseModernForm : KryptonForm
{
    protected TableLayoutPanel ContentTable = new();
    protected KryptonButton BtnSave = new();
    protected KryptonButton BtnCancel = new();

    public BaseModernForm(string title, int width = 640, int height = 560)
    {
        Text = title;
        ClientSize = new Size(width, height);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        // 1. Üst Başlık Paneli (Üstte Sabit)
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 0, 20, 0)
        };
        var lblTitle = new Label
        {
            Text = title,
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        headerPanel.Controls.Add(lblTitle);

        // 2. Alt Butonlar Paneli (Altta Her Zaman Sabit ve Net Görünür)
        var bottomPanel = new Panel
        {
            Name = "actionPanel",
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 14, 20, 14)
        };

        BtnSave = UITheme.CreateKryptonButton("Kaydet", UITheme.Primary, Color.White, (s, e) => { }, 120, 36);
        BtnSave.DialogResult = DialogResult.OK;
        BtnSave.Dock = DockStyle.Right;

        BtnCancel = UITheme.CreateKryptonButton("İptal", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { DialogResult = DialogResult.Cancel; Close(); }, 100, 36);
        BtnCancel.Dock = DockStyle.Right;

        bottomPanel.Controls.Add(BtnCancel);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 12 });
        bottomPanel.Controls.Add(BtnSave);

        // 3. Orta Kaydırılabilir İçerik Alanı (Asla tabana taşmaz, butonları örtmez)
        var scrollContainer = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20, 15, 20, 15)
        };

        ContentTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0)
        };
        ContentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        ContentTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        scrollContainer.Controls.Add(ContentTable);

        // Doğru Docking Sırası:
        // Önce Fill olan scrollContainer, sonra Bottom, sonra Top eklenir ve Top/Bottom SendToBack yapılır.
        Controls.Add(scrollContainer);
        Controls.Add(bottomPanel);
        Controls.Add(headerPanel);
        headerPanel.SendToBack();
        bottomPanel.SendToBack();

        AcceptButton = BtnSave;
        CancelButton = BtnCancel;
    }

    protected void AddRow(string label, Control control, int rowHeight = 38)
    {
        int row = ContentTable.RowCount++;
        ContentTable.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

        var lbl = new Label
        {
            Text = label,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextSecondary
        };

        control.Dock = DockStyle.Fill;
        control.Font = UITheme.RegularFont;

        ContentTable.Controls.Add(lbl, 0, row);
        ContentTable.Controls.Add(control, 1, row);
    }

    protected static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return double.TryParse(text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
    }
}
