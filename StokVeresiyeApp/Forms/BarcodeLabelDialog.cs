using System.Drawing;
using System.Drawing.Printing;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Forms;

public class BarcodeLabelDialog : Form
{
    private readonly Product _product;

    private readonly ComboBox _cmbTemplate = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _nudCopies = new() { Minimum = 1, Maximum = 9999, Value = 1 };
    private readonly PictureBox _picPreview = new() { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.CenterImage };
    private readonly CheckBox _chkShowPrice = new() { Text = "Fiyatı Etikete Dahil Et", Checked = true, AutoSize = true };
    private readonly TextBox _txtCustomTitle = new();
    private readonly TextBox _txtCustomPrice = new();

    public BarcodeLabelDialog(Product product)
    {
        _product = product;
        Text = $"Barkod & Raf Etiketi Yazdır - {_product.Name}";
        ClientSize = new Size(720, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        _txtCustomTitle.Text = _product.Name;
        _txtCustomPrice.Text = _product.SalePrice > 0 ? _product.SalePrice.ToString("N2") : _product.PurchasePrice.ToString("N2");

        BuildUI();
        UpdatePreview();
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new CardPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(20, 12, 20, 12) };
        var lblTitle = new Label { Text = "🏷️ Barkod ve Raf Etiketi Yazdırma", Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = "Termal rulo etiket veya raf etiketi formatında barkod çıktısı alabilirsiniz.", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Alt Butonlar
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = UITheme.CardBg, Padding = new Padding(15, 12, 15, 12) };
        var btnPrint = UITheme.CreateButton("🖨️ Yazdır", UITheme.Primary, Color.White, PrintClick, 130, 36);
        var btnPreview = UITheme.CreateButton("👁️ Sayfa Önizleme", UITheme.Info, Color.White, PreviewClick, 150, 36);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 36);

        btnPrint.Dock = DockStyle.Right;
        btnPreview.Dock = DockStyle.Right;
        btnClose.Dock = DockStyle.Left;

        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(btnPreview);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        bottom.Controls.Add(btnPrint);
        Controls.Add(bottom);

        // 3. İçerik (Sol Ayarlar, Sağ Önizleme)
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 15, 20, 15) };

        // Sol Ayarlar Paneli
        var pnlLeft = new Panel { Left = 20, Top = 15, Width = 310, Height = 390 };
        int top = 0;

        pnlLeft.Controls.Add(new Label { Text = "Etiket Şablonu:", Font = UITheme.TitleFont, Left = 0, Top = top, Width = 300, Height = 20 });
        top += 22;
        _cmbTemplate.Left = 0;
        _cmbTemplate.Top = top;
        _cmbTemplate.Width = 300;
        _cmbTemplate.Items.AddRange(new object[]
        {
            "Termal Etiket (50x30 mm)",
            "Termal Etiket (40x20 mm)",
            "Raf Etiketi (Büyük Fiyatlı - 70x45 mm)",
            "A4 Standart Kağıt"
        });
        _cmbTemplate.SelectedIndex = 0;
        _cmbTemplate.SelectedIndexChanged += (s, e) => UpdatePreview();
        pnlLeft.Controls.Add(_cmbTemplate);

        top += 36;
        pnlLeft.Controls.Add(new Label { Text = "Etiket Başlığı / Ürün Adı:", Font = UITheme.TitleFont, Left = 0, Top = top, Width = 300, Height = 20 });
        top += 22;
        _txtCustomTitle.Left = 0;
        _txtCustomTitle.Top = top;
        _txtCustomTitle.Width = 300;
        _txtCustomTitle.TextChanged += (s, e) => UpdatePreview();
        pnlLeft.Controls.Add(_txtCustomTitle);

        top += 36;
        pnlLeft.Controls.Add(new Label { Text = "Etiket Fiyatı (₺):", Font = UITheme.TitleFont, Left = 0, Top = top, Width = 300, Height = 20 });
        top += 22;
        _txtCustomPrice.Left = 0;
        _txtCustomPrice.Top = top;
        _txtCustomPrice.Width = 300;
        _txtCustomPrice.TextChanged += (s, e) => UpdatePreview();
        pnlLeft.Controls.Add(_txtCustomPrice);

        top += 36;
        pnlLeft.Controls.Add(new Label { Text = "Yazdırılacak Adet (Kopya):", Font = UITheme.TitleFont, Left = 0, Top = top, Width = 300, Height = 20 });
        top += 22;
        _nudCopies.Left = 0;
        _nudCopies.Top = top;
        _nudCopies.Width = 140;
        pnlLeft.Controls.Add(_nudCopies);

        top += 38;
        _chkShowPrice.Left = 0;
        _chkShowPrice.Top = top;
        _chkShowPrice.CheckedChanged += (s, e) => UpdatePreview();
        pnlLeft.Controls.Add(_chkShowPrice);

        content.Controls.Add(pnlLeft);

        // Sağ Canlı Önizleme Paneli
        var pnlRight = new CardPanel { Left = 350, Top = 15, Width = 330, Height = 390, Padding = new Padding(15) };
        var lblPrevTitle = new Label { Text = "👁️ Canlı Etiket Görünümü:", Font = UITheme.TitleFont, ForeColor = UITheme.PrimaryDark, Dock = DockStyle.Top, Height = 25 };
        _picPreview.Dock = DockStyle.Fill;
        pnlRight.Controls.Add(_picPreview);
        pnlRight.Controls.Add(lblPrevTitle);

        content.Controls.Add(pnlRight);
        Controls.Add(content);

        content.BringToFront();
        header.SendToBack();
        bottom.SendToBack();
    }

    private Size GetLabelPixelSize()
    {
        string t = _cmbTemplate.SelectedItem?.ToString() ?? "";
        if (t.Contains("40x20")) return new Size(220, 120);
        if (t.Contains("70x45") || t.Contains("Raf")) return new Size(280, 180);
        return new Size(260, 150); // 50x30
    }

    private void UpdatePreview()
    {
        try
        {
            var size = GetLabelPixelSize();
            var bmp = new Bitmap(size.Width, size.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            string title = _txtCustomTitle.Text.Trim();
            string barcode = !string.IsNullOrWhiteSpace(_product.Barcode) ? _product.Barcode : _product.Code;
            double price = _chkShowPrice.Checked ? ParseDouble(_txtCustomPrice.Text) : 0;
            string template = _cmbTemplate.SelectedItem?.ToString() ?? "";

            BarcodeRenderer.DrawProductLabel(g, title, barcode, price, template, new Rectangle(0, 0, size.Width, size.Height));

            _picPreview.Image?.Dispose();
            _picPreview.Image = bmp;
        }
        catch { }
    }

    private double ParseDouble(string str)
    {
        if (double.TryParse(str.Replace(".", ","), out double val)) return val;
        if (double.TryParse(str.Replace(",", "."), out double val2)) return val2;
        return 0;
    }

    private PrintDocument CreatePrintDoc()
    {
        var doc = new PrintDocument();
        int printedCount = 0;
        int targetCopies = (int)_nudCopies.Value;

        doc.PrintPage += (s, e) =>
        {
            string title = _txtCustomTitle.Text.Trim();
            string barcode = !string.IsNullOrWhiteSpace(_product.Barcode) ? _product.Barcode : _product.Code;
            double price = _chkShowPrice.Checked ? ParseDouble(_txtCustomPrice.Text) : 0;
            string template = _cmbTemplate.SelectedItem?.ToString() ?? "";

            var size = GetLabelPixelSize();

            if (template.Contains("A4"))
            {
                // A4 kağıt üzerinde çoklu ızgara baskısı (3 sütun x 8 satır = 24 etiket)
                int cols = 3;
                int rows = 8;
                int startX = 40;
                int startY = 40;
                int labelW = 230;
                int labelH = 120;
                int gapX = 15;
                int gapY = 12;

                int pageMax = cols * rows;
                int onThisPage = 0;

                while (printedCount < targetCopies && onThisPage < pageMax)
                {
                    int col = onThisPage % cols;
                    int row = onThisPage / cols;

                    int x = startX + col * (labelW + gapX);
                    int y = startY + row * (labelH + gapY);

                    BarcodeRenderer.DrawProductLabel(e.Graphics!, title, barcode, price, template, new Rectangle(x, y, labelW, labelH));

                    printedCount++;
                    onThisPage++;
                }

                e.HasMorePages = printedCount < targetCopies;
            }
            else
            {
                // Termal Rulo Etiket: Her sayfaya 1 etiket
                BarcodeRenderer.DrawProductLabel(e.Graphics!, title, barcode, price, template, new Rectangle(10, 10, size.Width, size.Height));
                printedCount++;
                e.HasMorePages = printedCount < targetCopies;
            }
        };

        return doc;
    }

    private void PrintClick(object? sender, EventArgs e)
    {
        using var pd = new PrintDialog();
        using var doc = CreatePrintDoc();
        pd.Document = doc;

        if (pd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                doc.Print();
                MessageBox.Show("Barkod etiketleri yazıcıya gönderildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yazdırma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PreviewClick(object? sender, EventArgs e)
    {
        try
        {
            using var doc = CreatePrintDoc();
            using var ppd = new PrintPreviewDialog
            {
                Document = doc,
                Width = 900,
                Height = 700,
                StartPosition = FormStartPosition.CenterParent
            };
            ppd.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Önizleme oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
