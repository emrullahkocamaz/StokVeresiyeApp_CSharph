using System.Data;
using ClosedXML.Excel;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ExcelImportDialog : Form
{
    private readonly string _importType; // "Urun" veya "Cari"
    private readonly DataGridView _gridPreview = new();
    private readonly Label _lblStatus = new();
    private readonly ProgressBar _progressBar = new() { Visible = false, Height = 20, Dock = DockStyle.Bottom };
    private readonly CheckBox _chkUpdateExisting = new() { Text = "Mevcut kayıtlar varsa bilgilerini güncelle", Checked = true, AutoSize = true, Font = UITheme.RegularFont };
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private DataTable? _parsedTable;
    private string? _selectedFilePath;

    public ExcelImportDialog(string importType = "Urun")
    {
        _importType = importType;
        Text = _importType == "Urun" ? "📦 Excel'den Toplu Ürün İçe Aktarma Sihirbazı" : "👥 Excel'den Toplu Cari İçe Aktarma Sihirbazı";
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
    }

    private void BuildUI()
    {
        // 1. Başlık
        var header = new CardPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(20, 12, 20, 12) };
        string title = _importType == "Urun" ? "📦 Excel Dosyasından Toplu Ürün Aktarımı" : "👥 Excel Dosyasından Toplu Cari Aktarımı";
        string sub = _importType == "Urun" 
            ? "Toptancınızdan veya başka programdan aldığınız Excel listesini tek tıkla sisteme aktarabilir, fiyat ve stokları güncelleyebilirsiniz." 
            : "Müşteri ve tedarikçi listenizi Excel formatında sisteme toplu olarak aktarabilirsiniz.";

        var lblTitle = new Label { Text = title, Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = sub, Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Toolbar & Seçim Butonları
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(12, 14, 12, 14) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        var btnTemplate = UITheme.CreateButton("📥 Örnek Şablon Excel İndir", Color.FromArgb(79, 70, 229), Color.White, (s, e) => DownloadTemplate(), 200, 36);
        var btnSelectFile = UITheme.CreateButton("📂 Excel Dosyası Seç (.xlsx)", UITheme.Primary, Color.White, (s, e) => SelectFileAndPreview(), 200, 36);
        var btnImport = UITheme.CreateButton("🚀 Verileri İçe Aktar", UITheme.Success, Color.White, (s, e) => ExecuteImport(), 180, 36);

        flow.Controls.Add(btnTemplate);
        flow.Controls.Add(btnSelectFile);
        flow.Controls.Add(_chkUpdateExisting);

        if (_importType == "Urun")
        {
            var lblWh = new Label { Text = "Aktarılacak Depo:", AutoSize = true, Margin = new Padding(10, 8, 0, 0) };
            var warehouses = WarehouseService.GetActiveWarehouses();
            _cmbWarehouse.DataSource = warehouses;
            _cmbWarehouse.DisplayMember = "Name";
            _cmbWarehouse.ValueMember = "Id";
            flow.Controls.Add(lblWh);
            flow.Controls.Add(_cmbWarehouse);
        }

        flow.Controls.Add(btnImport);
        toolbar.Controls.Add(flow);
        Controls.Add(toolbar);

        // 3. Durum Çubuğu
        var pnlStatus = new CardPanel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(15, 10, 15, 10) };
        _lblStatus.Text = "Lütfen aktarmak istediğiniz Excel dosyasını seçiniz veya örnek şablonu indiriniz.";
        _lblStatus.ForeColor = UITheme.TextSecondary;
        _lblStatus.Dock = DockStyle.Fill;
        pnlStatus.Controls.Add(_lblStatus);
        Controls.Add(pnlStatus);
        Controls.Add(_progressBar);

        // 4. Önizleme Grid
        var gridCard = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(15, 10, 15, 10) };
        UITheme.ApplyGridStyle(_gridPreview);
        gridCard.Controls.Add(_gridPreview);
        Controls.Add(gridCard);
    }

    private void DownloadTemplate()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = _importType == "Urun" ? "Ornek_Urun_Listesi.xlsx" : "Ornek_Cari_Listesi.xlsx",
            Title = "Örnek Excel Şablonunu Kaydedin"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            using var workbook = new XLWorkbook();
            if (_importType == "Urun")
            {
                var ws = workbook.Worksheets.Add("Urunler");
                string[] headers = { "Ürün Kodu", "Barkod", "Ürün Adı", "Kategori", "Birim", "Alış Fiyatı", "Satış Fiyatı", "Toptan Fiyat", "Bayi Fiyatı", "Mevcut Stok", "Kritik Seviye", "KDV %" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }

                // Örnek 2 satır veri
                object[,] samples = {
                    { "URN-001", "869000000001", "Örnek Ürün A", "Gıda", "Adet", 50.0, 75.0, 65.0, 60.0, 100, 10, 20 },
                    { "URN-002", "869000000002", "Örnek Ürün B", "Temizlik", "Koli", 120.0, 180.0, 160.0, 150.0, 25, 5, 20 }
                };

                for (int r = 0; r < 2; r++)
                {
                    for (int c = 0; c < headers.Length; c++)
                    {
                        ws.Cell(r + 2, c + 1).Value = samples[r, c]?.ToString() ?? "";
                    }
                }
                ws.Columns().AdjustToContents();
            }
            else
            {
                var ws = workbook.Worksheets.Add("Cariler");
                string[] headers = { "Cari Adı", "Türü (Müşteri/Tedarikçi)", "Telefon", "E-Posta", "Adres", "Vergi Dairesi", "Vergi No", "Fiyat Grubu", "Sabit İskonto %", "Risk Limiti", "Açılış Bakiyesi" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(79, 70, 229);
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }

                object[,] samples = {
                    { "Ahmet Yılmaz Ticaret", "Müşteri", "05551234567", "ahmet@mail.com", "Kadıköy / İstanbul", "Kadıköy", "1234567890", "Perakende", 0, 50000, 1500 },
                    { "Örnek Toptan Gıda A.Ş.", "Tedarikçi", "02129876543", "info@toptan.com", "İstoç / İstanbul", "İkitelli", "9876543210", "Toptan", 5, 100000, 0 }
                };

                for (int r = 0; r < 2; r++)
                {
                    for (int c = 0; c < headers.Length; c++)
                    {
                        ws.Cell(r + 2, c + 1).Value = samples[r, c]?.ToString() ?? "";
                    }
                }
                ws.Columns().AdjustToContents();
            }

            workbook.SaveAs(sfd.FileName);
            MessageBox.Show($"Örnek şablon başarıyla kaydedildi:\n{sfd.FileName}", "Şablon Hazır", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Şablon oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectFileAndPreview()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Excel Dosyaları (*.xlsx;*.xls)|*.xlsx;*.xls",
            Title = "İçe Aktarılacak Excel Dosyasını Seçin"
        };

        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            _selectedFilePath = ofd.FileName;
            using var workbook = new XLWorkbook(_selectedFilePath);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                MessageBox.Show("Seçilen Excel dosyasında sayfa bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var range = ws.RangeUsed();
            if (range == null)
            {
                MessageBox.Show("Excel sayfası boş.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int rowCount = range.RowCount();
            int colCount = range.ColumnCount();
            if (rowCount < 2)
            {
                MessageBox.Show("Excel dosyasında başlık dışında veri bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _parsedTable = new DataTable();
            for (int col = 1; col <= colCount; col++)
            {
                string headerName = ws.Cell(1, col).GetString().Trim();
                if (string.IsNullOrWhiteSpace(headerName)) headerName = $"Kolon_{col}";
                _parsedTable.Columns.Add(headerName, typeof(string));
            }

            for (int row = 2; row <= rowCount; row++)
            {
                var dr = _parsedTable.NewRow();
                bool hasData = false;
                for (int col = 1; col <= colCount; col++)
                {
                    string val = ws.Cell(row, col).GetString().Trim();
                    dr[col - 1] = val;
                    if (!string.IsNullOrWhiteSpace(val)) hasData = true;
                }
                if (hasData) _parsedTable.Rows.Add(dr);
            }

            _gridPreview.DataSource = _parsedTable;
            _lblStatus.Text = $"✅ Dosya başarıyla okundu: {Path.GetFileName(_selectedFilePath)} | Toplam {_parsedTable.Rows.Count} satır veri tespit edildi.";
            _lblStatus.ForeColor = Color.FromArgb(22, 101, 52);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dosya okunurken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExecuteImport()
    {
        if (_parsedTable == null || _parsedTable.Rows.Count == 0)
        {
            MessageBox.Show("Lütfen önce verisi olan geçerli bir Excel dosyası seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_importType == "Urun")
        {
            ImportProducts();
        }
        else
        {
            ImportAccounts();
        }
    }

    private void ImportProducts()
    {
        int added = 0;
        int updated = 0;
        int errors = 0;
        long warehouseId = _cmbWarehouse.SelectedValue != null ? Convert.ToInt64(_cmbWarehouse.SelectedValue) : 1;

        _progressBar.Visible = true;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = _parsedTable!.Rows.Count;
        _progressBar.Value = 0;

        foreach (DataRow row in _parsedTable.Rows)
        {
            try
            {
                string name = GetVal(row, "ad", "ürün adı", "isim", "urun adi");
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors++;
                    continue;
                }

                string code = GetVal(row, "kod", "ürün kodu", "stok kodu", "urun kodu");
                if (string.IsNullOrWhiteSpace(code))
                {
                    code = "URN-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
                }

                string barcode = GetVal(row, "barkod", "barcode");
                string category = GetVal(row, "kategori", "grup") ?? "Genel";
                string unit = GetVal(row, "birim", "ölçü") ?? "Adet";

                double purchasePrice = ParseDouble(GetVal(row, "alış", "alis", "maliyet"));
                double salePrice = ParseDouble(GetVal(row, "satış", "satis", "perakende"));
                double wholesalePrice = ParseDouble(GetVal(row, "toptan", "toptan fiyat"));
                double specialPrice = ParseDouble(GetVal(row, "bayi", "özel", "ozel"));
                double stock = ParseDouble(GetVal(row, "stok", "miktar", "adet", "mevcut"));
                double minStock = ParseDouble(GetVal(row, "kritik", "asgari", "min")) is double m && m > 0 ? m : 5;
                double vat = ParseDouble(GetVal(row, "kdv", "vergi")) is double v && v > 0 ? v : 20;

                // Ürün zaten var mı?
                var existing = Database.Query("SELECT Id FROM Products WHERE Code=$c OR (Barcode != '' AND Barcode=$b)", ("$c", code), ("$b", barcode));

                if (existing.Rows.Count > 0)
                {
                    if (_chkUpdateExisting.Checked)
                    {
                        long pId = Convert.ToInt64(existing.Rows[0]["Id"]);
                        Database.Execute(@"
UPDATE Products 
SET Name=$n, Barcode=$b, Category=$cat, Unit=$u, PurchasePrice=$pp, SalePrice=$sp, 
    WholesalePrice=$wp, SpecialPrice=$spp, MinStockLevel=$msl, VatPercent=$vp
WHERE Id=$id",
                            ("$id", pId),
                            ("$n", name),
                            ("$b", barcode),
                            ("$cat", category),
                            ("$u", unit),
                            ("$pp", purchasePrice),
                            ("$sp", salePrice),
                            ("$wp", wholesalePrice),
                            ("$spp", specialPrice),
                            ("$msl", minStock),
                            ("$vp", vat));

                        updated++;
                    }
                }
                else
                {
                    Database.Execute(@"
INSERT INTO Products (Code, Barcode, Name, Category, Unit, OpeningStock, PurchasePrice, SalePrice, WholesalePrice, SpecialPrice, MinStockLevel, VatPercent, IsActive)
VALUES ($c, $b, $n, $cat, $u, $os, $pp, $sp, $wp, $spp, $msl, $vp, 1);
DECLARE @newId BIGINT = SCOPE_IDENTITY();
IF $os > 0
BEGIN
    INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, WarehouseId, Note)
    VALUES (CONVERT(VARCHAR(10), GETDATE(), 120), @newId, 'Gelen', $os, $pp, 'EXCEL-AKTARIM', $wId, 'Excel ile ilk stok girişi');
END;",
                        ("$c", code),
                        ("$b", barcode),
                        ("$n", name),
                        ("$cat", category),
                        ("$u", unit),
                        ("$os", stock),
                        ("$pp", purchasePrice),
                        ("$sp", salePrice),
                        ("$wp", wholesalePrice),
                        ("$spp", specialPrice),
                        ("$msl", minStock),
                        ("$vp", vat),
                        ("$wId", warehouseId));

                    added++;
                }
            }
            catch
            {
                errors++;
            }
            _progressBar.Value++;
        }

        _progressBar.Visible = false;
        AuditLogService.Log("Urun", "Excel Toplu İçe Aktarım", null, "Toplu Ürün Aktarımı", $"Eklenen: {added}, Güncellenen: {updated}, Hatalı: {errors}");
        MessageBox.Show($"Excel aktarımı tamamlandı!\n\n✅ Yeni Eklenen Ürün: {added}\n🔄 Güncellenen Ürün: {updated}\n⚠️ Atlanan/Hatalı: {errors}", "Aktarım Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ImportAccounts()
    {
        int added = 0;
        int updated = 0;
        int errors = 0;

        _progressBar.Visible = true;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = _parsedTable!.Rows.Count;
        _progressBar.Value = 0;

        foreach (DataRow row in _parsedTable.Rows)
        {
            try
            {
                string name = GetVal(row, "ad", "unvan", "ünvan", "cari adı", "firma");
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors++;
                    continue;
                }

                string typeRaw = GetVal(row, "tür", "tip", "türü", "cari türü") ?? "Müşteri";
                string type = typeRaw.IndexOf("tedarik", StringComparison.OrdinalIgnoreCase) >= 0 ? "Tedarikçi" : "Müşteri";
                string phone = GetVal(row, "tel", "telefon", "gsm", "cep") ?? "";
                string email = GetVal(row, "posta", "mail", "eposta") ?? "";
                string address = GetVal(row, "adres", "sehir", "il") ?? "";
                string taxOffice = GetVal(row, "vergi dairesi", "vd") ?? "";
                string taxNumber = GetVal(row, "vergi no", "tc", "vno") ?? "";
                string priceGroup = GetVal(row, "fiyat grubu", "tarife") ?? "Perakende";
                double discount = ParseDouble(GetVal(row, "iskonto", "indirim"));
                double limit = ParseDouble(GetVal(row, "limit", "risk"));
                double openingBalance = ParseDouble(GetVal(row, "bakiye", "borç", "alacak"));

                var existing = Database.Query("SELECT Id FROM Accounts WHERE Name=$n", ("$n", name));
                if (existing.Rows.Count > 0)
                {
                    if (_chkUpdateExisting.Checked)
                    {
                        long aId = Convert.ToInt64(existing.Rows[0]["Id"]);
                        Database.Execute(@"
UPDATE Accounts 
SET Type=$t, Phone=$p, Email=$e, Address=$a, TaxOffice=$to, TaxNumber=$tn, 
    PriceGroup=$pg, DefaultDiscountPercent=$dp, BalanceLimit=$l
WHERE Id=$id",
                            ("$id", aId),
                            ("$t", type),
                            ("$p", phone),
                            ("$e", email),
                            ("$a", address),
                            ("$to", taxOffice),
                            ("$tn", taxNumber),
                            ("$pg", priceGroup),
                            ("$dp", discount),
                            ("$l", limit));
                        updated++;
                    }
                }
                else
                {
                    Database.Execute(@"
INSERT INTO Accounts (Name, Type, Phone, Email, Address, TaxOffice, TaxNumber, PriceGroup, DefaultDiscountPercent, BalanceLimit, IsActive)
VALUES ($n, $t, $p, $e, $a, $to, $tn, $pg, $dp, $l, 1);
DECLARE @newAccId BIGINT = SCOPE_IDENTITY();
IF $bal > 0
BEGIN
    INSERT INTO AccountMovements (MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
    VALUES (CONVERT(VARCHAR(10), GETDATE(), 120), @newAccId, CASE WHEN $t='Müşteri' THEN 'Satış' ELSE 'Alış' END, 'EXCEL-ACILIS', $bal, 'Açılış Bakiyesi', 'Merkez Kasa', 'Excel ile devir bakiye');
END;",
                        ("$n", name),
                        ("$t", type),
                        ("$p", phone),
                        ("$e", email),
                        ("$a", address),
                        ("$to", taxOffice),
                        ("$tn", taxNumber),
                        ("$pg", priceGroup),
                        ("$dp", discount),
                        ("$l", limit),
                        ("$bal", openingBalance));

                    added++;
                }
            }
            catch
            {
                errors++;
            }
            _progressBar.Value++;
        }

        _progressBar.Visible = false;
        AuditLogService.Log("Cari", "Excel Toplu İçe Aktarım", null, "Toplu Cari Aktarımı", $"Eklenen: {added}, Güncellenen: {updated}, Hatalı: {errors}");
        MessageBox.Show($"Cari aktarımı tamamlandı!\n\n✅ Yeni Eklenen Cari: {added}\n🔄 Güncellenen Cari: {updated}\n⚠️ Atlanan/Hatalı: {errors}", "Aktarım Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private string? GetVal(DataRow row, params string[] possibleKeywords)
    {
        foreach (DataColumn col in row.Table.Columns)
        {
            string colName = col.ColumnName.ToLowerInvariant();
            foreach (var kw in possibleKeywords)
            {
                if (colName.Contains(kw.ToLowerInvariant()))
                {
                    string v = row[col]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
        }
        return null;
    }

    private double ParseDouble(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        val = val.Replace("₺", "").Replace("$", "").Replace("€", "").Trim();
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double d)) return d;
        if (double.TryParse(val.Replace(".", ","), System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("tr-TR"), out double dTr)) return dTr;
        if (double.TryParse(val.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dInv)) return dInv;
        return 0;
    }
}
