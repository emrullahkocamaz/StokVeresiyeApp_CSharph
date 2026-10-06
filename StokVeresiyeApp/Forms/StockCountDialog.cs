using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class StockCountDialog : Form
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _txtBarcode = new();
    private readonly NumericUpDown _nudCountQty = new() { Minimum = 1, Maximum = 99999, Value = 1, Width = 80 };
    private readonly Label _lblStats = new();
    private readonly DataTable _dtCount = new();

    public StockCountDialog()
    {
        Text = "📋 Depo & Raf Stok Sayımı / Düzeltme Fişi (Stok Eşitleme)";
        ClientSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        InitDataTable();
        BuildUI();
    }

    private void InitDataTable()
    {
        _dtCount.Columns.Add("ProductId", typeof(long));
        _dtCount.Columns.Add("Barkod", typeof(string));
        _dtCount.Columns.Add("Ürün Kodu", typeof(string));
        _dtCount.Columns.Add("Ürün Adı", typeof(string));
        _dtCount.Columns.Add("Birim", typeof(string));
        _dtCount.Columns.Add("Sistem Stoğu", typeof(double));
        _dtCount.Columns.Add("Sayılan Miktar", typeof(double));
        _dtCount.Columns.Add("Fark", typeof(double)); // Sayılan - Sistem
        _dtCount.Columns.Add("Durum", typeof(string)); // Fazla, Eksik, Tam
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new CardPanel { Dock = DockStyle.Top, Height = 75, Padding = new Padding(20, 12, 20, 12) };
        var lblTitle = new Label { Text = "📋 Fiziksel Stok Sayımı & Otomatik Düzeltme Fişi", Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = "Barkod okutarak veya ürün seçerek sayım yapabilir; sistem stoğu ile fiili stoğu tek tıkla eşitleyebilirsiniz.", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 22 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Barkod Okutma ve Hızlı Giriş Çubuğu
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtBarcode.Width = 240;
        _txtBarcode.Height = 32;
        _txtBarcode.PlaceholderText = "🔍 Barkod Okutun veya Enter...";
        _txtBarcode.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddProductByBarcode();
            }
        };

        var btnAddByList = UITheme.CreateButton("+ Ürün Listesinden Ekle", UITheme.Secondary, Color.White, (s, e) => OpenProductPicker(), 170, 34);
        var btnAddAllProducts = UITheme.CreateButton("📦 Tüm Ürünleri Sayım Listesine Çek", UITheme.PrimaryDark, Color.White, (s, e) => LoadAllActiveProducts(), 230, 34);
        var btnClear = UITheme.CreateButton("🧹 Temizle", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => ClearList(), 85, 34);

        _lblStats.AutoSize = true;
        _lblStats.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _lblStats.ForeColor = UITheme.PrimaryDark;
        _lblStats.TextAlign = ContentAlignment.MiddleRight;
        _lblStats.Dock = DockStyle.Right;

        var flowLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flowLeft.Controls.Add(new Label { Text = "Barkod / Kod:", AutoSize = true, Margin = new Padding(0, 7, 5, 0), Font = UITheme.TitleFont });
        flowLeft.Controls.Add(_txtBarcode);
        flowLeft.Controls.Add(new Label { Text = "Adet:", AutoSize = true, Margin = new Padding(8, 7, 5, 0), Font = UITheme.TitleFont });
        flowLeft.Controls.Add(_nudCountQty);
        flowLeft.Controls.Add(btnAddByList);
        flowLeft.Controls.Add(btnAddAllProducts);
        flowLeft.Controls.Add(btnClear);

        toolbar.Controls.Add(flowLeft);
        toolbar.Controls.Add(_lblStats);
        Controls.Add(toolbar);

        // 3. Grid
        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        UITheme.ApplyGridStyle(_grid);
        _grid.DataSource = _dtCount;
        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellValueChanged += (s, e) => RecalculateRow(e.RowIndex);
        gridContainer.Controls.Add(_grid);
        Controls.Add(gridContainer);

        // 4. Alt Buton Paneli
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 65, BackColor = UITheme.CardBg, Padding = new Padding(15, 12, 15, 12) };
        var btnApply = UITheme.CreateButton("💾 Sayım Sonucunu Stoğa Eşitle (Düzeltme Fişi Kes)", UITheme.Success, Color.White, ApplyStockCorrection, 340, 38);
        var btnExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Primary, Color.White, ExportExcelClick, 130, 38);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 38);

        btnApply.Dock = DockStyle.Left;
        btnExcel.Dock = DockStyle.Left;
        btnClose.Dock = DockStyle.Right;

        bottom.Controls.Add(btnApply);
        bottom.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 15 });
        bottom.Controls.Add(btnExcel);
        bottom.Controls.Add(btnClose);
        Controls.Add(bottom);

        // WinForms Z-Order
        gridContainer.BringToFront();
        header.SendToBack();
        toolbar.SendToBack();
        bottom.SendToBack();

        FormatGridColumns();
    }

    private void FormatGridColumns()
    {
        if (_grid.Columns["ProductId"] != null) _grid.Columns["ProductId"].Visible = false;
        if (_grid.Columns["Sistem Stoğu"] != null) _grid.Columns["Sistem Stoğu"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Sayılan Miktar"] != null) _grid.Columns["Sayılan Miktar"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Fark"] != null) _grid.Columns["Fark"].DefaultCellStyle.Format = "N2";

        // Sütun kilitleri (Sadece Sayılan Miktar düzenlenebilir)
        foreach (DataGridViewColumn col in _grid.Columns)
        {
            col.ReadOnly = col.Name != "Sayılan Miktar";
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        var row = _grid.Rows[e.RowIndex];

        double diff = Convert.ToDouble(row.Cells["Fark"].Value ?? 0);
        if (diff < 0)
        {
            // Eksik / Fire (Kırmızı)
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(185, 28, 28);
        }
        else if (diff > 0)
        {
            // Fazla (Açık Mavi/Yeşil)
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
        }
    }

    private void RecalculateRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dtCount.Rows.Count) return;
        var r = _dtCount.Rows[rowIndex];
        double sys = Convert.ToDouble(r["Sistem Stoğu"]);
        double count = Convert.ToDouble(r["Sayılan Miktar"]);
        double diff = count - sys;
        r["Fark"] = diff;
        r["Durum"] = diff == 0 ? "Eşit (Tam)" : (diff > 0 ? $"+{diff:N2} Fazla" : $"{diff:N2} Eksik/Fire");
        UpdateStats();
    }

    private void UpdateStats()
    {
        int totalItems = _dtCount.Rows.Count;
        int diffItems = 0;
        foreach (DataRow r in _dtCount.Rows)
        {
            double diff = Convert.ToDouble(r["Fark"]);
            if (diff != 0) diffItems++;
        }

        _lblStats.Text = $"Sayım Yapılan: {totalItems} Ürün | Farklı Olan: {diffItems} Ürün";
    }

    private void AddProductByBarcode()
    {
        string bc = _txtBarcode.Text.Trim();
        if (string.IsNullOrWhiteSpace(bc)) return;

        // Ürünü bul
        var dt = Database.Query(@"
SELECT 
    p.Id, p.Barcode, p.Code, p.Name, p.Unit,
    (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) AS CurrentStock
FROM Products p
WHERE p.IsActive = 1 AND (p.Barcode = $b OR p.Code = $b)", ("$b", bc));

        if (dt.Rows.Count == 0)
        {
            MessageBox.Show($"'{bc}' barkoduna veya koduna sahip aktif bir ürün bulunamadı.", "Ürün Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtBarcode.SelectAll();
            return;
        }

        var r = dt.Rows[0];
        long pid = Convert.ToInt64(r["Id"]);
        double addQty = (double)_nudCountQty.Value;

        // Listede var mı kontrol et
        var existing = _dtCount.AsEnumerable().FirstOrDefault(row => row.Field<long>("ProductId") == pid);
        if (existing != null)
        {
            existing["Sayılan Miktar"] = Convert.ToDouble(existing["Sayılan Miktar"]) + addQty;
            int idx = _dtCount.Rows.IndexOf(existing);
            RecalculateRow(idx);
        }
        else
        {
            double sysStock = Convert.ToDouble(r["CurrentStock"]);
            double countQty = addQty;
            double diff = countQty - sysStock;

            _dtCount.Rows.Add(
                pid,
                r["Barcode"]?.ToString() ?? "",
                r["Code"]?.ToString() ?? "",
                r["Name"]?.ToString() ?? "",
                r["Unit"]?.ToString() ?? "Adet",
                sysStock,
                countQty,
                diff,
                diff == 0 ? "Eşit (Tam)" : (diff > 0 ? $"+{diff:N2} Fazla" : $"{diff:N2} Eksik/Fire")
            );
        }

        UpdateStats();
        _txtBarcode.Clear();
        _txtBarcode.Focus();
    }

    private void OpenProductPicker()
    {
        // Tüm ürünleri basitçe listeye çek veya seçtir
        LoadAllActiveProducts();
    }

    private void LoadAllActiveProducts()
    {
        var dt = Database.Query(@"
SELECT 
    p.Id, p.Barcode, p.Code, p.Name, p.Unit,
    (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) AS CurrentStock
FROM Products p
WHERE p.IsActive = 1
ORDER BY p.Name ASC");

        _dtCount.Clear();
        foreach (DataRow r in dt.Rows)
        {
            long pid = Convert.ToInt64(r["Id"]);
            double sysStock = Convert.ToDouble(r["CurrentStock"]);

            _dtCount.Rows.Add(
                pid,
                r["Barcode"]?.ToString() ?? "",
                r["Code"]?.ToString() ?? "",
                r["Name"]?.ToString() ?? "",
                r["Unit"]?.ToString() ?? "Adet",
                sysStock,
                sysStock, // Varsayılan olarak sistem stoğu ile başlatılır, esnaf saydıkça düzeltir
                0,
                "Eşit (Tam)"
            );
        }

        UpdateStats();
        MessageBox.Show($"{dt.Rows.Count} adet aktif ürün sayım listesine çekildi.\n'Sayılan Miktar' sütununa çift tıklayarak fiili sayım değerlerini girebilirsiniz.", "Liste Yüklendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ClearList()
    {
        if (_dtCount.Rows.Count > 0 && MessageBox.Show("Sayım listesi temizlensin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _dtCount.Clear();
            UpdateStats();
        }
    }

    private void ApplyStockCorrection(object? sender, EventArgs e)
    {
        var diffRows = _dtCount.AsEnumerable().Where(r => Math.Abs(r.Field<double>("Fark")) > 0.0001).ToList();
        if (!diffRows.Any())
        {
            MessageBox.Show("Sistem stoğu ile sayım arasında herhangi bir fark bulunmamaktadır. Düzeltme işlemi gerekmiyor.", "Fark Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Toplam {diffRows.Count} adet üründe stok farkı tespit edildi!\n\nBu işlem, aradaki farkları kapatmak için otomatik stok düzeltme fişi kesecek ve sistemdeki tüm stokları fiili sayım miktarlarına eşitleyecektir.\n\nİşlemi onaylıyor musunuz?",
            "Stok Düzeltme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (confirm != DialogResult.Yes) return;

        try
        {
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string docNo = "SYM-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

            using var conn = Database.Open();
            using var tx = conn.BeginTransaction();

            foreach (var r in diffRows)
            {
                long pid = r.Field<long>("ProductId");
                string pName = r.Field<string>("Ürün Adı") ?? "";
                double diff = r.Field<double>("Fark");
                double absQty = Math.Abs(diff);
                string movType = diff > 0 ? "Gelen" : "Satılan";
                string note = diff > 0 ? $"Sayım Fazlası Düzeltme (+{absQty:N2})" : $"Sayım Eksiği / Fire Düzeltme (-{absQty:N2})";

                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, Note)
VALUES($d, $p, $t, $q, 0, $doc, $n);";
                cmd.Parameters.AddWithValue("$d", dateStr);
                cmd.Parameters.AddWithValue("$p", pid);
                cmd.Parameters.AddWithValue("$t", movType);
                cmd.Parameters.AddWithValue("$q", absQty);
                cmd.Parameters.AddWithValue("$doc", docNo);
                cmd.Parameters.AddWithValue("$n", note);
                cmd.ExecuteNonQuery();

                AuditLogService.Log("StokHareket", "Yeni Eklendi", pid, $"{pName} - {movType} ({absQty:N2})", null, note, "Fiziksel stok sayım düzeltmesi uygulandı");
            }

            tx.Commit();

            MessageBox.Show($"Tebrikler! {diffRows.Count} adet ürünün stoğu başarıyla güncellendi ve sıfır hatayla eşitlendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Düzeltme uygulanırken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportExcelClick(object? sender, EventArgs e)
    {
        if (_dtCount.Rows.Count == 0) return;

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Stok_Sayim_Raporu_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(_dtCount, "Sayım Raporu", sfd.FileName);
                MessageBox.Show("Sayım raporu Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
