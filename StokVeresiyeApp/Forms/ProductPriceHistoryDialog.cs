using System;
using System.Drawing;
using System.Windows.Forms;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ProductPriceHistoryDialog : Form
{
    private readonly long _productId;
    private readonly Product? _product;
    private DataGridView _grid = new();
    private Label _lblSummary = new();

    public ProductPriceHistoryDialog(long productId)
    {
        _productId = productId;
        _product = ProductService.GetById(productId);

        Text = $"📜 Stok & Fiyat Değişim Tarihçesi - {_product?.Name ?? "Ürün"}";
        
        var screenArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
        int targetW = Math.Clamp(1000, 750, screenArea.Width - 60);
        int targetH = Math.Clamp(580, 420, (int)(screenArea.Height * 0.85));
        ClientSize = new Size(targetW, targetH);

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(248, 249, 250);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        InitializeComponents();
        LoadHistory();
    }

    private void InitializeComponents()
    {
        // Üst Başlık Paneli
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(24, 43, 73),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = $"Ürün: {_product?.Name} ({_product?.Code})",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28
        };

        double curStock = _productId > 0 ? ProductService.GetStock(_productId) : 0;
        _lblSummary = new Label
        {
            Text = $"Barkod: {_product?.Barcode ?? "-"}   |   Mevcut Stok: {curStock:N2} {_product?.Unit}   |   Güncel Alış: {_product?.PurchasePrice:N2} ₺   |   Satış: {_product?.SalePrice:N2} ₺",
            ForeColor = Color.FromArgb(200, 220, 245),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Dock = DockStyle.Fill
        };

        pnlHeader.Controls.Add(_lblSummary);
        pnlHeader.Controls.Add(lblTitle);
        Controls.Add(pnlHeader);

        // Alt Buton Paneli
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(238, 242, 246),
            Padding = new Padding(15, 8, 15, 8)
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 110,
            BackColor = Color.FromArgb(220, 53, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        pnlBottom.Controls.Add(btnClose);
        Controls.Add(pnlBottom);

        // Tablo Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            RowTemplate = { Height = 32 }
        };

        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 58, 95);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.ColumnHeadersHeight = 36;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.Black;

        Controls.Add(_grid);
        _grid.BringToFront();
    }

    private void LoadHistory()
    {
        try
        {
            var dt = ProductService.GetProductPriceHistory(_productId);
            _grid.DataSource = dt;

            // Para birimi formatlama
            if (_grid.Columns.Contains("Önceki Alış (₺)"))
                _grid.Columns["Önceki Alış (₺)"].DefaultCellStyle.Format = "N2";
            if (_grid.Columns.Contains("Yeni Alış (₺)"))
                _grid.Columns["Yeni Alış (₺)"].DefaultCellStyle.Format = "N2";
            if (_grid.Columns.Contains("Önceki Satış (₺)"))
                _grid.Columns["Önceki Satış (₺)"].DefaultCellStyle.Format = "N2";
            if (_grid.Columns.Contains("Yeni Satış (₺)"))
                _grid.Columns["Yeni Satış (₺)"].DefaultCellStyle.Format = "N2";
            if (_grid.Columns.Contains("Id"))
                _grid.Columns["Id"].Visible = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Tarihçe yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
