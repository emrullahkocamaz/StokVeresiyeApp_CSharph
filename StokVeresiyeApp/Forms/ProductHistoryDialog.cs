using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ProductHistoryDialog : Form
{
    private readonly long _productId;
    private readonly DataGridView _grid = new();
    private readonly Label _lblInfo = new();
    private readonly Label _lblStockSummary = new();

    public ProductHistoryDialog(long productId)
    {
        _productId = productId;
        Text = "Ürün Stok Hareket Geçmişi ve Detayı";
        
        var screenArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        int targetW = Math.Clamp(1000, 750, screenArea.Width - 60);
        int targetH = Math.Clamp(620, 440, (int)(screenArea.Height * 0.88));
        ClientSize = new Size(targetW, targetH);

        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        BuildUI();
        LoadHistory();
    }

    private void BuildUI()
    {
        // Üst Özet Paneli
        var topPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 90,
            Padding = new Padding(20, 15, 20, 15)
        };

        _lblInfo.Font = UITheme.SubHeaderFont;
        _lblInfo.ForeColor = UITheme.TextPrimary;
        _lblInfo.Dock = DockStyle.Left;
        _lblInfo.Width = 500;

        _lblStockSummary.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _lblStockSummary.ForeColor = UITheme.Primary;
        _lblStockSummary.Dock = DockStyle.Right;
        _lblStockSummary.TextAlign = ContentAlignment.MiddleRight;
        _lblStockSummary.Width = 400;

        topPanel.Controls.Add(_lblInfo);
        topPanel.Controls.Add(_lblStockSummary);

        // Grid Paneli
        var gridPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        UITheme.ApplyGridStyle(_grid);
        _grid.CellDoubleClick += (s, e) => OpenSelectedPdf();
        gridPanel.Controls.Add(_grid);

        // Alt Buton Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(15, 14, 15, 14)
        };

        var btnExportExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Primary, Color.White, ExportExcelClick, 140, 36);
        var btnOpenPdf = UITheme.CreateButton("👁️ Fatura PDF Önizle", Color.FromArgb(16, 185, 129), Color.White, (s, e) => OpenSelectedPdf(), 175, 36);
        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 90, 36);

        btnExportExcel.Dock = DockStyle.Left;
        btnOpenPdf.Dock = DockStyle.Left;
        btnClose.Dock = DockStyle.Right;

        var pnlGap = new Panel { Dock = DockStyle.Left, Width = 12 };

        bottomPanel.Controls.Add(btnOpenPdf);
        bottomPanel.Controls.Add(pnlGap);
        bottomPanel.Controls.Add(btnExportExcel);
        bottomPanel.Controls.Add(btnClose);

        // Dock sırası: Grid(Fill) -> Bottom -> Top
        Controls.Add(gridPanel);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
        topPanel.SendToBack();
        bottomPanel.SendToBack();
    }

    private void LoadHistory()
    {
        var prod = ProductService.GetById(_productId);
        if (prod == null)
        {
            MessageBox.Show("Ürün bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        var dt = ProductService.GetProductMovements(_productId);
        _grid.DataSource = dt;

        if (_grid.Columns["Id"] != null) _grid.Columns["Id"].Visible = false;
        if (_grid.Columns["PdfPath"] != null) _grid.Columns["PdfPath"].Visible = false;
        if (_grid.Columns["Miktar"] != null) _grid.Columns["Miktar"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Birim Fiyat"] != null) _grid.Columns["Birim Fiyat"].DefaultCellStyle.Format = "N2";
        if (_grid.Columns["Toplam Tutar"] != null) _grid.Columns["Toplam Tutar"].DefaultCellStyle.Format = "N2";

        double inQty = 0, outQty = 0;
        foreach (DataRow r in dt.Rows)
        {
            string type = r["İşlem Türü"]?.ToString() ?? "";
            double q = Convert.ToDouble(r["Miktar"]);
            if (type is "Gelen" or "İade Giriş") inQty += q;
            else outQty += q;
        }

        double remaining = prod.OpeningStock + inQty - outQty;

        _lblInfo.Text = $"{prod.Code} - {prod.Name}\nKategori: {prod.Category} | Birim: {prod.Unit} | Alış: {prod.PurchasePrice:N2} ₺";
        _lblStockSummary.Text = $"Kalan Stok: {remaining:N2} {prod.Unit}\n(Açılış: {prod.OpeningStock:N2} | Giriş: +{inQty:N2} | Çıkış: -{outQty:N2})";
    }

    private void OpenSelectedPdf()
    {
        if (_grid.CurrentRow != null)
        {
            var docNo = _grid.CurrentRow.Cells["Belge No"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(docNo) && InvoiceService.OpenInvoicePdfByNumber(docNo))
            {
                return;
            }

            var pdfVal = _grid.CurrentRow.Cells["PdfPath"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(pdfVal) && File.Exists(pdfVal))
            {
                InvoiceService.OpenPdfFile(pdfVal);
                return;
            }
        }

        if (InvoiceService.OpenLatestInvoicePdfForProduct(_productId))
        {
            return;
        }

        MessageBox.Show("Bu ürün veya seçili harekete ait arşivde kayıtlı bir e-fatura PDF dosyası bulunamadı.", "PDF Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportExcelClick(object? sender, EventArgs e)
    {
        if (_grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak hareket bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"Urun_Hareketleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(dt, "Ürün Hareketleri", sfd.FileName);
                MessageBox.Show("Ürün hareketleri Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
