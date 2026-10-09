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
        MinimumSize = new Size(900, 620);
        MaximumSize = new Size(Screen.PrimaryScreen?.WorkingArea.Width ?? width, Screen.PrimaryScreen?.WorkingArea.Height ?? height);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
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
            Name = "bodyPanel",
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
        // Arka plan katmanları önce, içerik alanı en üstte olacak şekilde set edilir.
        Controls.Add(scrollContainer);
        Controls.Add(bottomPanel);
        Controls.Add(headerPanel);
        Controls.SetChildIndex(headerPanel, 0);
        Controls.SetChildIndex(bottomPanel, 1);
        Controls.SetChildIndex(scrollContainer, 2);

        AcceptButton = BtnSave;
        CancelButton = BtnCancel;
    }

    protected void AddRow(string label, Control control, int rowHeight = 38)
    {
        int row = ContentTable.RowCount++;

        control.Dock = DockStyle.Fill;
        control.Font = UITheme.RegularFont;

        int effectiveRowHeight = Math.Max(rowHeight, Math.Max(control.Height, 28) + 8);
        ContentTable.RowStyles.Add(new RowStyle(SizeType.Absolute, effectiveRowHeight));

        var lbl = new Label
        {
            Text = label,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextSecondary
        };

        ContentTable.Controls.Add(lbl, 0, row);
        ContentTable.Controls.Add(control, 1, row);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        AdjustSizeToContent();
    }

    public virtual void AdjustSizeToContent()
    {
        try
        {
            // Eğer ContentTable kullanılmıyorsa veya satır eklenmemişse alt sınıfın kendi boyutunu koru
            if (!ContentTable.Visible || ContentTable.RowCount == 0)
            {
                return;
            }

            // ContentTable satırlarının ve elemanlarının toplam yüksekliğini hesapla
            int tableH = 0;
            foreach (RowStyle rs in ContentTable.RowStyles)
            {
                tableH += (int)rs.Height;
            }
            if (tableH == 0 && ContentTable.RowCount > 0)
            {
                tableH = ContentTable.RowCount * 40;
            }

            // Başlık (60) + Buton paneli (65) + Tablo yüksekliği + İç boşluklar (45)
            int neededHeight = 60 + 65 + tableH + 45;

            var screenArea = Screen.FromControl(this).WorkingArea;
            int maxHeight = (int)(screenArea.Height * 0.90);
            int minHeight = 350;

            int finalHeight = Math.Clamp(neededHeight, minHeight, maxHeight);
            int preferredWidth = Math.Min(ClientSize.Width, screenArea.Width - 60);
            int targetWidth = Math.Clamp(preferredWidth, 900, screenArea.Width - 30);

            ClientSize = new Size(targetWidth, finalHeight);

            // Ekran veya ana pencere ortasına yeniden hizala
            if (Owner != null && Owner.Visible)
            {
                Location = new Point(
                    Owner.Location.X + (Owner.Width - Width) / 2,
                    Math.Max(screenArea.Top + 10, Owner.Location.Y + (Owner.Height - Height) / 2)
                );
            }
            else
            {
                Location = new Point(
                    screenArea.Left + (screenArea.Width - Width) / 2,
                    screenArea.Top + (screenArea.Height - Height) / 2
                );
            }
        }
        catch { }
    }

    protected static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return double.TryParse(text.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
    }
}
