using System.Data;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class ExpiryAndStockAlertsDialog : BaseModernForm
{
    private readonly KryptonComboBox _cmbFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtSearch = new() { CueHint = { CueHintText = "🔍 Ürün adı veya barkod ile filtrele..." } };
    private readonly DataGridView _grid = new();
    private readonly Label _lblStatExpiry = new();
    private readonly Label _lblStatCritical = new();
    private DataTable? _allDt;

    public ExpiryAndStockAlertsDialog() : base("⚠️ Son Kullanma Tarihi (SKT) & Kritik Stok Alarmları", 1080, 720)
    {
        _cmbFilter.Items.AddRange(new object[] {
            "Tüm Alarmlar (SKT + Kritik Stok)",
            "⏳ SKT 15 Gün Kalanlar (Acil)",
            "⏳ SKT 30 Gün Kalanlar",
            "❌ SKT Süresi Geçmiş Ürünler",
            "⚠️ Kritik Stok Seviyesinin Altındakiler",
            "⛔ Stoğu Bitenler (0 veya Negatif)"
        });
        _cmbFilter.SelectedIndex = 0;

        var pnlFilterBar = new Panel { Height = 42, Dock = DockStyle.Fill };
        _cmbFilter.Dock = DockStyle.Left;
        _cmbFilter.Width = 320;

        _txtSearch.Dock = DockStyle.Fill;
        _txtSearch.Margin = new Padding(10, 0, 0, 0);

        pnlFilterBar.Controls.Add(_txtSearch);
        pnlFilterBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12 });
        pnlFilterBar.Controls.Add(_cmbFilter);

        AddRow("Alarm Filtresi", pnlFilterBar, 46);

        // İstatistik Özet Çubuğu
        var pnlStats = new Panel { Height = 46, Dock = DockStyle.Fill };
        _lblStatExpiry.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblStatExpiry.ForeColor = Color.FromArgb(185, 28, 28);
        _lblStatExpiry.Dock = DockStyle.Left;
        _lblStatExpiry.Width = 460;
        _lblStatExpiry.TextAlign = ContentAlignment.MiddleLeft;

        _lblStatCritical.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblStatCritical.ForeColor = Color.FromArgb(180, 83, 9);
        _lblStatCritical.Dock = DockStyle.Fill;
        _lblStatCritical.TextAlign = ContentAlignment.MiddleLeft;

        pnlStats.Controls.Add(_lblStatCritical);
        pnlStats.Controls.Add(_lblStatExpiry);
        AddRow("Durum Özeti", pnlStats, 48);

        // Grid
        SetupGrid();
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Height = 420, Padding = new Padding(0, 6, 0, 0) };
        pnlGrid.Controls.Add(_grid);
        AddRow("Alarm Listesi", pnlGrid, 430);

        // Alt Butonlar
        BtnSave.Visible = false; // Salt okuma / alarm ekranı
        BtnCancel.Text = "Kapat";

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            var btnDiscount = UITheme.CreateKryptonButton("🏷️ Seçili Ürüne Fiyat Güncelle", Color.FromArgb(79, 70, 229), Color.White, (s, e) => OpenQuickPriceEdit(), 210, 36);
            btnDiscount.Dock = DockStyle.Left;
            actionPanel.Controls.Add(btnDiscount);
        }

        _cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        _txtSearch.TextChanged += (s, e) => ApplyFilter();

        LoadData();
    }

    private void SetupGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Font = new Font("Segoe UI", 9.5f);
        _grid.ColumnHeadersHeight = 36;
        _grid.RowTemplate.Height = 32;

        _grid.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            string status = row.Cells["Durum"]?.Value?.ToString() ?? "";

            if (status.Contains("SÜRESİ DOLDU") || status.Contains("STOK BİTTİ"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(185, 28, 28);
            }
            else if (status.Contains("15 GÜN") || status.Contains("KRİTİK"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(180, 83, 9);
            }
            else if (status.Contains("30 GÜN"))
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(29, 78, 216);
            }
        };
    }

    private void LoadData()
    {
        string sql = @"
SELECT 
    p.Id,
    COALESCE(p.Barcode, '') AS [Barkod],
    p.Name AS [Ürün Adı],
    p.Category AS [Kategori],
    (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) AS [Mevcut Stok],
    p.Unit AS [Birim],
    COALESCE(p.MinStockLevel, 5) AS [Kritik Seviye],
    p.SalePrice AS [Satış Fiyatı],
    COALESCE(p.ExpiryDate, '') AS [SKT Tarihi]
FROM Products p
WHERE p.IsActive = 1
ORDER BY p.Name";

        _allDt = Database.Query(sql);

        // Hesaplanan kolonlar ekle
        _allDt.Columns.Add("Kalan Gün", typeof(string));
        _allDt.Columns.Add("Durum", typeof(string));

        DateTime today = DateTime.Today;
        int expiredCount = 0;
        int days15Count = 0;
        int criticalCount = 0;

        foreach (DataRow r in _allDt.Rows)
        {
            double stock = Convert.ToDouble(r["Mevcut Stok"]);
            double minStock = Convert.ToDouble(r["Kritik Seviye"]);
            string expStr = r["SKT Tarihi"]?.ToString() ?? "";

            string status = "Normal";
            string daysLeftStr = "-";

            if (!string.IsNullOrWhiteSpace(expStr) && DateTime.TryParse(expStr, out var expDate))
            {
                int diffDays = (expDate.Date - today).Days;
                if (diffDays < 0)
                {
                    status = "❌ SÜRESİ DOLDU";
                    daysLeftStr = $"{Math.Abs(diffDays)} gün önce bitti";
                    expiredCount++;
                }
                else if (diffDays <= 15)
                {
                    status = "⏳ 15 GÜN KALDI (ACİL)";
                    daysLeftStr = $"{diffDays} gün kaldı";
                    days15Count++;
                }
                else if (diffDays <= 30)
                {
                    status = "⏳ 30 GÜN KALDI";
                    daysLeftStr = $"{diffDays} gün kaldı";
                }
                else
                {
                    daysLeftStr = $"{diffDays} gün";
                }
            }

            if (stock <= 0)
            {
                status = string.IsNullOrWhiteSpace(expStr) ? "⛔ STOK BİTTİ" : status + " & STOK BİTTİ";
                criticalCount++;
            }
            else if (stock <= minStock)
            {
                status = string.IsNullOrWhiteSpace(expStr) ? "⚠️ KRİTİK STOK" : status + " & KRİTİK";
                criticalCount++;
            }

            r["Kalan Gün"] = daysLeftStr;
            r["Durum"] = status;
        }

        _lblStatExpiry.Text = $"⏳ SKT Alarmı: {expiredCount} Günü Geçmiş | {days15Count} Adet 15 Gün İçi";
        _lblStatCritical.Text = $"⚠️ Kritik/Tükenen Stok: {criticalCount} Adet Ürün";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_allDt == null) return;

        string search = _txtSearch.Text.Trim().Replace("'", "''");
        int filterIdx = _cmbFilter.SelectedIndex;

        string filterExpr = "";

        switch (filterIdx)
        {
            case 0: // Tümü (Alarm olanlar)
                filterExpr = "Durum <> 'Normal'";
                break;
            case 1: // 15 gün
                filterExpr = "Durum LIKE '%15 GÜN%'";
                break;
            case 2: // 30 gün
                filterExpr = "Durum LIKE '%30 GÜN%'";
                break;
            case 3: // Süresi dolan
                filterExpr = "Durum LIKE '%SÜRESİ DOLDU%'";
                break;
            case 4: // Kritik stok
                filterExpr = "Durum LIKE '%KRİTİK%'";
                break;
            case 5: // Stok bitti
                filterExpr = "Durum LIKE '%STOK BİTTİ%'";
                break;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string sCond = $"([Ürün Adı] LIKE '%{search}%' OR [Barkod] LIKE '%{search}%')";
            filterExpr = string.IsNullOrWhiteSpace(filterExpr) ? sCond : $"({filterExpr}) AND {sCond}";
        }

        _allDt.DefaultView.RowFilter = filterExpr;
        _grid.DataSource = _allDt.DefaultView;

        if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
        if (_grid.Columns["Satış Fiyatı"] != null) _grid.Columns["Satış Fiyatı"].DefaultCellStyle.Format = "N2 ₺";
        if (_grid.Columns["Mevcut Stok"] != null) _grid.Columns["Mevcut Stok"].DefaultCellStyle.Format = "N2";
    }

    private void OpenQuickPriceEdit()
    {
        if (_grid.CurrentRow?.DataBoundItem is DataRowView rv)
        {
            long pId = Convert.ToInt64(rv["Id"]);
            string pName = rv["Ürün Adı"]?.ToString() ?? "";
            double curPrice = Convert.ToDouble(rv["Satış Fiyatı"]);

            string res = PromptDialog.Show($"'{pName}' ürünü için yeni satış fiyatını giriniz:", "Hızlı Fiyat Güncelle", curPrice.ToString("N2"));
            if (!string.IsNullOrWhiteSpace(res) && double.TryParse(res.Replace('.', ','), out double newPrice) && newPrice > 0)
            {
                Database.Execute("UPDATE Products SET SalePrice=@p WHERE Id=@id", ("@p", newPrice), ("@id", pId));
                MessageBox.Show("Ürün fiyatı güncellendi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
        }
        else
        {
            MessageBox.Show("Lütfen tablodan bir ürün seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
