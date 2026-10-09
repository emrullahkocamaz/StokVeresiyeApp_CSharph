using System.Data;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class DeadStockReportDialog : BaseModernForm
{
    private readonly KryptonComboBox _cmbDays = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _grid = new();
    private readonly Label _lblCardCount = new();
    private readonly Label _lblCardCost = new();
    private readonly Label _lblCardPotential = new();

    public DeadStockReportDialog() : base("📦 Ölü Stok (Dead Stock) Analiz Raporu", 1080, 720)
    {
        _cmbDays.Items.AddRange(new object[] {
            "Son 30 Gündür Satılmayanlar",
            "Son 60 Gündür Satılmayanlar (Ölü Stok Başlangıcı)",
            "Son 90 Gündür Satılmayanlar (Kritik Durgun Stok)",
            "Son 180 Gündür (6 Ay) Satılmayanlar",
            "Son 365 Gündür (1 Yıl) Satılmayanlar"
        });
        _cmbDays.SelectedIndex = 2; // Varsayılan 90 gün

        AddRow("Analiz Edilecek Durgunluk Süresi", _cmbDays, 40);

        // 3 Büyük Gösterge Kartı Paneli
        var pnlCards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Height = 70,
            Padding = new Padding(0)
        };
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        pnlCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        var card1 = CreateStatCard("Hareketsiz Ürün Sayısı", _lblCardCount, Color.FromArgb(79, 70, 229));
        var card2 = CreateStatCard("Bağlanan Sermaye (Alış Tutarı)", _lblCardCost, Color.FromArgb(220, 38, 38));
        var card3 = CreateStatCard("Bekleyen Ciro (Satış Tutarı)", _lblCardPotential, Color.FromArgb(16, 185, 129));

        pnlCards.Controls.Add(card1, 0, 0);
        pnlCards.Controls.Add(card2, 1, 0);
        pnlCards.Controls.Add(card3, 2, 0);

        AddRow("Sermaye Etkisi", pnlCards, 75);

        // Öneri Paneli
        var lblTip = new Label
        {
            Text = "💡 Akıllı Tavsiye: Bu ürünler rafta maliyet oluşturmaktadır. 'Toplu Fiyat Sihirbazı' ile bu ürünlere %15-25 indirim tanımlayabilir veya kampanyalı satış yaparak bağlı sermayenizi hızlıca nakde çevirebilirsiniz.",
            ForeColor = Color.FromArgb(30, 41, 59),
            BackColor = Color.FromArgb(241, 245, 249),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0)
        };
        AddRow("Finansal Öneri", lblTip, 45);

        // Grid
        SetupGrid();
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Height = 360, Padding = new Padding(0, 6, 0, 0) };
        pnlGrid.Controls.Add(_grid);
        AddRow("Durgun Stok Listesi", pnlGrid, 370);

        BtnSave.Visible = false;
        BtnCancel.Text = "Kapat";

        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            var btnGoBulk = UITheme.CreateKryptonButton("🏷️ Toplu İndirim Sihirbazına Git", Color.FromArgb(124, 58, 237), Color.White, (s, e) =>
            {
                using var dlg = new BulkPriceUpdateDialog();
                dlg.ShowDialog(this);
                CalculateReport();
            }, 230, 36);
            btnGoBulk.Dock = DockStyle.Left;
            actionPanel.Controls.Add(btnGoBulk);
        }

        _cmbDays.SelectedIndexChanged += (s, e) => CalculateReport();
        CalculateReport();
    }

    private Control CreateStatCard(string title, Label lblValue, Color accentColor)
    {
        var pnl = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(4),
            Padding = new Padding(10, 8, 10, 8)
        };

        var lblTitle = new Label
        {
            Text = title,
            ForeColor = UITheme.TextMuted,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            Dock = DockStyle.Top,
            Height = 18
        };

        lblValue.Text = "0";
        lblValue.ForeColor = accentColor;
        lblValue.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        lblValue.Dock = DockStyle.Fill;
        lblValue.TextAlign = ContentAlignment.MiddleLeft;

        pnl.Controls.Add(lblValue);
        pnl.Controls.Add(lblTitle);
        return pnl;
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
        _grid.RowTemplate.Height = 30;
    }

    private void CalculateReport()
    {
        int days = _cmbDays.SelectedIndex switch
        {
            0 => 30,
            1 => 60,
            2 => 90,
            3 => 180,
            _ => 365
        };

        string cutoffDate = DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd");

        // Sorgu: Rafta mevcut stoğu > 0 olan ve cutoffDate tarihinden sonra hiç 'Giden' / 'Satış' hareketi olmayan ürünler
        string sql = $@"
SELECT 
    p.Id,
    COALESCE(p.Barcode, '') AS [Barkod],
    p.Name AS [Ürün Adı],
    p.Category AS [Kategori],
    (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) AS [Mevcut Stok],
    p.Unit AS [Birim],
    p.PurchasePrice AS [Alış Fiyatı],
    p.SalePrice AS [Satış Fiyatı],
    COALESCE((SELECT MAX(MovementDate) FROM StockMovements WHERE ProductId=p.Id AND MovementType IN ('Giden','Satış')), 'Hiç Satılmadı') AS [Son Satış Tarihi]
FROM Products p
WHERE p.IsActive = 1
  AND (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) > 0
  AND NOT EXISTS (
      SELECT 1 FROM StockMovements sm2 
      WHERE sm2.ProductId = p.Id 
        AND sm2.MovementType IN ('Giden', 'Satış') 
        AND sm2.MovementDate >= '{cutoffDate}'
  )
ORDER BY (p.PurchasePrice * (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0))) DESC";

        DataTable dt = Database.Query(sql);
        dt.Columns.Add("Bağlı Sermaye (₺)", typeof(double));
        dt.Columns.Add("Bekleyen Ciro (₺)", typeof(double));

        int totalCount = dt.Rows.Count;
        double totalCost = 0;
        double totalPotential = 0;

        foreach (DataRow r in dt.Rows)
        {
            double stock = Convert.ToDouble(r["Mevcut Stok"]);
            double purchase = Convert.ToDouble(r["Alış Fiyatı"]);
            double sale = Convert.ToDouble(r["Satış Fiyatı"]);

            double cost = stock * purchase;
            double potential = stock * sale;

            r["Bağlı Sermaye (₺)"] = Math.Round(cost, 2);
            r["Bekleyen Ciro (₺)"] = Math.Round(potential, 2);

            totalCost += cost;
            totalPotential += potential;
        }

        _lblCardCount.Text = $"{totalCount} Adet Ürün";
        _lblCardCost.Text = $"{totalCost:N2} ₺";
        _lblCardPotential.Text = $"{totalPotential:N2} ₺";

        _grid.DataSource = dt;

        if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
        if (_grid.Columns["Alış Fiyatı"] != null) _grid.Columns["Alış Fiyatı"].DefaultCellStyle.Format = "N2 ₺";
        if (_grid.Columns["Satış Fiyatı"] != null) _grid.Columns["Satış Fiyatı"].DefaultCellStyle.Format = "N2 ₺";
        if (_grid.Columns["Bağlı Sermaye (₺)"] != null) _grid.Columns["Bağlı Sermaye (₺)"].DefaultCellStyle.Format = "N2 ₺";
        if (_grid.Columns["Bekleyen Ciro (₺)"] != null) _grid.Columns["Bekleyen Ciro (₺)"].DefaultCellStyle.Format = "N2 ₺";
        if (_grid.Columns["Mevcut Stok"] != null) _grid.Columns["Mevcut Stok"].DefaultCellStyle.Format = "N2";
    }
}
