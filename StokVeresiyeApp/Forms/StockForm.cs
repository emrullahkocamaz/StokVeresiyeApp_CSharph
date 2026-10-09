using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class StockForm : BaseModernForm
{
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbProduct = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtQty = new() { Text = "1" };
    private readonly TextBox _txtUnitPrice = new() { Text = "0,00" };
    private readonly Label _lblTotalPrice = new() { ForeColor = UITheme.Primary, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly ComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtDocNo = new();
    private readonly TextBox _txtNote = new();
    private readonly DataGridView _gridReadyProducts = new();

    public StockForm() : base("Stok Hareketi Ekle", 620, 520)
    {
        // Depolar
        var warehouses = WarehouseService.GetActiveWarehouses();
        _cmbWarehouse.DataSource = warehouses;
        _cmbWarehouse.DisplayMember = "Name";
        _cmbWarehouse.ValueMember = "Id";

        // Ürünler
        var dtProducts = Database.Query("SELECT Id, Code + ' - ' + Name AS Display, PurchasePrice, SalePrice FROM Products WHERE IsActive=1 ORDER BY Name");
        _cmbProduct.DataSource = dtProducts;
        _cmbProduct.DisplayMember = "Display";
        _cmbProduct.ValueMember = "Id";

        // Cariler (Opsiyonel)
        var dtAccounts = Database.Query("SELECT 0 AS Id, '(Cari Seçilmedi / Genel)' AS Display UNION ALL SELECT Id, Name + ' (' + Type + ')' AS Display FROM Accounts WHERE IsActive=1 ORDER BY Display");
        _cmbAccount.DataSource = dtAccounts;
        _cmbAccount.DisplayMember = "Display";
        _cmbAccount.ValueMember = "Id";

        // Hareket Türleri
        _cmbType.Items.AddRange(new object[] { "Gelen", "Satılan", "İade Giriş", "Fire / Zayi" });
        _cmbType.SelectedIndex = 0;

        AddRow("Hareket Tarihi", _dtpDate);
        AddRow("Depo / Şube (*)", _cmbWarehouse);
        AddRow("Ürün (*)", _cmbProduct);
        AddRow("Hareket Türü (*)", _cmbType);
        AddRow("Miktar (*)", _txtQty);
        AddRow("Birim Fiyat (₺)", _txtUnitPrice);
        AddRow("Toplam Tutar", _lblTotalPrice);
        AddRow("İlgili Cari Firma (Opsiyonel)", _cmbAccount);
        AddRow("Belge / İrsaliye No", _txtDocNo);
        AddRow("Açıklama / Not", _txtNote);

        _gridReadyProducts.ReadOnly = true;
        _gridReadyProducts.AllowUserToAddRows = false;
        _gridReadyProducts.RowHeadersVisible = false;
        _gridReadyProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridReadyProducts.Font = UITheme.SmallFont;
        _gridReadyProducts.MinimumSize = new Size(0, 150);
        BindReadyProducts();
        AddRow("Satışa Hazır Ürünler", _gridReadyProducts, 190);

        _cmbProduct.SelectedIndexChanged += ProductChanged;
        _txtQty.TextChanged += UpdateTotal;
        _txtUnitPrice.TextChanged += UpdateTotal;

        BtnSave.Click += SaveClick;

        if (dtProducts.Rows.Count > 0)
        {
            ProductChanged(null, EventArgs.Empty);
        }
    }

    private void BindReadyProducts()
    {
        try
        {
            var dt = ProductService.GetAllProducts();
            _gridReadyProducts.DataSource = dt;

            if (_gridReadyProducts.Columns.Contains("Id")) _gridReadyProducts.Columns["Id"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Fatura No")) _gridReadyProducts.Columns["Fatura No"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Açılış")) _gridReadyProducts.Columns["Açılış"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Gelen")) _gridReadyProducts.Columns["Gelen"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Satılan")) _gridReadyProducts.Columns["Satılan"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Kritik Seviye")) _gridReadyProducts.Columns["Kritik Seviye"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Toplam Tutar")) _gridReadyProducts.Columns["Toplam Tutar"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Özellik / Tür")) _gridReadyProducts.Columns["Özellik / Tür"].Visible = false;

            if (_gridReadyProducts.Columns.Contains("Kalan Stok"))
            {
                _gridReadyProducts.Columns["Kalan Stok"].HeaderText = "Stok";
                _gridReadyProducts.Columns["Kalan Stok"].DefaultCellStyle.Format = "N2";
                _gridReadyProducts.Columns["Kalan Stok"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (_gridReadyProducts.Columns.Contains("Satış Fiyatı"))
            {
                _gridReadyProducts.Columns["Satış Fiyatı"].HeaderText = "Satış Fiyatı";
                _gridReadyProducts.Columns["Satış Fiyatı"].DefaultCellStyle.Format = "N2";
                _gridReadyProducts.Columns["Satış Fiyatı"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            if (_gridReadyProducts.Columns.Contains("Ürün Adı"))
            {
                _gridReadyProducts.Columns["Ürün Adı"].HeaderText = "Ürün";
            }
        }
        catch { }
    }

    private void ProductChanged(object? sender, EventArgs e)
    {
        if (_cmbProduct.SelectedItem is DataRowView rowView)
        {
            bool isOut = _cmbType.Text == "Satılan";
            double price = isOut ? Convert.ToDouble(rowView["SalePrice"]) : Convert.ToDouble(rowView["PurchasePrice"]);
            if (price > 0) _txtUnitPrice.Text = price.ToString("N2");
        }
        UpdateTotal(null, EventArgs.Empty);
    }

    private void UpdateTotal(object? sender, EventArgs e)
    {
        double q = ParseNumber(_txtQty.Text);
        double p = ParseNumber(_txtUnitPrice.Text);
        _lblTotalPrice.Text = $"{q * p:N2} ₺";
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        if (_cmbProduct.SelectedValue == null || ParseNumber(_txtQty.Text) <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir ürün ve pozitif bir miktar giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        try
        {
            long accountId = Convert.ToInt64(_cmbAccount.SelectedValue);
            long warehouseId = _cmbWarehouse.SelectedValue != null ? Convert.ToInt64(_cmbWarehouse.SelectedValue) : 1;
            Database.Execute(@"
INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note) 
VALUES($d, $p, $t, $q, $up, $doc, $acc, $wId, $n)",
                ("$d", _dtpDate.Value.ToString("yyyy-MM-dd")),
                ("$p", _cmbProduct.SelectedValue),
                ("$t", _cmbType.Text),
                ("$q", ParseNumber(_txtQty.Text)),
                ("$up", ParseNumber(_txtUnitPrice.Text)),
                ("$doc", string.IsNullOrWhiteSpace(_txtDocNo.Text) ? null : _txtDocNo.Text.Trim()),
                ("$acc", accountId > 0 ? accountId : null),
                ("$wId", warehouseId),
                ("$n", string.IsNullOrWhiteSpace(_txtNote.Text) ? null : _txtNote.Text.Trim()));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kayıt hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
