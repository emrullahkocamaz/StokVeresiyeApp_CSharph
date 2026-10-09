using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class InvoiceTextImportDialog : BaseModernForm
{
    private readonly TextBox _txtRawInput = new();
    private readonly DataGridView _gridPreview = new();
    private readonly ComboBox _cmbDefaultVat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly Label _lblSummary = new() 
    { 
        Text = "0 Kalem Ayrıştırıldı | Toplam: 0,00 ₺", 
        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), 
        ForeColor = UITheme.Primary, 
        AutoSize = true 
    };

    private readonly DataTable _tablePreview = new();
    public List<ParsedInvoiceItem> ExtractedItems { get; private set; } = new();

    public InvoiceTextImportDialog(string? initialText = null) 
        : base("📋 Faturadan Metin / Tablo Kopyala-Yapıştır ile Kalem Ekleme & Sağlama Sihirbazı", 1120, 720)
    {
        InitializePreviewTable();
        BuildLayout();

        if (!string.IsNullOrWhiteSpace(initialText))
        {
            _txtRawInput.Text = initialText;
            ParseTextLines();
        }
        else
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(clip) && clip.Length > 10)
                    {
                        _txtRawInput.Text = clip;
                        ParseTextLines();
                    }
                }
            }
            catch { }
        }
    }

    private void InitializePreviewTable()
    {
        _tablePreview.Columns.Add("LineNo", typeof(int));
        _tablePreview.Columns.Add("Barcode", typeof(string));
        _tablePreview.Columns.Add("ItemCode", typeof(string));
        _tablePreview.Columns.Add("ItemName", typeof(string));
        _tablePreview.Columns.Add("Quantity", typeof(double));
        _tablePreview.Columns.Add("Unit", typeof(string));
        _tablePreview.Columns.Add("UnitPrice", typeof(double));
        _tablePreview.Columns.Add("VatPercent", typeof(double));
        _tablePreview.Columns.Add("LineTotal", typeof(double));

        _gridPreview.DataSource = _tablePreview;
    }

    private void BuildLayout()
    {
        var bodyPanel = Controls.Find("bodyPanel", true).FirstOrDefault() as Panel;
        if (bodyPanel != null)
        {
            bodyPanel.Controls.Clear();
        }
        else
        {
            bodyPanel = new Panel { Dock = DockStyle.Fill };
            Controls.Add(bodyPanel);
            bodyPanel.BringToFront();
        }

        // 1. Bilgilendirme Banner
        var infoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(238, 242, 255),
            Padding = new Padding(12, 8, 12, 8)
        };
        var lblInfo = new Label
        {
            Text = "💡 İpucu: PDF faturanızdaki ürün tablosunu fareyle seçip kopyalayın (Ctrl+C), sol kutucuğa yapıştırın (Ctrl+V). Sistem tüm ürünleri, adetleri ve fiyatları anında tespit eder.",
            Font = UITheme.RegularFont,
            ForeColor = Color.FromArgb(67, 56, 202),
            Dock = DockStyle.Fill
        };
        infoPanel.Controls.Add(lblInfo);

        // 2. Ana Çalışma Alanı (Split / Çift Panel: Sol Metin, Sağ Önizleme Tablosu)
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 440,
            SplitterWidth = 6,
            Padding = new Padding(8)
        };

        // Sol Panel: Yapıştırma Alanı
        var pnlLeft = new Panel { Dock = DockStyle.Fill };
        var lblPasteTitle = new Label
        {
            Text = "📥 1. Kopyalanan Fatura Metnini Buraya Yapıştırın:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 26
        };

        _txtRawInput.Dock = DockStyle.Fill;
        _txtRawInput.Multiline = true;
        _txtRawInput.ScrollBars = ScrollBars.Both;
        _txtRawInput.Font = new Font("Consolas", 9f);
        _txtRawInput.TextChanged += (s, e) => ParseTextLines();

        var pnlLeftToolbar = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(0, 6, 0, 0) };
        var btnPasteClipboard = UITheme.CreateButton("📋 Panodan Yapıştır", UITheme.Primary, Color.White, (s, e) =>
        {
            if (Clipboard.ContainsText())
            {
                _txtRawInput.Text = Clipboard.GetText();
            }
        }, 145, 34);

        var btnClearText = UITheme.CreateButton("🧹 Temizle", UITheme.Secondary, Color.White, (s, e) =>
        {
            _txtRawInput.Clear();
        }, 90, 34);

        var btnReparse = UITheme.CreateButton("⚡ Yeniden Ayrıştır", Color.FromArgb(16, 185, 129), Color.White, (s, e) =>
        {
            ParseTextLines();
        }, 130, 34);

        var flowLeftBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flowLeftBtns.Controls.Add(btnPasteClipboard);
        flowLeftBtns.Controls.Add(btnClearText);
        flowLeftBtns.Controls.Add(btnReparse);
        pnlLeftToolbar.Controls.Add(flowLeftBtns);

        pnlLeft.Controls.Add(_txtRawInput);
        pnlLeft.Controls.Add(pnlLeftToolbar);
        pnlLeft.Controls.Add(lblPasteTitle);
        lblPasteTitle.BringToFront();
        pnlLeftToolbar.BringToFront();
        _txtRawInput.BringToFront();
        split.Panel1.Controls.Add(pnlLeft);

        // Sağ Panel: Ayrıştırılan Kalemler Önizlemesi
        var pnlRight = new Panel { Dock = DockStyle.Fill };
        var pnlRightHeader = new Panel { Dock = DockStyle.Top, Height = 32 };
        var lblGridTitle = new Label
        {
            Text = "📊 2. Ayrıştırılan Ürün Kalemleri Önizlemesi:",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Left,
            AutoSize = true
        };

        var flowVat = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        flowVat.Controls.Add(new Label { Text = "Varsayılan KDV:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), Font = UITheme.SmallFont });
        _cmbDefaultVat.Items.AddRange(new object[] { "%20", "%10", "%1", "%0" });
        _cmbDefaultVat.SelectedIndex = 0;
        _cmbDefaultVat.SelectedIndexChanged += (s, e) => ParseTextLines();
        flowVat.Controls.Add(_cmbDefaultVat);

        pnlRightHeader.Controls.Add(lblGridTitle);
        pnlRightHeader.Controls.Add(flowVat);

        UITheme.ApplyGridStyle(_gridPreview);
        _gridPreview.ReadOnly = false;
        _gridPreview.AllowUserToAddRows = false;
        FormatGridColumns();

        pnlRight.Controls.Add(_gridPreview);
        pnlRight.Controls.Add(pnlRightHeader);
        pnlRightHeader.BringToFront();
        _gridPreview.BringToFront();
        split.Panel2.Controls.Add(pnlRight);

        bodyPanel.Controls.Add(split);
        bodyPanel.Controls.Add(infoPanel);
        infoPanel.BringToFront();
        split.BringToFront();

        // Alt Butonlar
        BtnSave.Text = "✅ Bu Kalemleri Fatura Tablosuna Aktar";
        BtnSave.Width = 260;
        BtnSave.Click += TransferItemsClick;

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            _lblSummary.Location = new Point(16, 12);
            actionPanel.Controls.Add(_lblSummary);
            _lblSummary.BringToFront();
        }
    }

    private void FormatGridColumns()
    {
        if (_gridPreview.Columns.Count == 0) return;

        if (_gridPreview.Columns["LineNo"] is { } colNo) { colNo.HeaderText = "Sıra"; colNo.Width = 45; }
        if (_gridPreview.Columns["Barcode"] is { } colBar) { colBar.HeaderText = "Barkod"; colBar.Width = 110; }
        if (_gridPreview.Columns["ItemCode"] is { } colCode) { colCode.HeaderText = "Kod"; colCode.Width = 85; }
        if (_gridPreview.Columns["ItemName"] is { } colName) { colName.HeaderText = "Ürün Adı"; colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }
        if (_gridPreview.Columns["Quantity"] is { } colQty) { colQty.HeaderText = "Miktar"; colQty.Width = 65; colQty.DefaultCellStyle.Format = "N2"; }
        if (_gridPreview.Columns["Unit"] is { } colUnit) { colUnit.HeaderText = "Birim"; colUnit.Width = 55; }
        if (_gridPreview.Columns["UnitPrice"] is { } colPrice) { colPrice.HeaderText = "Fiyat (₺)"; colPrice.Width = 85; colPrice.DefaultCellStyle.Format = "N2"; }
        if (_gridPreview.Columns["VatPercent"] is { } colVat) { colVat.HeaderText = "KDV %"; colVat.Width = 55; }
        if (_gridPreview.Columns["LineTotal"] is { } colTot) { colTot.HeaderText = "Toplam (₺)"; colTot.Width = 95; colTot.DefaultCellStyle.Format = "N2"; }
    }

    private void ParseTextLines()
    {
        string raw = _txtRawInput.Text;
        _tablePreview.Rows.Clear();
        ExtractedItems.Clear();

        if (string.IsNullOrWhiteSpace(raw))
        {
            UpdateSummary();
            return;
        }

        double defaultVat = _cmbDefaultVat.SelectedIndex switch
        {
            1 => 10,
            2 => 1,
            3 => 0,
            _ => 20
        };

        var lines = raw.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        int lineCounter = 1;

        foreach (var line in lines)
        {
            string t = line.Trim();
            if (string.IsNullOrWhiteSpace(t) || t.Length < 3) continue;

            // Başlık veya alt toplam satırlarını atla
            if (Regex.IsMatch(t, @"^(Sıra|No\b|Mal\s*Hizmet|Ürün\s*Adı|Miktar|Birim|Fiyat|KDV|Tutar|Toplam|Matrah|Yalnız|Vergi)", RegexOptions.IgnoreCase))
                continue;

            var item = ParseSingleLine(t, lineCounter, defaultVat);
            if (item != null)
            {
                ExtractedItems.Add(item);
                var row = _tablePreview.NewRow();
                row["LineNo"] = item.LineNo;
                row["Barcode"] = item.Barcode ?? "";
                row["ItemCode"] = item.ItemCode ?? "";
                row["ItemName"] = item.ItemName;
                row["Quantity"] = item.Quantity;
                row["Unit"] = item.Unit;
                row["UnitPrice"] = item.UnitPrice;
                row["VatPercent"] = item.VatPercent;
                row["LineTotal"] = item.LineTotal;
                _tablePreview.Rows.Add(row);
                lineCounter++;
            }
        }

        UpdateSummary();
    }

    private ParsedInvoiceItem? ParseSingleLine(string line, int lineNo, double defaultVat)
    {
        var item = new ParsedInvoiceItem
        {
            LineNo = lineNo,
            Unit = "Adet",
            Quantity = 1.0,
            VatPercent = defaultVat
        };

        // 1. Birim ve Miktar Tespiti (Örn: "1 Adet", "2 Adet", "1,5 Kg")
        double detectedQty = 1.0;
        bool hasExplicitQty = false;
        string lineClean = line;

        var unitRegex = new Regex(@"(?<qty>\d+(?:[\.,]\d+)?)\s*(?<unit>ADET|Adet|adet|ADT|Adt|adt|KG|Kg|kg|KOLİ|Koli|koli|PAKET|Paket|paket|LİTRE|Litre|Lt|lt|METRE|Metre|Mt|mt|KUTU|Kutu|kutu)\b", RegexOptions.IgnoreCase);
        var unitMatch = unitRegex.Match(lineClean);
        if (unitMatch.Success)
        {
            if (TryParseFlexibleNumber(unitMatch.Groups["qty"].Value, out double q) && q > 0)
            {
                detectedQty = q;
                hasExplicitQty = true;
                item.Quantity = q;
                item.Unit = unitMatch.Groups["unit"].Value;
                lineClean = lineClean.Remove(unitMatch.Index, unitMatch.Length);
            }
        }

        // 2. Tab veya boşluklara göre ayır
        string[] parts;
        if (lineClean.Contains('\t'))
        {
            parts = lineClean.Split('\t').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
        }
        else
        {
            parts = Regex.Split(lineClean, @"\s{2,}").Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            if (parts.Length < 3)
            {
                parts = lineClean.Split(' ').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
            }
        }

        if (parts.Length == 0) return null;

        // Barkod tespiti (8-14 haneli rakamlar)
        string? detectedBarcode = null;
        for (int i = 0; i < parts.Length; i++)
        {
            if (Regex.IsMatch(parts[i], @"^\d{8,14}$"))
            {
                detectedBarcode = parts[i];
                break;
            }
        }
        item.Barcode = detectedBarcode;

        // İskonto % Tespiti (örn: %30,00)
        double detectedDisc = 0;
        var discMatch = Regex.Match(line, @"%\s*(\d+(?:[\.,]\d+)?)");
        if (discMatch.Success)
        {
            if (TryParseFlexibleNumber(discMatch.Groups[1].Value, out double dVal))
            {
                if (dVal > 0 && dVal != 1 && dVal != 10 && dVal != 20)
                {
                    detectedDisc = dVal;
                }
            }
        }
        item.DiscountPercent = detectedDisc;

        // Sayısal değerleri topla (Fiyat ve Tutar)
        var numbers = new List<double>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == detectedBarcode) continue;

            string cleanNum = parts[i].Replace("₺", "").Replace("TL", "").Replace("%", "").Trim();
            if (TryParseFlexibleNumber(cleanNum, out double val) && val > 0)
            {
                // İskonto yüzdesiyle aynı olan sayıyı fiyat listesine alma
                if (detectedDisc > 0 && Math.Abs(val - detectedDisc) < 0.01) continue;
                numbers.Add(val);
            }
        }

        // Metin parçalarını toplayarak Ürün Adı oluştur
        var textParts = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == detectedBarcode) continue;
            string p = parts[i];

            // Başlangıç sıra numarası
            if (i == 0 && int.TryParse(p, out int sNo) && sNo < 500)
            {
                item.LineNo = sNo;
                continue;
            }

            // Sayı, birim veya para birimi değilse ürün adına ekle
            if (!Regex.IsMatch(p, @"^(\d+(?:[\.,]\d+)?|TL|₺|%|Adet|Kg|Lt)$", RegexOptions.IgnoreCase))
            {
                textParts.Add(p);
            }
        }

        item.ItemName = string.Join(" ", textParts).Trim();
        if (string.IsNullOrWhiteSpace(item.ItemName))
        {
            item.ItemName = !string.IsNullOrWhiteSpace(detectedBarcode) ? $"Ürün {detectedBarcode}" : $"Ürün #{lineNo}";
        }

        // Sayıların Atanması (Birim Fiyat ve Net Tutar)
        if (numbers.Count == 1)
        {
            item.UnitPrice = numbers[0];
            item.LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
        }
        else if (numbers.Count == 2)
        {
            double n1 = numbers[0];
            double n2 = numbers[1];

            if (hasExplicitQty)
            {
                // Miktar zaten belirlendi. n1 ve n2 kesinlikle [Birim Fiyat, Net Tutar]'dır!
                // Örnek: n1 = 470 TL, n2 = 329 TL (Net Tutar)
                if (detectedDisc > 0)
                {
                    double exp1 = Math.Round((item.Quantity * n1) * (1.0 - (detectedDisc / 100.0)), 2);
                    if (Math.Abs(exp1 - n2) < 2.0)
                    {
                        item.UnitPrice = n1;
                        item.LineTotal = n2;
                    }
                    else
                    {
                        item.UnitPrice = Math.Max(n1, n2);
                        item.LineTotal = Math.Min(n1, n2);
                    }
                }
                else
                {
                    item.UnitPrice = n1;
                    item.LineTotal = n2;
                }
            }
            else
            {
                // Miktar belirlenmediyse: [Miktar, Birim Fiyat] veya [Birim Fiyat, Tutar]
                if (n1 <= 100 && n1 == Math.Floor(n1))
                {
                    item.Quantity = n1;
                    item.UnitPrice = n2;
                    item.LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
                }
                else
                {
                    item.Quantity = 1;
                    item.UnitPrice = Math.Min(n1, n2);
                    item.LineTotal = Math.Max(n1, n2);
                }
            }
        }
        else if (numbers.Count >= 3)
        {
            // 3 veya daha fazla sayı: [Miktar?, Birim Fiyat, Net Tutar]
            if (!hasExplicitQty && numbers[0] <= 100 && numbers[0] == Math.Floor(numbers[0]))
            {
                item.Quantity = numbers[0];
                item.UnitPrice = numbers[1];
                item.LineTotal = numbers[numbers.Count - 1];
            }
            else
            {
                item.UnitPrice = numbers[0];
                item.LineTotal = numbers[numbers.Count - 1];
            }
        }

        if (item.LineTotal == 0 && item.UnitPrice > 0 && item.Quantity > 0)
        {
            item.LineTotal = Math.Round(item.Quantity * item.UnitPrice, 2);
        }

        item.VatAmount = Math.Round(item.LineTotal * (item.VatPercent / 100.0), 2);
        return item;
    }

    private static bool TryParseFlexibleNumber(string str, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(str)) return false;

        str = str.Trim();
        // Türkçe format: 1.250,50 -> 1250.50
        if (str.Contains('.') && str.Contains(','))
        {
            str = str.Replace(".", "").Replace(",", ".");
        }
        else if (str.Contains(','))
        {
            str = str.Replace(',', '.');
        }

        return double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private void UpdateSummary()
    {
        int count = ExtractedItems.Count;
        double total = ExtractedItems.Sum(i => i.LineTotal);
        _lblSummary.Text = $"{count} Kalem Ayrıştırıldı | Toplam Tutar: {total:N2} ₺";
        BtnSave.Enabled = count > 0;
    }

    private void TransferItemsClick(object? sender, EventArgs e)
    {
        if (ExtractedItems.Count == 0)
        {
            MessageBox.Show("Aktarılacak geçerli ürün kalemi bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
