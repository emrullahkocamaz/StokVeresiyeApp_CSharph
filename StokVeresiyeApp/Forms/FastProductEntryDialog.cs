using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class FastProductEntryDialog : KryptonForm
{
    private readonly KryptonTextBox _txtBarcode = new();
    private readonly KryptonTextBox _txtName = new();
    private readonly KryptonComboBox _cmbFeatures = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly KryptonComboBox _cmbCategory = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly KryptonComboBox _cmbUnit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtPurchasePrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtSalePrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtQuantity = new() { Text = "1" };
    private readonly KryptonComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly KryptonDataGridView _grid = new();
    private readonly DataTable _recentTable = new();
    private readonly Label _lblCounter = new();

    public FastProductEntryDialog()
    {
        Text = "⚡ Hızlı & Seri Ürün Girişi (Manuel Ürün Ekleme Ekranı)";
        Size = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        InitializeComponents();
        LoadInitialData();
    }

    private void InitializeComponents()
    {
        // Üst Bilgilendirme Bannerı
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "⚡ Hızlı & Manuel Ürün Giriş Ekranı",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 24
        };

        var lblSub = new Label
        {
            Text = "Barkod okutarak veya elle yazarak art arda seri ürün girişi yapabilirsiniz. Bilgileri girip 'Enter' veya 'Kaydet'e basmanız yeterlidir.",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular),
            Dock = DockStyle.Fill
        };

        pnlHeader.Controls.Add(lblSub);
        pnlHeader.Controls.Add(lblTitle);
        Controls.Add(pnlHeader);

        // Giriş Kartı Paneli
        var pnlInputs = new KryptonGroupBox
        {
            Dock = DockStyle.Top,
            Height = 195,
            Values = { Heading = "Yeni Ürün Bilgileri" },
            Padding = new Padding(12)
        };
        pnlInputs.Panel.Padding = new Padding(10);

        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));

        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));

        // Satır 1: Barkod, Ürün Adı (2 col), Özellik (Sprey vb.), Kategori, Birim
        tbl.Controls.Add(CreateFieldPanel("Barkod (İsteğe Bağlı):", _txtBarcode), 0, 0);
        tbl.Controls.Add(CreateFieldPanel("Ürün Adı (*):", _txtName), 1, 0);
        tbl.SetColumnSpan(tbl.GetControlFromPosition(1, 0)!, 2);

        tbl.Controls.Add(CreateFieldPanel("Özellik / Tür (Sprey, Sıvı vb.):", _cmbFeatures), 3, 0);
        tbl.Controls.Add(CreateFieldPanel("Kategori:", _cmbCategory), 4, 0);
        tbl.Controls.Add(CreateFieldPanel("Birim:", _cmbUnit), 5, 0);

        // Satır 2: Alış Fiyatı, Satış Fiyatı, Giriş Stoğu, Hedef Depo, Otomatik Barkod Butonu, Kaydet Butonu
        tbl.Controls.Add(CreateFieldPanel("Alış Fiyatı (₺):", _txtPurchasePrice), 0, 1);
        tbl.Controls.Add(CreateFieldPanel("Satış Fiyatı (₺):", _txtSalePrice), 1, 1);
        tbl.Controls.Add(CreateFieldPanel("Giriş Adeti / Stok:", _txtQuantity), 2, 1);
        tbl.Controls.Add(CreateFieldPanel("Hedef Depo:", _cmbWarehouse), 3, 1);

        var btnGenBarcode = new KryptonButton
        {
            Text = "🎲 Barkod Üret",
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand
        };
        btnGenBarcode.Click += (s, e) => _txtBarcode.Text = DateTime.Now.ToString("869yyMMddHHmm");
        tbl.Controls.Add(CreateButtonPanel("Hızlı İşlem:", btnGenBarcode), 4, 1);

        var btnAdd = new KryptonButton
        {
            Text = "💾 Kaydet & Sonraki (Enter)",
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand
        };
        btnAdd.StateCommon.Back.Color1 = Color.FromArgb(16, 185, 129);
        btnAdd.StateCommon.Back.Color2 = Color.FromArgb(5, 150, 105);
        btnAdd.StateCommon.Content.ShortText.Color1 = Color.White;
        btnAdd.StateCommon.Content.ShortText.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        btnAdd.Click += (s, e) => SaveAndNext();
        tbl.Controls.Add(CreateButtonPanel("Seri Ekle:", btnAdd), 5, 1);

        // Enter tuşu dinleyicileri
        _txtBarcode.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _txtName.Focus(); };
        _txtName.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _cmbFeatures.Focus(); };
        _cmbFeatures.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _txtPurchasePrice.Focus(); };
        _txtPurchasePrice.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _txtSalePrice.Focus(); };
        _txtSalePrice.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) _txtQuantity.Focus(); };
        _txtQuantity.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SaveAndNext(); };

        pnlInputs.Panel.Controls.Add(tbl);
        Controls.Add(pnlInputs);

        // Alt Bar (Durum ve Sayaç)
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(15, 6, 15, 6)
        };

        _lblCounter.Text = "Bu oturumda eklenen: 0 ürün";
        _lblCounter.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _lblCounter.ForeColor = Color.FromArgb(30, 41, 59);
        _lblCounter.Dock = DockStyle.Left;
        _lblCounter.AutoSize = true;

        var btnClose = new Button
        {
            Text = "Tamamla ve Kapat",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 150,
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;

        pnlBottom.Controls.Add(_lblCounter);
        pnlBottom.Controls.Add(btnClose);
        Controls.Add(pnlBottom);

        // Orta: Bu Oturumda Eklenen Ürünler Tablosu
        var pnlGridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 6, 12, 6)
        };

        var lblGridTitle = new Label
        {
            Text = "📋 Bu Oturumda Seri Olarak Eklenen Ürünler",
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Top,
            Height = 26
        };

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowTemplate.Height = 28;

        _recentTable.Columns.Add("Saat", typeof(string));
        _recentTable.Columns.Add("Barkod", typeof(string));
        _recentTable.Columns.Add("Ürün Adı", typeof(string));
        _recentTable.Columns.Add("Özellik", typeof(string));
        _recentTable.Columns.Add("Kategori", typeof(string));
        _recentTable.Columns.Add("Alış (₺)", typeof(string));
        _recentTable.Columns.Add("Satış (₺)", typeof(string));
        _recentTable.Columns.Add("Giriş Stoğu", typeof(string));
        _recentTable.Columns.Add("Hedef Depo", typeof(string));

        _grid.DataSource = _recentTable;

        pnlGridContainer.Controls.Add(_grid);
        pnlGridContainer.Controls.Add(lblGridTitle);
        Controls.Add(pnlGridContainer);

        pnlInputs.BringToFront();
        pnlGridContainer.BringToFront();
    }

    private Control CreateFieldPanel(string labelText, Control inputControl)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(3) };
        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Top,
            Height = 18
        };
        inputControl.Dock = DockStyle.Top;
        pnl.Controls.Add(inputControl);
        pnl.Controls.Add(lbl);
        return pnl;
    }

    private Control CreateButtonPanel(string labelText, Control buttonControl)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(3) };
        var lbl = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Dock = DockStyle.Top,
            Height = 18
        };
        buttonControl.Dock = DockStyle.Top;
        buttonControl.Height = 28;
        pnl.Controls.Add(buttonControl);
        pnl.Controls.Add(lbl);
        return pnl;
    }

    private void LoadInitialData()
    {
        // Birimler
        _cmbUnit.Items.AddRange(new object[] { "Adet", "Kg", "Gram", "Litre", "Paket", "Koli", "Metre", "Kutu" });
        _cmbUnit.SelectedIndex = 0;

        // Özellikler
        _cmbFeatures.Items.AddRange(new object[] { 
            "Sprey", "Sıvı", "Tablet", "Toz", "Kapsül", "Jel", "Krem", "Merhem", "Damla", "Ampul", 
            "Süspansiyon", "Şurup", "Solüsyon", "Köpük", "Granül", "Standart" 
        });

        // Kategoriler
        var cats = Database.Query("SELECT DISTINCT Category FROM Products WHERE IsActive=1 AND Category IS NOT NULL AND Category != ''");
        foreach (DataRow r in cats.Rows)
        {
            _cmbCategory.Items.Add(r[0].ToString()!);
        }
        if (!_cmbCategory.Items.Contains("Genel")) _cmbCategory.Items.Add("Genel");
        if (!_cmbCategory.Items.Contains("Gıda")) _cmbCategory.Items.Add("Gıda");
        if (!_cmbCategory.Items.Contains("Temizlik")) _cmbCategory.Items.Add("Temizlik");
        if (!_cmbCategory.Items.Contains("Kırtasiye")) _cmbCategory.Items.Add("Kırtasiye");
        _cmbCategory.Text = "Genel";

        // Depolar
        var whs = WarehouseService.GetActiveWarehouses();
        foreach (var w in whs)
        {
            _cmbWarehouse.Items.Add(new WarehouseComboItem(w.Id, w.Name));
        }
        if (_cmbWarehouse.Items.Count > 0) _cmbWarehouse.SelectedIndex = 0;
    }

    private void SaveAndNext()
    {
        string name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen ürün adını yazınız.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        string barcode = _txtBarcode.Text.Trim();
        string features = _cmbFeatures.Text.Trim();
        string category = string.IsNullOrWhiteSpace(_cmbCategory.Text) ? "Genel" : _cmbCategory.Text.Trim();
        string unit = _cmbUnit.Text.Trim();
        double buyPrice = ParseNumber(_txtPurchasePrice.Text);
        double salePrice = ParseNumber(_txtSalePrice.Text);
        double qty = ParseNumber(_txtQuantity.Text);
        long? whId = (_cmbWarehouse.SelectedItem as WarehouseComboItem)?.Id;

        try
        {
            // Barkod veya isimle mevcut ürün var mı kontrol et
            Product? existing = null;
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                existing = ProductService.GetByCode(barcode);
            }

            if (existing != null)
            {
                // Ürün zaten var! Üzerine stok ekle ve fiyat/tarihçe kaydet
                double oldStock = ProductService.GetStock(existing.Id);
                double newStock = oldStock + qty;

                // Stok hareketi ekle
                if (qty > 0)
                {
                    Database.Execute(@"
INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, WarehouseId, Note)
VALUES (@date, @pid, 'Gelen', @qty, @price, @doc, @wId, @note);",
                        ("@date", DateTime.Now.ToString("yyyy-MM-dd")),
                        ("@pid", existing.Id),
                        ("@qty", qty),
                        ("@price", buyPrice > 0 ? buyPrice : existing.PurchasePrice),
                        ("@doc", "MANUEL-GİRİŞ"),
                        ("@wId", (object?)whId ?? DBNull.Value),
                        ("@note", "Hızlı Manuel Seri Giriş")
                    );
                }

                // Fiyat güncelle
                if (buyPrice > 0 || salePrice > 0)
                {
                    Database.Execute("UPDATE Products SET PurchasePrice = CASE WHEN @p > 0 THEN @p ELSE PurchasePrice END, SalePrice = CASE WHEN @s > 0 THEN @s ELSE SalePrice END WHERE Id = @id;",
                        ("@p", buyPrice),
                        ("@s", salePrice),
                        ("@id", existing.Id)
                    );
                }

                // Tarihçeye kaydet
                ProductService.RecordPriceHistory(
                    productId: existing.Id,
                    docNo: "MANUEL-SERİ",
                    supplier: "Manuel Giriş",
                    oldStock: oldStock,
                    addedStock: qty,
                    newStock: newStock,
                    oldBuy: existing.PurchasePrice,
                    newBuy: buyPrice > 0 ? buyPrice : existing.PurchasePrice,
                    oldSale: existing.SalePrice,
                    newSale: salePrice > 0 ? salePrice : existing.SalePrice,
                    note: "Manuel seri ürün girişi ile stok/fiyat güncellendi"
                );

                _recentTable.Rows.InsertAt(_recentTable.NewRow(), 0);
                var row = _recentTable.Rows[0];
                row["Saat"] = DateTime.Now.ToString("HH:mm:ss");
                row["Barkod"] = existing.Barcode;
                row["Ürün Adı"] = $"{existing.Name} (Mevcut Ürüne Stok Eklendi)";
                row["Özellik"] = features;
                row["Kategori"] = category;
                row["Alış (₺)"] = $"{buyPrice:N2} ₺";
                row["Satış (₺)"] = $"{salePrice:N2} ₺";
                row["Giriş Stoğu"] = $"+{qty:N2} (Toplam: {newStock:N2})";
                row["Hedef Depo"] = _cmbWarehouse.Text;
            }
            else
            {
                // Yeni ürün oluştur
                string autoCode = GenerateCode();
                Database.Execute(@"
INSERT INTO Products(Code, Barcode, Name, Category, Unit, OpeningStock, PurchasePrice, SalePrice, VatPercent, MinStockLevel, Features)
VALUES(@c, @b, @n, @cat, @u, @o, @p, @sp, 20, 5, @feat);",
                    ("@c", autoCode),
                    ("@b", string.IsNullOrWhiteSpace(barcode) ? (object)DBNull.Value : barcode),
                    ("@n", name),
                    ("@cat", category),
                    ("@u", unit),
                    ("@o", qty),
                    ("@p", buyPrice),
                    ("@sp", salePrice),
                    ("@feat", string.IsNullOrWhiteSpace(features) ? (object)DBNull.Value : features)
                );

                var newProd = ProductService.GetByCode(autoCode);
                if (newProd != null && qty > 0)
                {
                    ProductService.RecordPriceHistory(
                        productId: newProd.Id,
                        docNo: "YENİ-KAYIT",
                        supplier: "Manuel Giriş",
                        oldStock: 0,
                        addedStock: qty,
                        newStock: qty,
                        oldBuy: 0,
                        newBuy: buyPrice,
                        oldSale: 0,
                        newSale: salePrice,
                        note: "Hızlı manuel giriş ile ilk stok oluşturuldu"
                    );
                }

                _recentTable.Rows.InsertAt(_recentTable.NewRow(), 0);
                var row = _recentTable.Rows[0];
                row["Saat"] = DateTime.Now.ToString("HH:mm:ss");
                row["Barkod"] = barcode;
                row["Ürün Adı"] = name;
                row["Özellik"] = features;
                row["Kategori"] = category;
                row["Alış (₺)"] = $"{buyPrice:N2} ₺";
                row["Satış (₺)"] = $"{salePrice:N2} ₺";
                row["Giriş Stoğu"] = $"{qty:N2} {unit}";
                row["Hedef Depo"] = _cmbWarehouse.Text;
            }

            _lblCounter.Text = $"Bu oturumda eklenen: {_recentTable.Rows.Count} ürün";

            // Sonraki giriş için alanları temizle
            _txtBarcode.Text = "";
            _txtName.Text = "";
            _txtPurchasePrice.Text = "0,00";
            _txtSalePrice.Text = "0,00";
            _txtQuantity.Text = "1";
            _txtBarcode.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ürün kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GenerateCode()
    {
        try
        {
            var dt = Database.Query("SELECT Code FROM Products WHERE Code LIKE 'URN-%'");
            long maxNum = 0;
            foreach (DataRow row in dt.Rows)
            {
                string c = row["Code"]?.ToString() ?? "";
                if (c.StartsWith("URN-", StringComparison.OrdinalIgnoreCase))
                {
                    string numPart = c.Substring(4);
                    if (long.TryParse(numPart, out long n) && n > maxNum) maxNum = n;
                }
            }
            return $"URN-{(maxNum + 1):D4}";
        }
        catch
        {
            return $"URN-{DateTime.Now.Ticks % 10000:D4}";
        }
    }

    private static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        text = text.Replace("₺", "").Trim();
        if (double.TryParse(text.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val)) return val;
        if (double.TryParse(text, out double val2)) return val2;
        return 0;
    }

    private class WarehouseComboItem
    {
        public long Id { get; }
        public string Name { get; }
        public WarehouseComboItem(long id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }
}
