using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class WarehouseTransferDialog : BaseModernForm
{
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbSourceWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbTargetWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtBarcode = new();
    private readonly ComboBox _cmbProduct = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtCurrentStock = new() { ReadOnly = true, BackColor = Color.FromArgb(241, 245, 249), Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
    private readonly NumericUpDown _nudQty = new() { Minimum = 1, Maximum = 999999, Value = 1, DecimalPlaces = 2, Width = 150 };
    private readonly TextBox _txtDocNo = new();
    private readonly TextBox _txtNote = new();

    public WarehouseTransferDialog() : base("🔄 Depolar & Şubeler Arası Stok Transfer Fişi", 640, 580)
    {
        BuildFormControls();
        LoadWarehouses();
        LoadProducts();
        UpdateStockInfo();
    }

    private void BuildFormControls()
    {
        _txtDocNo.Text = $"TRF-{DateTime.Now:yyyyMMddHHmmss}";

        _txtBarcode.PlaceholderText = "🔍 Barkod okutun veya aşağıdan seçin...";
        _txtBarcode.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SearchProductByBarcode();
            }
        };

        _cmbSourceWarehouse.SelectedIndexChanged += (s, e) => UpdateStockInfo();
        _cmbProduct.SelectedIndexChanged += (s, e) => UpdateStockInfo();

        AddRow("Transfer Tarihi", _dtpDate);
        AddRow("Çıkış Yapılacak Kaynak Depo (*)", _cmbSourceWarehouse);
        AddRow("Giriş Yapılacak Hedef Depo (*)", _cmbTargetWarehouse);
        AddRow("Barkod Okut (Hızlı Seçim)", _txtBarcode);
        AddRow("Transfer Edilecek Ürün (*)", _cmbProduct);
        AddRow("Kaynak Depodaki Mevcut Stok", _txtCurrentStock);
        AddRow("Transfer Miktarı (*)", _nudQty);
        AddRow("Belge / Sevk İrsaliye No", _txtDocNo);
        AddRow("Açıklama / Sevk Notu", _txtNote);

        BtnSave.Text = "🔄 Transferi Gerçekleştir";
        BtnSave.BackColor = Color.FromArgb(13, 148, 136); // Teal
        BtnSave.Click += TransferClick;
    }

    private void LoadWarehouses()
    {
        var warehouses = WarehouseService.GetActiveWarehouses();
        _cmbSourceWarehouse.DataSource = new BindingSource(warehouses, null);
        _cmbSourceWarehouse.DisplayMember = "Name";
        _cmbSourceWarehouse.ValueMember = "Id";

        var warehousesTarget = WarehouseService.GetActiveWarehouses();
        _cmbTargetWarehouse.DataSource = new BindingSource(warehousesTarget, null);
        _cmbTargetWarehouse.DisplayMember = "Name";
        _cmbTargetWarehouse.ValueMember = "Id";

        if (warehouses.Count > 1)
        {
            _cmbTargetWarehouse.SelectedIndex = 1;
        }
    }

    private void LoadProducts()
    {
        var dt = Database.Query(@"
SELECT Id, Code + ' - ' + Name + ' (' + Unit + ')' AS Display, Barcode 
FROM Products 
WHERE IsActive=1 
ORDER BY Name");

        _cmbProduct.DataSource = dt;
        _cmbProduct.DisplayMember = "Display";
        _cmbProduct.ValueMember = "Id";
    }

    private void SearchProductByBarcode()
    {
        string bc = _txtBarcode.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(bc)) return;

        if (_cmbProduct.DataSource is DataTable dt)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (string.Equals(dt.Rows[i]["Barcode"]?.ToString(), bc, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbProduct.SelectedIndex = i;
                    _txtBarcode.Clear();
                    _nudQty.Focus();
                    _nudQty.Select(0, _nudQty.Text.Length);
                    return;
                }
            }
        }
        MessageBox.Show($"'{bc}' barkoduna sahip ürün bulunamadı.", "Barkod Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateStockInfo()
    {
        if (_cmbProduct.SelectedValue == null || _cmbSourceWarehouse.SelectedValue == null)
        {
            _txtCurrentStock.Text = "-";
            return;
        }

        try
        {
            long pId = Convert.ToInt64(_cmbProduct.SelectedValue);
            long wId = Convert.ToInt64(_cmbSourceWarehouse.SelectedValue);
            double qty = WarehouseService.GetProductStockInWarehouse(pId, wId);

            _txtCurrentStock.Text = $"{qty:N2} Adet / Birim";
            _txtCurrentStock.ForeColor = qty <= 0 ? UITheme.Danger : Color.FromArgb(22, 101, 52);
        }
        catch
        {
            _txtCurrentStock.Text = "-";
        }
    }

    private void TransferClick(object? sender, EventArgs e)
    {
        if (_cmbSourceWarehouse.SelectedValue == null || _cmbTargetWarehouse.SelectedValue == null)
        {
            MessageBox.Show("Lütfen kaynak ve hedef depoları seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        long srcId = Convert.ToInt64(_cmbSourceWarehouse.SelectedValue);
        long tgtId = Convert.ToInt64(_cmbTargetWarehouse.SelectedValue);

        if (srcId == tgtId)
        {
            MessageBox.Show("Kaynak depo ile hedef depo aynı olamaz! Lütfen farklı bir hedef depo seçiniz.", "Hatalı Seçim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (_cmbProduct.SelectedValue == null)
        {
            MessageBox.Show("Lütfen transfer edilecek ürünü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        long pId = Convert.ToInt64(_cmbProduct.SelectedValue);
        double transferQty = (double)_nudQty.Value;
        if (transferQty <= 0)
        {
            MessageBox.Show("Transfer miktarı sıfırdan büyük olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        double availableStock = WarehouseService.GetProductStockInWarehouse(pId, srcId);
        if (transferQty > availableStock)
        {
            if (MessageBox.Show($"Kaynak depoda sadece {availableStock:N2} adet ürün bulunmaktadır. {transferQty:N2} adet transfer etmek kaynak depoyu eksi stoğa düşürecektir. Devam etmek istiyor musunuz?", "Eksi Stok Uyarısı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }
        }

        try
        {
            WarehouseService.TransferStock(srcId, tgtId, pId, transferQty, _txtDocNo.Text, _txtNote.Text);
            MessageBox.Show("Depolar arası transfer başarıyla tamamlandı ve stok hareketlerine işlendi.", "Transfer Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Transfer hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
