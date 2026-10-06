using System.Data;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class QuickSaleDialog : BaseModernForm
{
    private readonly KryptonDateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly KryptonComboBox _cmbOperation = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbProduct = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonComboBox _cmbVariant = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonTextBox _txtStockInfo = new() { ReadOnly = true };
    private readonly KryptonTextBox _txtQty = new() { Text = "1" };
    private readonly KryptonTextBox _txtUnitPrice = new() { Text = "0,00" };
    private readonly KryptonTextBox _txtDiscount = new() { Text = "0" };
    private readonly KryptonTextBox _txtVat = new() { Text = "20" };
    private readonly KryptonLabel _lblNetTotal = new() { Text = "0,00 ₺" };
    private readonly KryptonComboBox _cmbPayment = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly KryptonCheckBox _chkHasDueDate = new() { Text = "Vade / Söz Tarihi:", AutoSize = true };
    private readonly KryptonDateTimePicker _dtpDueDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(7), Enabled = false, Width = 130 };
    private readonly KryptonTextBox _txtDocNo = new();
    private readonly KryptonComboBox _cmbNote = new() { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly Label _lblBlacklistWarning = new()
    {
        Text = "⚠️ DİKKAT: Seçili Cari KARA LİSTEYE alınmıştır!",
        ForeColor = Color.Firebrick,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Visible = false,
        AutoSize = true
    };

    private readonly KryptonTextBox _txtBarcodeScan = new() 
    { 
        CueHint = { CueHintText = "Barkod okutun veya yazıp Enter'a basın (F2)..." },
        Font = new Font("Segoe UI", 10f)
    };
    private readonly Label _lblBarcodeInfo = new() 
    { 
        Text = "💡 Normal barkod, terazi veya renk/beden varyant barkodları otomatik algılanır.", 
        ForeColor = UITheme.TextMuted, 
        Font = UITheme.SmallFont, 
        AutoSize = true 
    };
    private readonly KryptonTextBox _txtProductFilter = new() 
    { 
        CueHint = { CueHintText = "🔍 Ürün Adı, Barkod veya Stok Kodu yazarak hızlı arayın..." },
        Font = new Font("Segoe UI", 10f)
    };

    // Parçalı Ödeme Dağıtımı Değerleri
    private double _splitCash = 0;
    private double _splitCard = 0;
    private double _splitTransfer = 0;
    private double _splitCredit = 0;
    private bool _isSplitConfigured = false;

    // Fiş / Sepet Bekletme Butonları
    private KryptonButton? _btnParkCart;
    private KryptonButton? _btnOpenParked;

    public QuickSaleDialog(string initialOperation = "Satış", long? initialProductId = null) 
        : base(initialOperation == "Satış" ? "⚡ Hızlı Satış / Fiş Kesme" : "📥 Hızlı Alış / Fatura Girişi", 740, 840)
    {
        _cmbOperation.Items.AddRange(new object[] { "Satış", "Alış" });
        _cmbOperation.SelectedItem = initialOperation;

        // Depolar
        var warehouses = WarehouseService.GetActiveWarehouses();
        _cmbWarehouse.DataSource = warehouses;
        _cmbWarehouse.DisplayMember = "Name";
        _cmbWarehouse.ValueMember = "Id";

        // Cariler (Fiyat Grubu ve Sabit İskonto ile)
        var dtAccounts = Database.Query(@"
SELECT 0 AS Id, '(Perakende / Peşin Müşteri)' AS Display, 0 AS IsBlacklisted, 'Perakende' AS PriceGroup, 0.0 AS DefaultDiscountPercent, '' AS Phone
UNION ALL 
SELECT Id, Name + ' (' + Type + ')' + CASE WHEN COALESCE(IsBlacklisted, 0) = 1 THEN ' [⛔ KARA LİSTE]' ELSE '' END AS Display, 
       COALESCE(IsBlacklisted, 0) AS IsBlacklisted,
       COALESCE(PriceGroup, 'Perakende') AS PriceGroup,
       COALESCE(DefaultDiscountPercent, 0) AS DefaultDiscountPercent,
       COALESCE(Phone, '') AS Phone
FROM Accounts 
WHERE IsActive=1 
ORDER BY Display");
        _cmbAccount.DataSource = dtAccounts;
        _cmbAccount.DisplayMember = "Display";
        _cmbAccount.ValueMember = "Id";
        _cmbAccount.SelectedIndexChanged += (s, e) =>
        {
            UpdateBlacklistWarning();
            ProductSelectionChanged(null, EventArgs.Empty);
        };

        // Ürünler (Code ve Barcode alanları dahil)
        var dtProducts = Database.Query(@"
SELECT 
    p.Id, 
    p.Code,
    COALESCE(p.Barcode, '') AS Barcode,
    p.Code + ' - ' + p.Name AS Display, 
    p.Name,
    p.PurchasePrice, 
    p.SalePrice,
    COALESCE(p.WholesalePrice, 0) AS WholesalePrice,
    COALESCE(p.SpecialPrice, 0) AS SpecialPrice,
    p.VatPercent,
    p.DiscountPercent,
    (p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) AS CurrentStock,
    p.Unit
FROM Products p 
WHERE p.IsActive = 1 
ORDER BY p.Name");
        _cmbProduct.DataSource = dtProducts;
        _cmbProduct.DisplayMember = "Display";
        _cmbProduct.ValueMember = "Id";

        // Dinamik Ürün Filtreleme Kutusu Olayı (Yazıldıkça anında süzer ve seçer)
        _txtProductFilter.TextChanged += (s, e) =>
        {
            string q = _txtProductFilter.Text.Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                dtProducts.DefaultView.RowFilter = "";
            }
            else
            {
                string safeQ = q.Replace("'", "''");
                dtProducts.DefaultView.RowFilter = $"Display LIKE '%{safeQ}%' OR Barcode LIKE '%{safeQ}%' OR Code LIKE '%{safeQ}%'";
                if (dtProducts.DefaultView.Count > 0)
                {
                    _cmbProduct.SelectedValue = dtProducts.DefaultView[0]["Id"];
                }
            }
        };

        // Ödeme Yöntemleri (Parçalı Ödeme Dahil)
        _cmbPayment.Items.AddRange(new object[] { "Veresiye (Açık Hesap)", "Nakit", "Kredi Kartı", "Havale / EFT", "💳 Parçalı / Çoklu Ödeme" });
        _cmbPayment.SelectedIndex = 0;
        _cmbPayment.SelectedIndexChanged += (s, e) =>
        {
            if (_cmbPayment.Text == "Veresiye (Açık Hesap)")
            {
                _chkHasDueDate.Checked = true;
            }
            else if (_cmbPayment.Text == "💳 Parçalı / Çoklu Ödeme")
            {
                OpenSplitPaymentDialog();
            }
        };

        // Vade paneli
        var pnlDue = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0) };
        _chkHasDueDate.CheckedChanged += (s, e) => _dtpDueDate.Enabled = _chkHasDueDate.Checked;
        pnlDue.Controls.Add(_chkHasDueDate);
        pnlDue.Controls.Add(_dtpDueDate);

        // Akıllı Açıklama Hafızası
        try
        {
            var notes = TransactionService.GetFrequentDescriptions();
            foreach (var note in notes) _cmbNote.Items.Add(note);
        }
        catch { }

        // Barkod Panel Container (54px ferah yükseklik)
        var pnlBarcode = new Panel { Height = 50, Dock = DockStyle.Fill };
        _txtBarcodeScan.Dock = DockStyle.Top;
        _lblBarcodeInfo.Dock = DockStyle.Bottom;
        pnlBarcode.Controls.Add(_txtBarcodeScan);
        pnlBarcode.Controls.Add(_lblBarcodeInfo);

        // Form Satırlarını Ferah ve Düzgün Aralıklarla Ekle
        AddRow("İşlem Türü", _cmbOperation, 40);
        AddRow("Tarih", _dtpDate, 40);
        AddRow("Depo / Şube (*)", _cmbWarehouse, 40);
        AddRow("Cari Firma / Müşteri", _cmbAccount, 40);
        AddRow("Güvenlik Uyarısı", _lblBlacklistWarning, 32);
        AddRow("Barkod / Terazi (F2)", pnlBarcode, 54);
        AddRow("🔍 Hızlı Ürün Ara", _txtProductFilter, 42);
        AddRow("Seçili Ürün (*)", _cmbProduct, 42);
        AddRow("🎨 Varyant (Renk/Beden)", _cmbVariant, 38);
        AddRow("Mevcut Stok Durumu", _txtStockInfo, 40);
        AddRow("Miktar (*)", _txtQty, 40);
        AddRow("Birim Fiyat (₺)", _txtUnitPrice, 40);
        AddRow("İskonto %", _txtDiscount, 40);
        AddRow("KDV Oranı %", _txtVat, 40);
        AddRow("Toplam Net Tutar", _lblNetTotal, 42);
        AddRow("Ödeme Yöntemi", _cmbPayment, 40);
        AddRow("Vade / Söz Tarihi", pnlDue, 40);
        AddRow("Fiş / Fatura No", _txtDocNo, 40);
        AddRow("Açıklama / Not", _cmbNote, 40);

        // Önceden seçili ürün aktarılmışsa uygula
        if (initialProductId.HasValue && initialProductId.Value > 0)
        {
            try
            {
                _cmbProduct.SelectedValue = initialProductId.Value;
                ProductSelectionChanged(null, EventArgs.Empty);
            }
            catch { }
        }

        // Barkod Okuyucu Olayı (Enter yakalama)
        _txtBarcodeScan.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                ProcessScannedBarcode(_txtBarcodeScan.Text.Trim(), dtProducts);
            }
        };

        // F2 Kısayolu: Barkod kutusuna odaklan
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F2)
            {
                _txtBarcodeScan.Focus();
                _txtBarcodeScan.SelectAll();
                e.Handled = true;
            }
        };

        _cmbProduct.SelectedIndexChanged += ProductSelectionChanged;
        _cmbVariant.SelectedIndexChanged += VariantSelectionChanged;
        _cmbOperation.SelectedIndexChanged += OperationChanged;
        _cmbWarehouse.SelectedIndexChanged += (s, e) => UpdateStockInfoForWarehouse();
        _txtQty.TextChanged += CalculateTotal;
        _txtUnitPrice.TextChanged += CalculateTotal;
        _txtDiscount.TextChanged += CalculateTotal;
        _txtVat.TextChanged += CalculateTotal;

        BtnSave.Text = "⚡ Satışı Onayla";
        BtnSave.Click += SaveClick;

        BuildExtraToolbarButtons();

        if (dtProducts.Rows.Count > 0)
        {
            ProductSelectionChanged(null, EventArgs.Empty);
        }

        // Otomatik Fiş No
        _txtDocNo.Text = (initialOperation == "Satış" ? "SAT-" : "ALS-") + DateTime.Now.ToString("yyyyMMdd-HHmm");
        UpdateBlacklistWarning();
        UpdateParkedButtonBadge();
    }

    private void BuildExtraToolbarButtons()
    {
        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            // 1. Fişi Askıya Al (Park) Butonu
            _btnParkCart = UITheme.CreateKryptonButton("⏸️ Fişi Askıya Al", Color.FromArgb(234, 88, 12), Color.White, (s, e) => ParkCurrentSale(), 130, 36);
            _btnParkCart.Location = new Point(14, 12);
            actionPanel.Controls.Add(_btnParkCart);

            // 2. Bekleyen Fişler Butonu
            _btnOpenParked = UITheme.CreateKryptonButton("📂 Bekleyen Fişler (0)", Color.FromArgb(79, 70, 229), Color.White, (s, e) => OpenParkedSalesDialog(), 155, 36);
            _btnOpenParked.Location = new Point(150, 12);
            actionPanel.Controls.Add(_btnOpenParked);

            // 3. WhatsApp Fişi Butonu
            var btnWa = UITheme.CreateKryptonButton("📲 WhatsApp Fişi", Color.FromArgb(16, 185, 129), Color.White, (s, e) => SendWhatsAppReceiptClick(), 140, 36);
            btnWa.Location = new Point(312, 12);
            actionPanel.Controls.Add(btnWa);
        }
    }

    private void UpdateParkedButtonBadge()
    {
        if (_btnOpenParked != null)
        {
            int count = ParkedSalesService.Count;
            _btnOpenParked.Text = $"📂 Bekleyen Fişler ({count})";
            var bg = count > 0 ? Color.FromArgb(79, 70, 229) : Color.FromArgb(148, 163, 184);
            _btnOpenParked.StateCommon.Back.Color1 = bg;
            _btnOpenParked.StateCommon.Back.Color2 = bg;
        }
    }

    private void ParkCurrentSale()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ParkedSales) && !curUser.HasPermission(UserPermissions.QuickSale))
        {
            MessageBox.Show("Fiş bekletme (askıya alma) modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbProduct.SelectedValue == null || ParseNumber(_txtQty.Text) <= 0)
        {
            MessageBox.Show("Askıya almak için geçerli bir ürün ve miktar seçili olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string prodName = (_cmbProduct.SelectedItem is DataRowView rv) ? rv["Display"]?.ToString() ?? "" : "";
        string accName = (_cmbAccount.SelectedItem is DataRowView av) ? av["Display"]?.ToString() ?? "(Perakende)" : "(Perakende)";

        var parked = new ParkedSaleModel
        {
            Operation = _cmbOperation.Text,
            AccountId = Convert.ToInt64(_cmbAccount.SelectedValue ?? 0),
            AccountName = accName,
            WarehouseId = Convert.ToInt64(_cmbWarehouse.SelectedValue ?? 1),
            ProductId = Convert.ToInt64(_cmbProduct.SelectedValue),
            ProductName = prodName,
            Quantity = ParseNumber(_txtQty.Text),
            UnitPrice = ParseNumber(_txtUnitPrice.Text),
            DiscountPercent = ParseNumber(_txtDiscount.Text),
            VatPercent = ParseNumber(_txtVat.Text),
            TotalAmount = ParseNumber(_lblNetTotal.Text),
            PaymentMethod = _cmbPayment.Text,
            DocNo = _txtDocNo.Text.Trim(),
            Note = _cmbNote.Text.Trim(),
            DueDate = _chkHasDueDate.Checked ? _dtpDueDate.Value.ToString("yyyy-MM-dd") : null,
            VariantId = _cmbVariant.SelectedValue is long vid && vid > 0 ? vid : null,
            VariantName = _cmbVariant.Text
        };

        ParkedSalesService.Park(parked);
        UpdateParkedButtonBadge();

        MessageBox.Show($"Sepet askıya alındı!\n\nArkadaki müşterinin satışına devam edebilirsiniz. Bekleyen fişe '📂 Bekleyen Fişler' butonundan istediğiniz zaman tek tıkla geri dönebilirsiniz.", "Sepet Askıya Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Formu yeni müşteri için sıfırla
        _txtQty.Text = "1";
        _txtProductFilter.Text = string.Empty;
        _txtDocNo.Text = (_cmbOperation.Text == "Satış" ? "SAT-" : "ALS-") + DateTime.Now.ToString("yyyyMMdd-HHmm");
        _cmbAccount.SelectedIndex = 0;
        _txtBarcodeScan.Focus();
    }

    private void OpenParkedSalesDialog()
    {
        if (ParkedSalesService.Count == 0)
        {
            MessageBox.Show("Şu anda askıda bekleyen herhangi bir fiş bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new ParkedSalesDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedParkedSale != null)
        {
            var p = dlg.SelectedParkedSale;
            _cmbOperation.SelectedItem = p.Operation;
            _cmbAccount.SelectedValue = p.AccountId;
            _cmbWarehouse.SelectedValue = p.WarehouseId;
            _cmbProduct.SelectedValue = p.ProductId;
            _txtQty.Text = p.Quantity.ToString("N2");
            _txtUnitPrice.Text = p.UnitPrice.ToString("N2");
            _txtDiscount.Text = p.DiscountPercent.ToString("N2");
            _txtVat.Text = p.VatPercent.ToString("N2");
            _cmbPayment.SelectedItem = p.PaymentMethod;
            _txtDocNo.Text = p.DocNo;
            _cmbNote.Text = p.Note;

            if (!string.IsNullOrWhiteSpace(p.DueDate) && DateTime.TryParse(p.DueDate, out var dtDue))
            {
                _chkHasDueDate.Checked = true;
                _dtpDueDate.Value = dtDue;
            }
            else
            {
                _chkHasDueDate.Checked = false;
            }

            CalculateTotal(null, EventArgs.Empty);
            UpdateParkedButtonBadge();
            MessageBox.Show($"'{p.DisplayText}' fişi kasaya geri yüklendi! Satışa kaldığınız yerden devam edebilirsiniz.", "Fiş Geri Yüklendi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OpenSplitPaymentDialog()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.SplitPayment) && !curUser.HasPermission(UserPermissions.QuickSale))
        {
            MessageBox.Show("Parçalı ve çoklu tahsilat yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        double currentTotal = ParseNumber(_lblNetTotal.Text);
        if (currentTotal <= 0)
        {
            MessageBox.Show("Önce ürün ve miktar belirleyiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long accountId = Convert.ToInt64(_cmbAccount.SelectedValue ?? 0);
        bool allowCredit = accountId > 0;

        using var dlg = new SplitPaymentDialog(currentTotal, allowCredit);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _splitCash = dlg.CashAmount;
            _splitCard = dlg.CardAmount;
            _splitTransfer = dlg.TransferAmount;
            _splitCredit = dlg.CreditAmount;
            _isSplitConfigured = true;

            MessageBox.Show($"Parçalı ödeme dağıtıldı:\n• Nakit: {_splitCash:N2} ₺\n• Kredi Kartı: {_splitCard:N2} ₺\n• Havale/EFT: {_splitTransfer:N2} ₺\n• Veresiye: {_splitCredit:N2} ₺", "Dağıtım Onaylandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _isSplitConfigured = false;
            _cmbPayment.SelectedIndex = 1; // Nakit'e geri dön
        }
    }

    private void SendWhatsAppReceiptClick()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.DigitalReceipt) && !curUser.HasPermission(UserPermissions.QuickSale))
        {
            MessageBox.Show("WhatsApp ile dijital fiş gönderme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string phone = string.Empty;
        if (_cmbAccount.SelectedItem is DataRowView rv && rv["Phone"] != DBNull.Value)
        {
            phone = rv["Phone"].ToString() ?? "";
        }

        string customerName = (_cmbAccount.SelectedItem is DataRowView cav) ? cav["Display"]?.ToString() ?? "Perakende Müşteri" : "Perakende Müşteri";
        string productName = (_cmbProduct.SelectedItem is DataRowView pav) ? pav["Display"]?.ToString() ?? "Ürün" : "Ürün";
        double q = ParseNumber(_txtQty.Text);
        double price = ParseNumber(_txtUnitPrice.Text);
        double disc = ParseNumber(_txtDiscount.Text);
        double vat = ParseNumber(_txtVat.Text);
        double total = ParseNumber(_lblNetTotal.Text);
        string payment = _cmbPayment.Text;

        string receipt = ReceiptDigitalService.BuildReceiptText(_txtDocNo.Text.Trim(), _dtpDate.Value, customerName, productName, q, "Adet", price, disc, vat, total, payment, _cmbNote.Text.Trim());

        if (string.IsNullOrWhiteSpace(phone))
        {
            string inputPhone = Microsoft.VisualBasic.Interaction.InputBox("Müşterinin telefon numarasını giriniz (Örn: 0532 123 45 67):", "WhatsApp Dijital Fiş", "");
            phone = inputPhone.Trim();
        }

        try
        {
            ReceiptDigitalService.OpenWhatsApp(phone, receipt);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateBlacklistWarning()
    {
        if (_cmbAccount.SelectedItem is DataRowView rowView)
        {
            bool isBlacklisted = Convert.ToInt32(rowView["IsBlacklisted"]) == 1;
            _lblBlacklistWarning.Visible = isBlacklisted;
        }
        else
        {
            _lblBlacklistWarning.Visible = false;
        }
    }

    private void UpdateStockInfoForWarehouse()
    {
        if (_cmbProduct.SelectedValue == null || _cmbWarehouse.SelectedValue == null) return;
        try
        {
            long pId = Convert.ToInt64(_cmbProduct.SelectedValue);
            long wId = Convert.ToInt64(_cmbWarehouse.SelectedValue);
            double qty = WarehouseService.GetProductStockInWarehouse(pId, wId);
            string unit = (_cmbProduct.SelectedItem is DataRowView rv) ? rv["Unit"]?.ToString() ?? "Adet" : "Adet";
            _txtStockInfo.Text = $"{qty:N2} {unit} ({_cmbWarehouse.Text})";
        }
        catch { }
    }

    private void OperationChanged(object? sender, EventArgs e)
    {
        ProductSelectionChanged(null, EventArgs.Empty);
    }

    private void ProductSelectionChanged(object? sender, EventArgs e)
    {
        if (_cmbProduct.SelectedItem is DataRowView rowView)
        {
            long pId = Convert.ToInt64(rowView["Id"]);
            UpdateStockInfoForWarehouse();
            LoadVariantsForProduct(pId);

            bool isSale = _cmbOperation.Text == "Satış";
            double pPrice = Convert.ToDouble(rowView["PurchasePrice"]);
            double sPrice = Convert.ToDouble(rowView["SalePrice"]);
            double wPrice = Convert.ToDouble(rowView["WholesalePrice"]);
            double spPrice = Convert.ToDouble(rowView["SpecialPrice"]);
            double vat = Convert.ToDouble(rowView["VatPercent"]);
            double disc = Convert.ToDouble(rowView["DiscountPercent"]);

            // Cari Fiyat Grubu ve Sabit İskonto Entegrasyonu
            string priceGroup = "Perakende";
            double customerDiscount = 0;
            if (_cmbAccount.SelectedItem is DataRowView accRow)
            {
                priceGroup = accRow["PriceGroup"]?.ToString() ?? "Perakende";
                customerDiscount = Convert.ToDouble(accRow["DefaultDiscountPercent"]);
            }

            if (isSale)
            {
                double chosenPrice = sPrice > 0 ? sPrice : pPrice;
                if (priceGroup.Equals("Toptan", StringComparison.OrdinalIgnoreCase) && wPrice > 0)
                {
                    chosenPrice = wPrice;
                }
                else if ((priceGroup.Contains("Özel") || priceGroup.Contains("Bayi")) && spPrice > 0)
                {
                    chosenPrice = spPrice;
                }

                _txtUnitPrice.Text = chosenPrice.ToString("N2");
                _txtDiscount.Text = customerDiscount > 0 ? customerDiscount.ToString("N2") : disc.ToString("N2");
            }
            else
            {
                _txtUnitPrice.Text = pPrice.ToString("N2");
                _txtDiscount.Text = disc.ToString("N2");
            }

            _txtVat.Text = vat.ToString("N2");
        }
        CalculateTotal(null, EventArgs.Empty);
    }

    private void LoadVariantsForProduct(long productId)
    {
        _cmbVariant.Items.Clear();
        _cmbVariant.Items.Add(new VariantComboItem { Id = 0, Display = "Standart (Varyantsız)", PriceDiff = 0 });
        try
        {
            var variants = ProductVariantService.GetVariantsByProductId(productId);
            foreach (var v in variants)
            {
                string diffStr = v.PriceDifference > 0 ? $" (+{v.PriceDifference:N2} ₺)" : (v.PriceDifference < 0 ? $" ({v.PriceDifference:N2} ₺)" : "");
                _cmbVariant.Items.Add(new VariantComboItem
                {
                    Id = v.Id,
                    Display = $"{v.VariantName}{diffStr}",
                    PriceDiff = v.PriceDifference
                });
            }
        }
        catch { }
        _cmbVariant.SelectedIndex = 0;
    }

    private void VariantSelectionChanged(object? sender, EventArgs e)
    {
        if (_cmbVariant.SelectedItem is VariantComboItem item && item.Id > 0 && _cmbProduct.SelectedItem is DataRowView rowView)
        {
            double basePrice = Convert.ToDouble(rowView["SalePrice"]);
            _txtUnitPrice.Text = (basePrice + item.PriceDiff).ToString("N2");
        }
        CalculateTotal(null, EventArgs.Empty);
    }

    private class VariantComboItem
    {
        public long Id { get; set; }
        public string Display { get; set; } = string.Empty;
        public double PriceDiff { get; set; }
        public override string ToString() => Display;
    }

    private void CalculateTotal(object? sender, EventArgs e)
    {
        double q = ParseNumber(_txtQty.Text);
        double price = ParseNumber(_txtUnitPrice.Text);
        double disc = ParseNumber(_txtDiscount.Text);
        double vat = ParseNumber(_txtVat.Text);

        double lineTotal = q * price * (1 - disc / 100.0) * (1 + vat / 100.0);
        _lblNetTotal.Text = $"{lineTotal:N2} ₺";
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        if (_cmbProduct.SelectedValue == null || ParseNumber(_txtQty.Text) <= 0)
        {
            MessageBox.Show("Lütfen geçerli bir ürün ve miktar seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        long accountId = Convert.ToInt64(_cmbAccount.SelectedValue);
        string paymentMethod = _cmbPayment.Text;

        if (accountId == 0 && paymentMethod == "Veresiye (Açık Hesap)")
        {
            MessageBox.Show("Veresiye (Açık Hesap) işlemi yapabilmek için lütfen kayıtlı bir Müşteri/Cari seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // Kara Liste Güvenlik Onayı
        if (_lblBlacklistWarning.Visible && paymentMethod == "Veresiye (Açık Hesap)")
        {
            var ask = MessageBox.Show(
                "⚠️ DİKKAT: Bu müşteri [KARA LİSTE]'dedir!\n\nVeresiye (Açık Hesap) satışı onaylamak istediğinize emin misiniz?",
                "Kara Liste Güvenlik Uyarısı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );
            if (ask != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }
        }

        try
        {
            double q = ParseNumber(_txtQty.Text);
            double price = ParseNumber(_txtUnitPrice.Text);
            double disc = ParseNumber(_txtDiscount.Text);
            double vat = ParseNumber(_txtVat.Text);
            double totalAmount = q * price * (1 - disc / 100.0) * (1 + vat / 100.0);
            string? dueDateStr = _chkHasDueDate.Checked ? _dtpDueDate.Value.ToString("yyyy-MM-dd") : null;
            long warehouseId = _cmbWarehouse.SelectedValue != null ? Convert.ToInt64(_cmbWarehouse.SelectedValue) : 1;

            string finalNote = _cmbNote.Text.Trim();
            if (_cmbVariant.SelectedItem is VariantComboItem vi && vi.Id > 0)
            {
                finalNote = $"[Varyant: {vi.Display}] " + finalNote;
            }

            if (paymentMethod == "💳 Parçalı / Çoklu Ödeme")
            {
                if (!_isSplitConfigured || Math.Abs((_splitCash + _splitCard + _splitTransfer + _splitCredit) - totalAmount) > 0.05)
                {
                    OpenSplitPaymentDialog();
                    if (!_isSplitConfigured)
                    {
                        DialogResult = DialogResult.None;
                        return;
                    }
                }

                TransactionService.ProcessIntegratedSaleOrPurchaseSplit(
                    _dtpDate.Value,
                    accountId,
                    Convert.ToInt64(_cmbProduct.SelectedValue),
                    _cmbOperation.Text,
                    q,
                    price,
                    totalAmount,
                    _splitCash,
                    _splitCard,
                    _splitTransfer,
                    _splitCredit,
                    _txtDocNo.Text.Trim(),
                    finalNote,
                    dueDateStr,
                    warehouseId
                );
            }
            else
            {
                TransactionService.ProcessIntegratedSaleOrPurchase(
                    _dtpDate.Value,
                    accountId,
                    Convert.ToInt64(_cmbProduct.SelectedValue),
                    _cmbOperation.Text,
                    q,
                    price,
                    totalAmount,
                    paymentMethod,
                    _txtDocNo.Text.Trim(),
                    finalNote,
                    dueDateStr,
                    warehouseId
                );
            }

            var promptWa = MessageBox.Show(
                "İşlem başarıyla kaydedildi! Stok ve Kasa hareketleri güncellendi.\n\nMüşteriye WhatsApp ile Dijital Fiş göndermek ister misiniz?",
                "Satış Tamamlandı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (promptWa == DialogResult.Yes)
            {
                SendWhatsAppReceiptClick();
            }

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"İşlem sırasında hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.None;
        }
    }

    private void ProcessScannedBarcode(string barcode, DataTable dtProducts)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        // 1. Önce Varyant Barkodu mu kontrol et (Renk/Beden)
        var variantMatch = ProductVariantService.FindByBarcode(barcode);
        if (variantMatch.HasValue)
        {
            _cmbProduct.SelectedValue = variantMatch.Value.ProductId;
            _lblBarcodeInfo.Text = $"✅ Varyant Bulundu: {variantMatch.Value.VariantName} (+{variantMatch.Value.PriceDiff:N2} ₺)";
            _lblBarcodeInfo.ForeColor = Color.DarkGreen;

            // Varyant combo'sunda eşleşeni seç
            for (int i = 0; i < _cmbVariant.Items.Count; i++)
            {
                if (_cmbVariant.Items[i] is VariantComboItem item && item.Id == variantMatch.Value.VariantId)
                {
                    _cmbVariant.SelectedIndex = i;
                    break;
                }
            }

            _txtBarcodeScan.Text = string.Empty;
            _txtBarcodeScan.Focus();
            return;
        }

        var parsed = BarcodeScaleHelper.Parse(barcode);
        DataRow? matchedRow = null;

        // 2. Terazi Barkodu ise: PLU koduna göre ara
        if (parsed.IsScaleBarcode)
        {
            foreach (DataRow row in dtProducts.Rows)
            {
                string code = row["Code"]?.ToString()?.Trim() ?? "";
                string pBarcode = row["Barcode"]?.ToString()?.Trim() ?? "";

                if (code.Equals(parsed.ProductCode, StringComparison.OrdinalIgnoreCase) ||
                    code.Equals(parsed.CleanProductCode, StringComparison.OrdinalIgnoreCase) ||
                    pBarcode.Equals(parsed.ProductCode, StringComparison.OrdinalIgnoreCase) ||
                    pBarcode.Equals(parsed.CleanProductCode, StringComparison.OrdinalIgnoreCase) ||
                    (parsed.RawBarcode.Length >= 7 && pBarcode.Equals(parsed.RawBarcode.Substring(0, 7), StringComparison.OrdinalIgnoreCase)))
                {
                    matchedRow = row;
                    break;
                }
            }
        }
        else
        {
            // Normal Barkod ise: Doğrudan Barcode veya Code eşleşmesi
            foreach (DataRow row in dtProducts.Rows)
            {
                string code = row["Code"]?.ToString()?.Trim() ?? "";
                string pBarcode = row["Barcode"]?.ToString()?.Trim() ?? "";
                if (pBarcode.Equals(barcode, StringComparison.OrdinalIgnoreCase) ||
                    code.Equals(barcode, StringComparison.OrdinalIgnoreCase))
                {
                    matchedRow = row;
                    break;
                }
            }
        }

        if (matchedRow == null)
        {
            _lblBarcodeInfo.Text = $"❌ Ürün bulunamadı! (Okunan Barkod/PLU: {barcode})";
            _lblBarcodeInfo.ForeColor = Color.Firebrick;
            try { System.Media.SystemSounds.Beep.Play(); } catch { }
            _txtBarcodeScan.SelectAll();
            return;
        }

        // Ürünü seç
        _cmbProduct.SelectedValue = matchedRow["Id"];
        string productName = matchedRow["Display"]?.ToString() ?? "";

        // Terazi Gramajı veya Tutarı Çözümleme
        if (parsed.BarcodeType == ScaleBarcodeType.EmbeddedWeight)
        {
            _txtQty.Text = parsed.WeightKg.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture);
            _lblBarcodeInfo.Text = $"⚖️ Tartılı Barkod (28): {parsed.WeightKg:N3} kg çözüldü. ({productName})";
            _lblBarcodeInfo.ForeColor = Color.DarkGreen;
        }
        else if (parsed.BarcodeType == ScaleBarcodeType.EmbeddedPrice)
        {
            double unitPrice = ParseNumber(_txtUnitPrice.Text);
            if (unitPrice > 0)
            {
                double calculatedWeight = parsed.TotalPrice / unitPrice;
                _txtQty.Text = calculatedWeight.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture);
                _lblBarcodeInfo.Text = $"⚖️ Tutarlı Barkod (27): {parsed.TotalPrice:N2} ₺ ({calculatedWeight:N3} kg) çözüldü. ({productName})";
            }
            else
            {
                _txtQty.Text = "1";
                _txtUnitPrice.Text = parsed.TotalPrice.ToString("N2");
                _lblBarcodeInfo.Text = $"⚖️ Tutarlı Barkod (27): {parsed.TotalPrice:N2} ₺ olarak aktarıldı. ({productName})";
            }
            _lblBarcodeInfo.ForeColor = Color.DarkGreen;
        }
        else
        {
            _txtQty.Text = "1";
            _lblBarcodeInfo.Text = $"✅ Ürün seçildi: {productName}";
            _lblBarcodeInfo.ForeColor = Color.DarkGreen;
        }

        CalculateTotal(null, EventArgs.Empty);
        _txtBarcodeScan.Text = string.Empty;
        _txtBarcodeScan.Focus();
    }
}
