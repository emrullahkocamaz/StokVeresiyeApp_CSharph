using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;
using System.Data;

namespace StokVeresiyeApp.Forms;

public class ProductForm : BaseModernForm
{
    private readonly KryptonTextBox _txtCode = new();
    private readonly KryptonTextBox _txtBarcode = new();
    private readonly KryptonTextBox _txtName = new();
    private readonly KryptonComboBox _cmbCategory = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly KryptonComboBox _cmbUnit = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtOpeningStock = new() { Text = "0" };
    private readonly KryptonTextBox _txtMinStock = new() { Text = "5" };
    private readonly KryptonTextBox _txtPurchasePrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtDiscount = new() { Text = "0" };
    private readonly KryptonTextBox _txtVat = new() { Text = "20" };
    private readonly KryptonTextBox _txtSalePrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtWholesalePrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtSpecialPrice = new() { Text = "0,00" };
    private readonly KryptonDateTimePicker _dtpExpiryDate = new() 
    { 
        Format = DateTimePickerFormat.Short, 
        ShowCheckBox = true, 
        Checked = false, 
        Width = 160 
    };
    private readonly KryptonTextBox _txtBatchNumber = new() { Width = 200 };
    private readonly KryptonComboBox _cmbFeatures = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly KryptonLabel _lblCalculatedPrice = new() { Text = "0,00 ₺" };

    private readonly long _id = -1;

    public ProductForm(long? productId = null) : base(productId.HasValue && productId.Value > 0 ? "Ürün Kartı Düzenle" : "Yeni Ürün Tanımla", 660, 750)
    {
        _id = productId ?? -1;

        // Birimler
        _cmbUnit.Items.AddRange(new object[] { "Adet", "Kg", "Gram", "Litre", "Paket", "Koli", "Metre", "Kutu" });
        _cmbUnit.SelectedIndex = 0;

        // Özellik / Formlar
        _cmbFeatures.Items.AddRange(new object[] { 
            "Sprey", "Sıvı", "Tablet", "Toz", "Kapsül", "Jel", "Krem", "Merhem", "Damla", "Ampul", 
            "Süspansiyon", "Şurup", "Solüsyon", "Köpük", "Granül", "Standart" 
        });

        // Kategoriler
        var cats = Database.Query("SELECT DISTINCT Category FROM Products WHERE IsActive=1 AND Category IS NOT NULL AND Category != ''");
        foreach (System.Data.DataRow r in cats.Rows)
        {
            _cmbCategory.Items.Add(r[0].ToString()!);
        }
        if (!_cmbCategory.Items.Contains("Genel")) _cmbCategory.Items.Add("Genel");
        if (!_cmbCategory.Items.Contains("Gıda")) _cmbCategory.Items.Add("Gıda");
        if (!_cmbCategory.Items.Contains("Temizlik")) _cmbCategory.Items.Add("Temizlik");
        if (!_cmbCategory.Items.Contains("Kırtasiye")) _cmbCategory.Items.Add("Kırtasiye");
        _cmbCategory.Text = "Genel";

        // Form alanları
        AddRow("Ürün Kodu (*)", _txtCode);
        AddRow("Barkod No", _txtBarcode);
        AddRow("Ürün Adı (*)", _txtName);
        AddRow("Özellik / Tür (Sprey vb.)", _cmbFeatures);
        AddRow("Kategori", _cmbCategory);
        AddRow("Birim", _cmbUnit);
        AddRow("Açılış Stoğu", _txtOpeningStock);
        AddRow("Kritik Stok Uyarısı", _txtMinStock);
        AddRow("Alış Fiyatı (KDV Hariç)", _txtPurchasePrice);
        AddRow("İskonto %", _txtDiscount);
        AddRow("KDV Oranı %", _txtVat);
        AddRow("Perakende Satış Fiyatı", _txtSalePrice);
        AddRow("Toptan Satış Fiyatı", _txtWholesalePrice);
        AddRow("Özel / Bayi Fiyatı", _txtSpecialPrice);
        AddRow("Son Kullanma Tarihi (SKT)", _dtpExpiryDate);
        AddRow("Parti / Lot Numarası", _txtBatchNumber);
        AddRow("Hesaplanan Satış Fiyatı", _lblCalculatedPrice);

        _txtPurchasePrice.TextChanged += CalculateSalePrice;
        _txtDiscount.TextChanged += CalculateSalePrice;
        _txtVat.TextChanged += CalculateSalePrice;

        BtnSave.Click += SaveClick;

        if (_id > 0)
        {
            LoadData();

            try
            {
                var dtInv = InvoiceService.GetProductInvoices(_id);
                if (dtInv.Rows.Count > 0)
                {
                    var firstRow = dtInv.Rows[0];
                    string invNo = firstRow["Fatura No"]?.ToString() ?? "";
                    string invDate = firstRow["Fatura Tarihi"]?.ToString() ?? "";
                    string supplier = firstRow["Tedarikçi"]?.ToString() ?? "-";
                    string qtyUnit = $"{firstRow["Alınan Miktar"]} {firstRow["Birim"]}";
                    string buyPrice = $"{Convert.ToDouble(firstRow["Alış Fiyatı (₺)"]):N2} ₺";

                    var pnlPdfBox = new Panel
                    {
                        Height = 78,
                        Dock = DockStyle.Fill,
                        BackColor = Color.FromArgb(240, 249, 255),
                        Padding = new Padding(10, 8, 10, 8)
                    };

                    var lblInvInfo = new Label
                    {
                        Text = $"📄 Fatura: {invNo} | Tarih: {invDate}\n🏢 Tedarikçi: {supplier} | Alış: {qtyUnit} @ {buyPrice}",
                        Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                        ForeColor = Color.FromArgb(15, 23, 42),
                        Dock = DockStyle.Top,
                        Height = 36
                    };

                    var btnOpenPdf = UITheme.CreateButton($"👁️ Fatura PDF Önizle & Aç ({invNo})", Color.FromArgb(20, 176, 186), Color.White, (s, e) =>
                    {
                        if (!InvoiceService.OpenInvoicePdfByNumber(invNo))
                        {
                            InvoiceService.OpenLatestInvoicePdfForProduct(_id);
                        }
                    }, 260, 30);
                    btnOpenPdf.Dock = DockStyle.Left;

                    pnlPdfBox.Controls.Add(btnOpenPdf);
                    pnlPdfBox.Controls.Add(lblInvInfo);

                    AddRow("E-Fatura & PDF", pnlPdfBox, 86);
                }

                // Varyant Yönetimi (Renk, Beden, Numara, Model)
                var btnManageVariants = UITheme.CreateButton("🎨 Varyantları Yönet (Renk / Beden / Model)", Color.FromArgb(79, 70, 229), Color.White, (s, e) =>
                {
                    using var dlg = new ProductVariantsDialog(_id, _txtName.Text.Trim());
                    dlg.ShowDialog(this);
                }, 320, 34);
                AddRow("Ürün Varyantları", btnManageVariants, 42);

                // Stok & Fiyat Geçmişi (Tarihçe)
                var btnHistory = UITheme.CreateButton("📜 Stok & Fiyat Değişim Tarihçesi", Color.FromArgb(16, 185, 129), Color.White, (s, e) =>
                {
                    using var dlg = new ProductPriceHistoryDialog(_id);
                    dlg.ShowDialog(this);
                }, 320, 34);
                AddRow("Fiyat & Stok Tarihçesi", btnHistory, 42);
            }
            catch { }
        }
        else
        {
            // Yeni ürün için çakışmayan otomatik kod üret
            _txtCode.Text = GenerateNextProductCode();
        }

        CalculateSalePrice(null, EventArgs.Empty);
    }

    private static string GenerateNextProductCode()
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
                    if (long.TryParse(numPart, out long n) && n > maxNum)
                    {
                        maxNum = n;
                    }
                }
            }

            long candidateNum = Math.Max(maxNum + 1, 1);
            while (true)
            {
                string candidate = $"URN-{candidateNum:D4}";
                var exists = Database.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Code = @c", ("@c", candidate));
                if (Convert.ToInt32(exists) == 0)
                {
                    return candidate;
                }
                candidateNum++;
            }
        }
        catch
        {
            return "URN-0001";
        }
    }

    private void CalculateSalePrice(object? sender, EventArgs e)
    {
        double p = ParseNumber(_txtPurchasePrice.Text);
        double d = ParseNumber(_txtDiscount.Text);
        double v = ParseNumber(_txtVat.Text);
        double calc = p * (1 - d / 100.0) * (1 + v / 100.0);
        _lblCalculatedPrice.Text = $"{calc:N2} ₺ (Alış - İskonto + KDV)";
    }

    private void LoadData()
    {
        var q = Database.Query("SELECT Code, Barcode, Name, Category, Unit, OpeningStock, PurchasePrice, SalePrice, WholesalePrice, SpecialPrice, DiscountPercent, VatPercent, MinStockLevel, ExpiryDate, BatchNumber, Features FROM Products WHERE Id=$id", ("$id", _id));
        if (q.Rows.Count == 0) return;
        var r = q.Rows[0];
        _txtCode.Text = r["Code"]?.ToString() ?? "";
        _txtBarcode.Text = r["Barcode"]?.ToString() ?? "";
        _txtName.Text = r["Name"]?.ToString() ?? "";
        _cmbFeatures.Text = r.Table.Columns.Contains("Features") ? r["Features"]?.ToString() ?? "" : "";
        _cmbCategory.Text = r["Category"]?.ToString() ?? "Genel";
        _cmbUnit.Text = r["Unit"]?.ToString() ?? "Adet";
        _txtOpeningStock.Text = Convert.ToDouble(r["OpeningStock"]).ToString("N2");
        _txtPurchasePrice.Text = Convert.ToDouble(r["PurchasePrice"]).ToString("N2");
        _txtSalePrice.Text = Convert.ToDouble(r["SalePrice"]).ToString("N2");
        _txtWholesalePrice.Text = (r["WholesalePrice"] != DBNull.Value ? Convert.ToDouble(r["WholesalePrice"]) : 0).ToString("N2");
        _txtSpecialPrice.Text = (r["SpecialPrice"] != DBNull.Value ? Convert.ToDouble(r["SpecialPrice"]) : 0).ToString("N2");
        _txtDiscount.Text = Convert.ToDouble(r["DiscountPercent"]).ToString("N2");
        _txtVat.Text = Convert.ToDouble(r["VatPercent"]).ToString("N2");
        _txtMinStock.Text = Convert.ToDouble(r["MinStockLevel"]).ToString("N2");

        string? exp = r["ExpiryDate"]?.ToString();
        if (!string.IsNullOrWhiteSpace(exp) && DateTime.TryParse(exp, out var expDt))
        {
            _dtpExpiryDate.Checked = true;
            _dtpExpiryDate.Value = expDt;
        }
        else
        {
            _dtpExpiryDate.Checked = false;
        }
        _txtBatchNumber.Text = r["BatchNumber"]?.ToString() ?? "";
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        string code = _txtCode.Text.Trim();
        string name = _txtName.Text.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Lütfen Ürün Kodu ve Ürün Adı alanlarını doldurunuz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // Mükerrer ürün kodu kontrolü
        var existingCount = Database.ExecuteScalar(
            _id < 0
                ? "SELECT COUNT(1) FROM Products WHERE Code = @c"
                : "SELECT COUNT(1) FROM Products WHERE Code = @c AND Id <> @id",
            ("@c", code),
            ("@id", _id)
        );

        if (Convert.ToInt32(existingCount) > 0)
        {
            MessageBox.Show(
                $"'{code}' ürün kodu zaten başka bir üründe kullanılmaktadır.\nLütfen farklı bir ürün kodu giriniz.",
                "Mükerrer Ürün Kodu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            _txtCode.Focus();
            _txtCode.SelectAll();
            DialogResult = DialogResult.None;
            return;
        }

        // Mükerrer barkod kontrolü (girilmişse)
        string barcode = _txtBarcode.Text.Trim();
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var barCount = Database.ExecuteScalar(
                _id < 0
                    ? "SELECT COUNT(1) FROM Products WHERE Barcode = @b"
                    : "SELECT COUNT(1) FROM Products WHERE Barcode = @b AND Id <> @id",
                ("@b", barcode),
                ("@id", _id)
            );

            if (Convert.ToInt32(barCount) > 0)
            {
                MessageBox.Show(
                    $"'{barcode}' barkod numarası zaten başka bir üründe kayıtlıdır.",
                    "Mükerrer Barkod",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                _txtBarcode.Focus();
                _txtBarcode.SelectAll();
                DialogResult = DialogResult.None;
                return;
            }
        }

        try
        {
            string? expiryStr = _dtpExpiryDate.Checked ? _dtpExpiryDate.Value.ToString("yyyy-MM-dd") : null;
            string? batchStr = string.IsNullOrWhiteSpace(_txtBatchNumber.Text) ? null : _txtBatchNumber.Text.Trim();
            string? featuresStr = string.IsNullOrWhiteSpace(_cmbFeatures.Text) ? null : _cmbFeatures.Text.Trim();

            if (_id < 0)
            {
                Database.Execute(@"
INSERT INTO Products(Code, Barcode, Name, Category, Unit, OpeningStock, PurchasePrice, SalePrice, WholesalePrice, SpecialPrice, DiscountPercent, VatPercent, MinStockLevel, ExpiryDate, BatchNumber, Features) 
VALUES($c, $b, $n, $cat, $u, $o, $p, $sp, $wp, $spp, $d, $v, $min, $exp, $batch, $feat)",
                    ("$c", code),
                    ("$b", _txtBarcode.Text.Trim()),
                    ("$n", _txtName.Text.Trim()),
                    ("$cat", string.IsNullOrWhiteSpace(_cmbCategory.Text) ? "Genel" : _cmbCategory.Text.Trim()),
                    ("$u", _cmbUnit.Text.Trim()),
                    ("$o", ParseNumber(_txtOpeningStock.Text)),
                    ("$p", ParseNumber(_txtPurchasePrice.Text)),
                    ("$sp", ParseNumber(_txtSalePrice.Text)),
                    ("$wp", ParseNumber(_txtWholesalePrice.Text)),
                    ("$spp", ParseNumber(_txtSpecialPrice.Text)),
                    ("$d", ParseNumber(_txtDiscount.Text)),
                    ("$v", ParseNumber(_txtVat.Text)),
                    ("$min", ParseNumber(_txtMinStock.Text)),
                    ("$exp", (object?)expiryStr ?? DBNull.Value),
                    ("$batch", (object?)batchStr ?? DBNull.Value),
                    ("$feat", (object?)featuresStr ?? DBNull.Value));
            }
            else
            {
                Database.Execute(@"
UPDATE Products SET 
    Code=$c, 
    Barcode=$b, 
    Name=$n, 
    Category=$cat, 
    Unit=$u, 
    OpeningStock=$o, 
    PurchasePrice=$p, 
    SalePrice=$sp, 
    WholesalePrice=$wp, 
    SpecialPrice=$spp, 
    DiscountPercent=$d, 
    VatPercent=$v, 
    MinStockLevel=$min,
    ExpiryDate=$exp,
    BatchNumber=$batch,
    Features=$feat 
WHERE Id=$id",
                    ("$c", _txtCode.Text.Trim()),
                    ("$b", _txtBarcode.Text.Trim()),
                    ("$n", _txtName.Text.Trim()),
                    ("$cat", string.IsNullOrWhiteSpace(_cmbCategory.Text) ? "Genel" : _cmbCategory.Text.Trim()),
                    ("$u", _cmbUnit.Text.Trim()),
                    ("$o", ParseNumber(_txtOpeningStock.Text)),
                    ("$p", ParseNumber(_txtPurchasePrice.Text)),
                    ("$sp", ParseNumber(_txtSalePrice.Text)),
                    ("$wp", ParseNumber(_txtWholesalePrice.Text)),
                    ("$spp", ParseNumber(_txtSpecialPrice.Text)),
                    ("$d", ParseNumber(_txtDiscount.Text)),
                    ("$v", ParseNumber(_txtVat.Text)),
                    ("$min", ParseNumber(_txtMinStock.Text)),
                    ("$exp", (object?)expiryStr ?? DBNull.Value),
                    ("$batch", (object?)batchStr ?? DBNull.Value),
                    ("$feat", (object?)featuresStr ?? DBNull.Value),
                    ("$id", _id));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }
}
