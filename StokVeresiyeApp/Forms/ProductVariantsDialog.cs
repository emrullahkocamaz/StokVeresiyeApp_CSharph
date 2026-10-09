using System.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class ProductVariantsDialog : BaseModernForm
{
    private readonly long _productId;
    private readonly string _productName;

    private readonly DataGridView _gridVariants = new();
    private readonly TextBox _txtVariantName = new() { PlaceholderText = "Örn: Kırmızı / XL veya 42 Numara" };
    private readonly TextBox _txtBarcode = new() { PlaceholderText = "Varyanta özel barkod okutun..." };
    private readonly TextBox _txtSku = new() { PlaceholderText = "SKU veya Kod..." };
    private readonly TextBox _txtPriceDiff = new() { Text = "0,00" };
    private readonly TextBox _txtStockQty = new() { Text = "0" };
    private long _selectedVariantId = 0;

    public ProductVariantsDialog(long productId, string productName) 
        : base($"🎨 Ürün Varyantları: {productName}", 780, 560)
    {
        _productId = productId;
        _productName = productName;

        BuildInterface();
        RefreshGrid();
    }

    private void BuildInterface()
    {
        var mainContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        // Üst Alan: Varyant Ekleme / Düzenleme Kartı
        var pnlInputs = new CardPanel { Dock = DockStyle.Top, Height = 140, Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 10) };
        var gridInputs = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(0)
        };
        gridInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        gridInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        gridInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        gridInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        void AddInput(string label, Control ctl, int col, int row)
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(4) };
            var lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 18, Font = UITheme.SmallFont, ForeColor = UITheme.TextSecondary };
            ctl.Dock = DockStyle.Bottom;
            pnl.Controls.Add(ctl);
            pnl.Controls.Add(lbl);
            gridInputs.Controls.Add(pnl, col, row);
        }

        AddInput("Varyant Adı (*)", _txtVariantName, 0, 0);
        AddInput("Özel Barkod", _txtBarcode, 1, 0);
        AddInput("SKU / Kod", _txtSku, 2, 0);
        AddInput("Fiyat Farkı (₺)", _txtPriceDiff, 3, 0);

        AddInput("Stok Miktarı", _txtStockQty, 0, 1);

        var btnAdd = UITheme.CreateButton("💾 Varyantı Kaydet", UITheme.Primary, Color.White, (s, e) => SaveVariantClick(), 150, 32);
        var btnClear = UITheme.CreateButton("Temizle", Color.FromArgb(148, 163, 184), Color.White, (s, e) => ClearInputs(), 90, 32);
        var btnDelete = UITheme.CreateButton("🗑️ Sil", Color.FromArgb(239, 68, 68), Color.White, (s, e) => DeleteVariantClick(), 90, 32);

        var pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(4, 18, 4, 4) };
        pnlButtons.Controls.Add(btnAdd);
        pnlButtons.Controls.Add(btnClear);
        pnlButtons.Controls.Add(btnDelete);
        gridInputs.Controls.Add(pnlButtons, 1, 1);
        gridInputs.SetColumnSpan(pnlButtons, 3);

        pnlInputs.Controls.Add(gridInputs);

        // Alt Alan: Varyantlar Tablosu
        var pnlGrid = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        UITheme.ApplyGridStyle(_gridVariants);
        _gridVariants.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridVariants.MultiSelect = false;
        _gridVariants.SelectionChanged += GridSelectionChanged;
        pnlGrid.Controls.Add(_gridVariants);

        mainContainer.Controls.Add(pnlGrid);
        mainContainer.Controls.Add(pnlInputs);

        if (Controls.Find("bodyPanel", true).FirstOrDefault() is Panel bodyPanel)
        {
            bodyPanel.Controls.Clear();
            bodyPanel.Controls.Add(mainContainer);
        }
        else
        {
            Controls.Add(mainContainer);
            mainContainer.BringToFront();
        }

        BtnSave.Text = "Tamam / Kapat";
        BtnSave.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
    }

    private void RefreshGrid()
    {
        try
        {
            var dt = ProductVariantService.GetVariantsTable(_productId);
            _gridVariants.DataSource = dt;
            if (_gridVariants.Columns.Contains("Id"))
            {
                _gridVariants.Columns["Id"].Visible = false;
            }
        }
        catch { }
    }

    private void GridSelectionChanged(object? sender, EventArgs e)
    {
        if (_gridVariants.CurrentRow != null && _gridVariants.CurrentRow.Cells["Id"].Value != null)
        {
            _selectedVariantId = Convert.ToInt64(_gridVariants.CurrentRow.Cells["Id"].Value);
            _txtVariantName.Text = _gridVariants.CurrentRow.Cells["Varyant (Renk/Beden)"].Value?.ToString() ?? "";
            _txtBarcode.Text = _gridVariants.CurrentRow.Cells["Barkod"].Value?.ToString() ?? "";
            _txtSku.Text = _gridVariants.CurrentRow.Cells["Stok Kodu / SKU"].Value?.ToString() ?? "";
            _txtPriceDiff.Text = Convert.ToDouble(_gridVariants.CurrentRow.Cells["Fiyat Farkı (₺)"].Value).ToString("N2");
            _txtStockQty.Text = Convert.ToDouble(_gridVariants.CurrentRow.Cells["Stok Miktarı"].Value).ToString("N2");
        }
    }

    private void ClearInputs()
    {
        _selectedVariantId = 0;
        _txtVariantName.Text = string.Empty;
        _txtBarcode.Text = string.Empty;
        _txtSku.Text = string.Empty;
        _txtPriceDiff.Text = "0,00";
        _txtStockQty.Text = "0";
        _txtVariantName.Focus();
    }

    private void SaveVariantClick()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductVariants) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Ürün varyantlarını ekleme veya düzenleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtVariantName.Text))
        {
            MessageBox.Show("Lütfen varyant adını (örneğin Renk / Beden) giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtVariantName.Focus();
            return;
        }

        try
        {
            var v = new ProductVariant
            {
                Id = _selectedVariantId,
                ProductId = _productId,
                VariantName = _txtVariantName.Text.Trim(),
                Barcode = _txtBarcode.Text.Trim(),
                Sku = _txtSku.Text.Trim(),
                PriceDifference = ParseNumber(_txtPriceDiff.Text),
                StockQuantity = ParseNumber(_txtStockQty.Text)
            };

            ProductVariantService.SaveVariant(v);
            RefreshGrid();
            ClearInputs();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Varyant kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteVariantClick()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductVariants) && !curUser.HasPermission(UserPermissions.ProductsDelete) && !curUser.IsSuperUser)
        {
            MessageBox.Show("Ürün varyantı silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_selectedVariantId <= 0)
        {
            MessageBox.Show("Lütfen silmek istediğiniz varyantı tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show("Seçili varyantı silmek istediğinize emin misiniz?", "Varyant Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                ProductVariantService.DeleteVariant(_selectedVariantId);
                RefreshGrid();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Varyant silinirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static double ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string clean = text.Replace("₺", "").Replace("%", "").Trim();
        if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out double val)) return val;
        if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val)) return val;
        return 0;
    }
}
