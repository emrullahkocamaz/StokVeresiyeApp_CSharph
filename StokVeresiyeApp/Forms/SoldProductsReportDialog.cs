using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class SoldProductsReportDialog : Form
{
    private readonly DateTimePicker _dtpStart = new() { Format = DateTimePickerFormat.Short, Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1) };
    private readonly DateTimePicker _dtpEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private readonly DataGridView _grid = new();
    private readonly Label _lblProductCount = new();
    private readonly Label _lblQuantity = new();
    private readonly Label _lblSalesTotal = new();

    public SoldProductsReportDialog()
    {
        Text = "Satılan Ürünler Raporu";
        ClientSize = new Size(1200, 760);
        MinimumSize = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(16, 12, 16, 8), BackColor = UITheme.CardBg };
        var lblStart = new Label { Text = "Başlangıç:", AutoSize = true, Left = 16, Top = 17, Font = UITheme.BoldFont };
        _dtpStart.SetBounds(92, 12, 130, 30);
        var lblEnd = new Label { Text = "Bitiş:", AutoSize = true, Left = 242, Top = 17, Font = UITheme.BoldFont };
        _dtpEnd.SetBounds(286, 12, 130, 30);
        var btnRefresh = UITheme.CreateButton("Listele", UITheme.Primary, Color.White, (s, e) => LoadReport(), 110, 34);
        btnRefresh.SetBounds(436, 10, 110, 34);
        toolbar.Controls.AddRange(new Control[] { lblStart, _dtpStart, lblEnd, _dtpEnd, btnRefresh });

        UITheme.ApplyGridStyle(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = true;

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            ColumnCount = 3,
            BackColor = UITheme.CardBg,
            Padding = new Padding(12, 8, 12, 8)
        };
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        summary.Controls.Add(CreateSummaryItem("Satılan Ürün Çeşidi", _lblProductCount), 0, 0);
        summary.Controls.Add(CreateSummaryItem("Toplam Satış Adedi", _lblQuantity), 1, 0);
        summary.Controls.Add(CreateSummaryItem("Toplam Satış Tutarı", _lblSalesTotal), 2, 0);

        Controls.Add(_grid);
        Controls.Add(summary);
        Controls.Add(toolbar);
        Controls.SetChildIndex(toolbar, 0);
        Controls.SetChildIndex(summary, 1);
        Controls.SetChildIndex(_grid, 2);

        LoadReport();
    }

    private static Control CreateSummaryItem(string title, Label valueLabel)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 2, 8, 2) };
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = UITheme.TextMuted,
            Font = UITheme.SmallFont
        };
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Text = "0";
        valueLabel.ForeColor = UITheme.TextPrimary;
        valueLabel.Font = UITheme.TitleFont;
        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private void LoadReport()
    {
        if (_dtpStart.Value.Date > _dtpEnd.Value.Date)
        {
            MessageBox.Show("Başlangıç tarihi bitiş tarihinden sonra olamaz.", "Tarih Aralığı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        const string sql = @"
WITH SalesByProduct AS
(
    SELECT
        ProductId,
        SUM(Quantity) AS SoldQuantity,
        SUM(Quantity * UnitPrice) AS SalesAmount
    FROM StockMovements
    WHERE MovementType IN ('Satılan', 'Satış', 'Giden')
      AND MovementDate >= @startDate
      AND MovementDate < DATEADD(day, 1, @endDate)
    GROUP BY ProductId
)
SELECT
    p.Id,
    p.Code AS [Ürün Kodu],
    p.Name AS [Ürün Adı],
    p.Unit AS [Birim],
    s.SoldQuantity AS [Satılan Adet],
    s.SalesAmount / NULLIF(s.SoldQuantity, 0) AS [Ortalama Birim Fiyat (₺)],
    s.SalesAmount AS [Satış Tutarı (₺)],
    COALESCE(p.OpeningStock, 0) + COALESCE((
        SELECT SUM(CASE
            WHEN sm.MovementType IN ('Gelen', 'İade Giriş', 'Transfer Giriş') THEN sm.Quantity
            WHEN sm.MovementType IN ('Satılan', 'Satış', 'Giden', 'Çıkış', 'Fire', 'Fire / Zayi', 'Transfer Çıkış') THEN -sm.Quantity
            ELSE 0
        END)
        FROM StockMovements sm
        WHERE sm.ProductId = p.Id
    ), 0) AS [Kalan Stok]
FROM SalesByProduct s
JOIN Products p ON p.Id = s.ProductId
ORDER BY s.SalesAmount DESC, p.Name;";

        try
        {
            DataTable table = Database.Query(
                sql,
                ("@startDate", _dtpStart.Value.Date.ToString("yyyy-MM-dd")),
                ("@endDate", _dtpEnd.Value.Date.ToString("yyyy-MM-dd")));
            _grid.DataSource = table;
            if (_grid.Columns["Id"] is { } idColumn) idColumn.Visible = false;
            foreach (string columnName in new[] { "Ortalama Birim Fiyat (₺)", "Satış Tutarı (₺)" })
            {
                if (_grid.Columns[columnName] is { } column)
                {
                    column.DefaultCellStyle.Format = "N2";
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            if (_grid.Columns["Satılan Adet"] is { } quantityColumn)
            {
                quantityColumn.DefaultCellStyle.Format = "N2";
                quantityColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_grid.Columns["Kalan Stok"] is { } stockColumn)
            {
                stockColumn.DefaultCellStyle.Format = "N2";
                stockColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            double totalQuantity = table.Rows.Cast<DataRow>().Sum(row => Convert.ToDouble(row["Satılan Adet"]));
            double totalAmount = table.Rows.Cast<DataRow>().Sum(row => Convert.ToDouble(row["Satış Tutarı (₺)"]));
            _lblProductCount.Text = table.Rows.Count.ToString("N0");
            _lblQuantity.Text = totalQuantity.ToString("N2");
            _lblSalesTotal.Text = $"{totalAmount:N2} ₺";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Satılan ürün raporu yüklenemedi: {ex.Message}", "Rapor Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}