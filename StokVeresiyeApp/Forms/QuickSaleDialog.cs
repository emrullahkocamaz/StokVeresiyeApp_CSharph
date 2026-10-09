using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class QuickSaleDialog : BaseModernForm
{
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short };
    private readonly ComboBox _cmbOperation = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbProduct = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbVariant = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtStockInfo = new() { ReadOnly = true };
    private readonly TextBox _txtQty = new() { Text = "1" };
    private readonly TextBox _txtUnitPrice = new() { Text = "0,00" };
    private readonly TextBox _txtDiscount = new() { Text = "0" };
    private readonly TextBox _txtVat = new() { Text = "20" };
    private readonly Label _lblNetTotal = new() { Text = "0,00 ₺" };
    private readonly ComboBox _cmbPayment = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _chkHasDueDate = new() { Text = "Vade / Söz Tarihi:", AutoSize = true };
    private readonly DateTimePicker _dtpDueDate = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(7), Enabled = false, Width = 130 };
    private readonly TextBox _txtDocNo = new();
    private readonly ComboBox _cmbNote = new() { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly Label _lblBlacklistWarning = new()
    {
        Text = "⚠️ DİKKAT: Seçili Cari KARA LİSTEYE alınmıştır!",
        ForeColor = Color.Firebrick,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        Visible = false,
        AutoSize = true
    };
    protected Label _lblTouchTitle = new()
    {
        Text = "⚡ Hızlı Satış Butonları (POS)",
        Font = new Font("Segoe UI", 11f, FontStyle.Bold),
        ForeColor = Color.FromArgb(30, 41, 59),
        Dock = DockStyle.Top,
        Height = 26
    };

    private readonly TextBox _txtBarcodeScan = new() 
    { 
        PlaceholderText = "Barkod okutun veya yazıp Enter'a basın (F2)...",
        Font = new Font("Segoe UI", 10f)
    };
    private readonly Label _lblBarcodeInfo = new() 
    { 
        Text = "💡 Normal barkod, terazi veya renk/beden varyant barkodları otomatik algılanır.", 
        ForeColor = UITheme.TextMuted, 
        Font = UITheme.SmallFont, 
        AutoSize = true 
    };
    private readonly TextBox _txtProductFilter = new() 
    { 
        PlaceholderText = "🔍 Ürün Adı, Barkod veya Stok Kodu yazarak hızlı arayın...",
        Font = new Font("Segoe UI", 10f)
    };

    // Parçalı Ödeme Dağıtımı Değerleri
    private double _splitCash = 0;
    private double _splitCard = 0;
    private double _splitTransfer = 0;
    private double _splitCredit = 0;
    private bool _isSplitConfigured = false;

    // Fiş / Sepet Bekletme Butonları
    private Button? _btnParkCart;
    private Button? _btnOpenParked;
    private readonly List<BulkSaleLine> _bulkSaleLines = new();
    private DataGridView? _gridBulkSale;

    private sealed class BulkSaleLine
    {
        public long ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double DiscountPercent { get; set; }
        public double VatPercent { get; set; }
        public double TotalAmount => Quantity * UnitPrice * (1 - DiscountPercent / 100.0) * (1 + VatPercent / 100.0);
    }

    // Dokunmatik Kasa & Hızlı Buton Paneli
    protected Panel? _pnlTouch;
    protected FlowLayoutPanel? _flowTouchButtons;
    private TextBox? _txtTouchFilter;
    private DataTable? _cachedProductsTable;
    private Label? _lblBulkTotal;

    public QuickSaleDialog(string initialOperation = "Satış", long? initialProductId = null) 
        : base(initialOperation == "Satış" ? "⚡ Hızlı Satış / POS Dokunmatik Kasa" : "📥 Hızlı Alış / Fatura Girişi", 1240, 840)
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

        // Ürünler (Code, Barcode, Category ve IsFastSale alanları dahil)
        var dtProducts = Database.Query(@"
SELECT 
    p.Id, 
    p.Code,
    COALESCE(p.Barcode, '') AS Barcode,
    p.Code + ' - ' + p.Name AS Display, 
    p.Name,
    p.Category,
    p.PurchasePrice, 
    p.SalePrice,
    COALESCE(p.WholesalePrice, 0) AS WholesalePrice,
    COALESCE(p.SpecialPrice, 0) AS SpecialPrice,
    p.VatPercent,
    p.DiscountPercent,
    COALESCE(p.IsFastSale, 0) AS IsFastSale,
    ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) AS CurrentStock,
    p.Unit
FROM Products p 
WHERE p.IsActive = 1 
ORDER BY p.Name");
        _cachedProductsTable = dtProducts;
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
        BuildTouchButtonsPanel(dtProducts);
        UpdateOperationUi();

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
            actionPanel.Padding = new Padding(12, 12, 12, 12);
            actionPanel.Height = 65;

            // Sol taraftaki işlem butonları için FlowLayoutPanel (asla sağdaki butonlarla çakışmaz)
            var flowLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            // 1. Fişi Askıya Al (Park) Butonu
            _btnParkCart = UITheme.CreateButton("⏸️ Askıya Al", Color.FromArgb(234, 88, 12), Color.White, (s, e) => ParkCurrentSale(), 110, 36);
            _btnParkCart.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(_btnParkCart);

            // 2. Bekleyen Fişler Butonu
            _btnOpenParked = UITheme.CreateButton("📂 Bekleyen (0)", Color.FromArgb(79, 70, 229), Color.White, (s, e) => OpenParkedSalesDialog(), 125, 36);
            _btnOpenParked.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(_btnOpenParked);

            // 3. FAST / Karekod IBAN ile Ödeme Butonu
            var btnFastQr = UITheme.CreateButton("⚡ FAST QR", Color.FromArgb(124, 58, 237), Color.White, (s, e) => OpenFastQrPayment(), 115, 36);
            btnFastQr.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(btnFastQr);

            // 4. WhatsApp Fişi Butonu
            var btnWa = UITheme.CreateButton("📲 WhatsApp", Color.FromArgb(16, 185, 129), Color.White, (s, e) => SendWhatsAppReceiptClick(), 110, 36);
            btnWa.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(btnWa);

            // 5. Çift Ekran / Müşteri Bilgi Ekranı Butonu
            var btnDisplay = UITheme.CreateButton("📺 Müşteri Ekranı", Color.FromArgb(14, 165, 233), Color.White, (s, e) =>
            {
                var curUser = UserService.CurrentUser;
                if (curUser != null && !curUser.HasPermission(UserPermissions.CustomerDisplay) && !curUser.HasPermission(UserPermissions.QuickSale))
                {
                    MessageBox.Show("Müşteri ekranını açma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                CustomerDisplayForm.ShowOrToggle();
                CalculateTotal(null, EventArgs.Empty);
            }, 125, 36);
            btnDisplay.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(btnDisplay);

            // 6. Termal Fiş Yazdır Butonu
            var btnThermal = UITheme.CreateButton("🖨️ Fiş Yazdır", Color.FromArgb(71, 85, 105), Color.White, (s, e) => PrintThermalReceiptClick(), 115, 36);
            btnThermal.Margin = new Padding(0, 0, 6, 0);
            flowLeft.Controls.Add(btnThermal);

            actionPanel.Controls.Add(flowLeft);
            flowLeft.BringToFront();
        }
    }

    private void UpdateParkedButtonBadge()
    {
        if (_btnOpenParked != null)
        {
            int count = ParkedSalesService.Count;
            _btnOpenParked.Text = $"📂 Bekleyen ({count})";
            var bg = count > 0 ? Color.FromArgb(79, 70, 229) : Color.FromArgb(148, 163, 184);
            _btnOpenParked.BackColor = bg;
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

        string defaultTag = "Masa " + (ParkedSalesService.Count + 1);
        string enteredTag = PromptDialog.Show(
            "Askıya alınan bu satış için Masa No, Araç Plakası veya Müşteri Adı giriniz:\n(Örn: Masa 4, 34 ABC 123, Paket Servis, Ahmet Bey)",
            "Sepeti Askıya Al (İsim / Masa / Plaka)",
            defaultTag
        );

        string finalTag = string.IsNullOrWhiteSpace(enteredTag) ? defaultTag : enteredTag.Trim();

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
            VariantName = _cmbVariant.Text,
            CustomTag = finalTag
        };

        ParkedSalesService.Park(parked);
        UpdateParkedButtonBadge();

        MessageBox.Show($"Sepet [{finalTag}] etiketiyle askıya alındı!\n\nArkadaki müşterinin satışına devam edebilirsiniz. Bekleyen fişe '📂 Bekleyen Fişler' butonundan dilediğiniz an geri dönebilirsiniz.", "Sepet Askıya Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);

        // Formu yeni müşteri için sıfırla
        _txtQty.Text = "1";
        _txtProductFilter.Text = string.Empty;
        _txtDocNo.Text = (_cmbOperation.Text == "Satış" ? "SAT-" : "ALS-") + DateTime.Now.ToString("yyyyMMdd-HHmm");
        _cmbAccount.SelectedIndex = 0;
        _txtBarcodeScan.Focus();
    }

    private void OpenFastQrPayment()
    {
        double currentTotal = ParseNumber(_lblNetTotal.Text);
        if (currentTotal <= 0)
        {
            MessageBox.Show("FAST / QR Kod ile ödeme alabilmek için önce geçerli bir ürün ve tutar giriniz.", "Tutar Geçersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string docNo = _txtDocNo.Text.Trim();
        using var dlg = new FastQrPaymentDialog(currentTotal, docNo);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _cmbPayment.Text = "Havale / EFT";
            string qrNote = $"[⚡ FAST QR Ödeme - {currentTotal:N2} ₺]";
            if (string.IsNullOrWhiteSpace(_cmbNote.Text))
            {
                _cmbNote.Text = qrNote;
            }
            else if (!_cmbNote.Text.Contains("FAST QR"))
            {
                _cmbNote.Text += " " + qrNote;
            }

            var ask = MessageBox.Show(
                $"Karekod ile {currentTotal:N2} ₺ tutarındaki ödeme hesaba teyit edildi!\n\nSatışı şimdi kaydetmek istiyor musunuz?",
                "Ödeme Onaylandı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (ask == DialogResult.Yes)
            {
                SaveClick(null, EventArgs.Empty);
            }
        }
    }

    private void BuildTouchButtonsPanel(DataTable dtProducts)
    {
        _pnlTouch = new Panel
        {
            Dock = DockStyle.Right,
            Width = 330,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(12)
        };

        // Üst Başlık & Yönetim Barı
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 75, BackColor = Color.Transparent };

        var pnlSearchRow = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(0, 4, 0, 0) };

        _txtTouchFilter = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "🔍 Butonlarda ara...",
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtTouchFilter.TextChanged += (s, e) => LoadTouchButtons(_txtTouchFilter.Text.Trim());

        var btnManage = UITheme.CreateButton("⚙️ Düzenle", Color.FromArgb(226, 232, 240), Color.FromArgb(51, 65, 85), (s, e) => OpenManageFastSaleProducts(), 85, 32);
        btnManage.Dock = DockStyle.Right;
        btnManage.Margin = new Padding(6, 0, 0, 0);

        pnlSearchRow.Controls.Add(_txtTouchFilter);
        pnlSearchRow.Controls.Add(btnManage);

        pnlHeader.Controls.Add(_lblTouchTitle);
        pnlHeader.Controls.Add(pnlSearchRow);

        _flowTouchButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 10, 0, 0),
            BackColor = Color.Transparent
        };

        _gridBulkSale = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _gridBulkSale.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Ürün", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 90 });
        _gridBulkSale.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Adet", DataPropertyName = "Qty", Width = 48 });
        _gridBulkSale.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Birim", DataPropertyName = "UnitPrice", Width = 68 });
        _gridBulkSale.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Toplam", DataPropertyName = "Total", Width = 75 });

        var btnAddToBulk = UITheme.CreateButton("➕ Seçilenleri Ekle", UITheme.Primary, Color.White, (s, e) => AddCurrentProductToBulk(), 120, 30);
        var btnClearBulk = UITheme.CreateButton("🗑️ Temizle", Color.FromArgb(148, 163, 184), Color.White, (s, e) => ClearBulkSelection(), 90, 30);
        btnAddToBulk.Dock = DockStyle.Left;
        btnClearBulk.Dock = DockStyle.Right;

        _lblBulkTotal = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "Sepet Toplamı: 0,00 ₺",
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary
        };
        var pnlBulkActions = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(0, 4, 0, 0) };
        pnlBulkActions.Controls.Add(btnAddToBulk);
        pnlBulkActions.Controls.Add(btnClearBulk);
        var pnlBulkFooter = new Panel { Dock = DockStyle.Bottom, Height = 62 };
        pnlBulkFooter.Controls.Add(_lblBulkTotal);
        pnlBulkFooter.Controls.Add(pnlBulkActions);
        var pnlBulkCart = new Panel { Dock = DockStyle.Bottom, Height = 150 };
        pnlBulkCart.Controls.Add(_gridBulkSale);
        pnlBulkCart.Controls.Add(pnlBulkFooter);

        _pnlTouch.Controls.Add(_flowTouchButtons);
        _pnlTouch.Controls.Add(pnlHeader);
        _pnlTouch.Controls.Add(pnlBulkCart);
        pnlBulkCart.BringToFront();

        Controls.Add(_pnlTouch);
        _pnlTouch.BringToFront();

        RefreshBulkGrid();

        LoadTouchButtons();
    }

    private void AddCurrentProductToBulk()
    {
        if (_cmbProduct.SelectedValue == null)
        {
            return;
        }

        long productId = Convert.ToInt64(_cmbProduct.SelectedValue);
        string displayName = (_cmbProduct.SelectedItem is DataRowView row) ? (row["Name"]?.ToString() ?? "Ürün") : "Ürün";
        double qty = ParseNumber(_txtQty.Text);
        if (qty <= 0) return;

        double unitPrice = ParseNumber(_txtUnitPrice.Text);
        double discountPct = ParseNumber(_txtDiscount.Text);
        double vatPct = ParseNumber(_txtVat.Text);
        var existing = _bulkSaleLines.FirstOrDefault(x => x.ProductId == productId);
        if (existing != null)
        {
            existing.Quantity += qty;
            existing.UnitPrice = unitPrice;
            existing.DiscountPercent = discountPct;
            existing.VatPercent = vatPct;
        }
        else
        {
            _bulkSaleLines.Add(new BulkSaleLine
            {
                ProductId = productId,
                Name = displayName,
                Quantity = qty,
                UnitPrice = unitPrice,
                DiscountPercent = discountPct,
                VatPercent = vatPct
            });
        }

        RefreshBulkGrid();
    }

    private void ClearBulkSelection()
    {
        _bulkSaleLines.Clear();
        RefreshBulkGrid();
    }

    private void RefreshBulkGrid()
    {
        if (_gridBulkSale == null) return;

        var rows = _bulkSaleLines.Select(x => new
        {
            Name = x.Name,
            Qty = x.Quantity.ToString("N2"),
            UnitPrice = x.UnitPrice.ToString("N2"),
            Total = x.TotalAmount.ToString("N2") + " ₺"
        }).ToList();

        _gridBulkSale.DataSource = rows.Count > 0 ? rows : null;
        if (_lblBulkTotal != null)
        {
            double grandTotal = _bulkSaleLines.Sum(x => x.TotalAmount);
            _lblBulkTotal.Text = $"Sepet Toplamı: {grandTotal:N2} ₺";
        }
    }

    private void SaveBulkQueuedItems()
    {
        if (_bulkSaleLines.Count == 0)
        {
            return;
        }

        long accountId = Convert.ToInt64(_cmbAccount.SelectedValue ?? 0);
        string paymentMethod = _cmbPayment.Text;

        if (accountId == 0 && paymentMethod == "Veresiye (Açık Hesap)")
        {
            MessageBox.Show("Toplu satışta veresiye işlemi için geçerli bir müşteri seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        double grandTotal = _bulkSaleLines.Sum(x => x.TotalAmount);
        if (paymentMethod == "💳 Parçalı / Çoklu Ödeme")
        {
            using var splitDialog = new SplitPaymentDialog(grandTotal, accountId > 0);
            if (splitDialog.ShowDialog(this) != DialogResult.OK) return;
            _splitCash = splitDialog.CashAmount;
            _splitCard = splitDialog.CardAmount;
            _splitTransfer = splitDialog.TransferAmount;
            _splitCredit = splitDialog.CreditAmount;
            _isSplitConfigured = true;
        }

        var lines = _bulkSaleLines.Select(x => (x.ProductId, x.Quantity, x.UnitPrice, x.TotalAmount)).ToList();
        try
        {
            if (!RunGuarded(guard => TransactionService.ProcessIntegratedSaleOrPurchaseBatch(
                _dtpDate.Value,
                accountId,
                _cmbOperation.Text,
                lines,
                paymentMethod,
                _txtDocNo.Text.Trim(),
                _cmbNote.Text.Trim(),
                _chkHasDueDate.Checked ? _dtpDueDate.Value.ToString("yyyy-MM-dd") : null,
                _cmbWarehouse.SelectedValue != null ? Convert.ToInt64(_cmbWarehouse.SelectedValue) : 1,
                _splitCash,
                _splitCard,
                _splitTransfer,
                _splitCredit,
                guard)))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Toplu satış kaydedilemedi; sepet işleminde hata oluştu: {ex.Message}", "İşlem Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SaleReceiptData? bulkReceipt = null;
        if (_cmbOperation.Text == "Satış")
        {
            bulkReceipt = BuildReceiptData(accountId, paymentMethod, _cmbNote.Text.Trim(),
                _bulkSaleLines.Select(x => new SaleReceiptLine { Name = x.Name, Unit = "Adet", Quantity = x.Quantity, UnitPrice = x.UnitPrice, DiscountPercent = x.DiscountPercent, VatPercent = x.VatPercent, Total = x.TotalAmount }).ToList());
        }

        _bulkSaleLines.Clear();
        RefreshBulkGrid();
        if (bulkReceipt != null)
            ShowSaleCompleted(bulkReceipt);
        else
            MessageBox.Show($"Toplam {lines.Count} ürün satırı kaydedildi. Sepet toplamı: {grandTotal:N2} ₺", "İşlem Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void LoadTouchButtons(string filter = "")
    {
        if (_flowTouchButtons == null || _cachedProductsTable == null) return;
        _flowTouchButtons.SuspendLayout();
        _flowTouchButtons.Controls.Clear();

        IEnumerable<DataRow> rows = _cachedProductsTable.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            string f = filter.ToLowerInvariant();
            rows = rows.Where(r => (r.Field<string>("Name") ?? "").ToLowerInvariant().Contains(f) ||
                                  (r.Field<string>("Barcode") ?? "").ToLowerInvariant().Contains(f) ||
                                  (r.Field<string>("Category") ?? "").ToLowerInvariant().Contains(f));
        }
        else
        {
            // Filtre yoksa: Önce IsFastSale=1 olanları, eğer hiç yoksa ilk 24 popüler ürünü göster
            var fastOnly = rows.Where(r => Convert.ToInt32(r["IsFastSale"]) == 1).ToList();
            if (fastOnly.Count > 0)
            {
                rows = fastOnly;
            }
            else
            {
                rows = rows.Take(24).ToList();
            }
        }

        // Dokunmatik renk paleti (Modern, pastel & canlı)
        Color[] cardColors = new[]
        {
            Color.FromArgb(224, 242, 254), // Mavi
            Color.FromArgb(220, 252, 231), // Yeşil
            Color.FromArgb(254, 249, 195), // Sarı
            Color.FromArgb(243, 232, 255), // Mor
            Color.FromArgb(255, 237, 213), // Turuncu
            Color.FromArgb(252, 231, 243), // Pembe
            Color.FromArgb(204, 251, 241), // Teal
            Color.FromArgb(254, 226, 226)  // Kırmızımsı
        };

        Color[] textColors = new[]
        {
            Color.FromArgb(3, 105, 161),
            Color.FromArgb(21, 128, 61),
            Color.FromArgb(161, 98, 7),
            Color.FromArgb(126, 34, 206),
            Color.FromArgb(194, 65, 12),
            Color.FromArgb(190, 24, 93),
            Color.FromArgb(15, 118, 110),
            Color.FromArgb(185, 28, 28)
        };

        int colorIdx = 0;
        foreach (var r in rows)
        {
            long pId = Convert.ToInt64(r["Id"]);
            string pName = r["Name"]?.ToString() ?? "Ürün";
            double pPrice = Convert.ToDouble(r["SalePrice"]);

            var btnCard = new Button
            {
                Width = 110,
                Height = 82,
                Margin = new Padding(4),
                BackColor = cardColors[colorIdx % cardColors.Length],
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = textColors[colorIdx % textColors.Length],
                TextAlign = ContentAlignment.TopCenter,
                Text = $"{pName}\n\n{pPrice:N2} ₺"
            };
            btnCard.FlatAppearance.BorderSize = 1;
            btnCard.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);

            btnCard.Click += (s, e) =>
            {
                _cmbProduct.SelectedValue = pId;
                _txtQty.Text = "1";
                AddCurrentProductToBulk();
                CalculateTotal(null, EventArgs.Empty);
            };

            _flowTouchButtons.Controls.Add(btnCard);
            colorIdx++;
        }

        if (_flowTouchButtons.Controls.Count == 0)
        {
            var lblEmpty = new Label
            {
                Text = "Aranan kritere uygun hızlı ürün bulunamadı.\n'⚙️ Düzenle' butonu ile hızlı ürünler seçebilirsiniz.",
                ForeColor = UITheme.TextMuted,
                Font = UITheme.SmallFont,
                AutoSize = true,
                Padding = new Padding(10)
            };
            _flowTouchButtons.Controls.Add(lblEmpty);
        }

        _flowTouchButtons.ResumeLayout();
    }

    private void OpenManageFastSaleProducts()
    {
        using var form = new Form
        {
            Text = "⭐ Hızlı Satış Butonlarını Yönet",
            Width = 520,
            Height = 600,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = UITheme.Background,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblInfo = new Label
        {
            Text = "Dokunmatik hızlı satış ekranında yer almasını istediğiniz ürünleri işaretleyin:",
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(12, 10, 12, 0),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
        };

        var chkList = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            CheckOnClick = true
        };

        var dt = Database.Query("SELECT Id, Name, SalePrice, COALESCE(IsFastSale, 0) AS IsFastSale FROM Products WHERE IsActive=1 ORDER BY Name");
        var idList = new List<long>();

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            var row = dt.Rows[i];
            long id = Convert.ToInt64(row["Id"]);
            idList.Add(id);
            string name = $"{row["Name"]} ({Convert.ToDouble(row["SalePrice"]):N2} ₺)";
            chkList.Items.Add(name, Convert.ToInt32(row["IsFastSale"]) == 1);
        }

        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(12) };
        var btnSave = UITheme.CreateButton("Kaydet", UITheme.Primary, Color.White, (s, e) =>
        {
            for (int i = 0; i < chkList.Items.Count; i++)
            {
                bool isChecked = chkList.GetItemChecked(i);
                long id = idList[i];
                Database.Execute("UPDATE Products SET IsFastSale=@p WHERE Id=@id", ("@p", isChecked ? 1 : 0), ("@id", id));
            }

            MessageBox.Show("Hızlı satış butonları güncellendi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            form.DialogResult = DialogResult.OK;
            form.Close();
        }, 110, 34);
        btnSave.Dock = DockStyle.Right;

        pnlBottom.Controls.Add(btnSave);
        form.Controls.Add(chkList);
        form.Controls.Add(lblInfo);
        form.Controls.Add(pnlBottom);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // Ürün tablosunu tazele
            _cachedProductsTable = Database.Query(@"
SELECT 
    p.Id, 
    p.Code,
    COALESCE(p.Barcode, '') AS Barcode,
    p.Code + ' - ' + p.Name AS Display, 
    p.Name,
    p.Category,
    p.PurchasePrice, 
    p.SalePrice,
    COALESCE(p.WholesalePrice, 0) AS WholesalePrice,
    COALESCE(p.SpecialPrice, 0) AS SpecialPrice,
    p.VatPercent,
    p.DiscountPercent,
    COALESCE(p.IsFastSale, 0) AS IsFastSale,
    ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) AS CurrentStock,
    p.Unit
FROM Products p 
WHERE p.IsActive = 1 
ORDER BY p.Name");
            LoadTouchButtons();
        }
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

    private void PrintThermalReceiptClick()
    {
        string customerName = (_cmbAccount.SelectedItem is DataRowView cav) ? cav["Display"]?.ToString() ?? "Perakende Müşteri" : "Perakende Müşteri";
        string productName = (_cmbProduct.SelectedItem is DataRowView pav) ? pav["Display"]?.ToString() ?? "Ürün" : "Ürün";
        double q = ParseNumber(_txtQty.Text);
        double price = ParseNumber(_txtUnitPrice.Text);
        double disc = ParseNumber(_txtDiscount.Text);
        double total = ParseNumber(_lblNetTotal.Text);
        string payment = _cmbPayment.Text;

        var items = new List<ReceiptPrintItem>
        {
            new ReceiptPrintItem
            {
                Name = productName,
                Quantity = q,
                UnitPrice = price,
                Total = total
            }
        };

        var result = ThermalReceiptService.PrintSale(
            customerName,
            _txtDocNo.Text.Trim(),
            payment,
            items,
            q * price,
            disc > 0 ? (q * price * disc / 100.0) : 0,
            total,
            0
        );

        if (!result.Success)
        {
            MessageBox.Show(result.Message, "Fiş Yazdırma", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        UpdateOperationUi();
        ProductSelectionChanged(null, EventArgs.Empty);
    }

    protected virtual bool IsSaleMode => _cmbOperation.Text == "Satış";

    protected virtual void ApplyOperationTheme()
    {
        bool isSale = IsSaleMode;
        this.BackColor = isSale ? UITheme.Background : Color.FromArgb(240, 253, 244);
        BtnSave.BackColor = isSale ? UITheme.Primary : Color.FromArgb(5, 150, 105);
        BtnSave.ForeColor = Color.White;
        _lblTouchTitle.ForeColor = isSale ? Color.FromArgb(30, 41, 59) : Color.FromArgb(22, 101, 52);
        if (_pnlTouch != null)
        {
            _pnlTouch.BackColor = isSale ? Color.FromArgb(248, 250, 252) : Color.FromArgb(240, 253, 244);
        }
        if (_flowTouchButtons != null)
        {
            _flowTouchButtons.BackColor = isSale ? Color.FromArgb(248, 250, 252) : Color.FromArgb(240, 253, 244);
        }
    }

    protected virtual void UpdateOperationUi()
    {
        bool isSale = IsSaleMode;

        Text = isSale ? "⚡ Hızlı Satış / POS Dokunmatik Kasa" : "📥 Hızlı Alış / Fatura Girişi";
        BtnSave.Text = isSale ? "⚡ Satışı Onayla" : "📥 Alışı Onayla";
        _lblTouchTitle.Text = isSale ? "⚡ Hızlı Satış Butonları (POS)" : "📦 Hızlı Alış Butonları (Stok Girişi)";
        _txtBarcodeScan.PlaceholderText = isSale
            ? "Barkod okutun veya yazıp Enter'a basın (F2)..."
            : "Alınacak ürün barkodunu okutun veya yazıp Enter'a basın...";
        _txtProductFilter.PlaceholderText = isSale
            ? "🔍 Ürün Adı, Barkod veya Stok Kodu yazarak hızlı satış yapın..."
            : "🔍 Alınacak ürün adı, barkod veya stok kodu ile hızlı arama yapın...";
        _lblBarcodeInfo.Text = isSale
            ? "💡 Normal barkod, terazi veya renk/beden varyant barkodları otomatik algılanır."
            : "💡 Alış işleminde ürün barkodu, stok hareketleri ve depo takibi otomatik güncellenir.";
        ApplyOperationTheme();
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

        // Müşteri Bilgi Ekranı (2. Ekran / Customer Display) Canlı Senkronizasyonu
        string prodName = (_cmbProduct.SelectedItem is DataRowView rv) ? rv["Display"]?.ToString() ?? "" : "";
        CustomerDisplayForm.UpdateCart(prodName, q, price, lineTotal);
    }

    private string DescribePayment(string paymentMethod)
    {
        if (paymentMethod != "💳 Parçalı / Çoklu Ödeme") return paymentMethod;
        var parts = new List<string>();
        if (_splitCash > 0) parts.Add($"Nakit {_splitCash:N2}");
        if (_splitCard > 0) parts.Add($"Kart {_splitCard:N2}");
        if (_splitTransfer > 0) parts.Add($"Havale {_splitTransfer:N2}");
        if (_splitCredit > 0) parts.Add($"Veresiye {_splitCredit:N2}");
        return "Parçalı (" + string.Join(", ", parts) + ")";
    }

    /// <summary>Kaydedilen satışın fiş/PDF verisini hazırlar (cari bilgisi ve güncel bakiye veritabanından okunur).</summary>
    private SaleReceiptData BuildReceiptData(long accountId, string paymentMethod, string? note, IEnumerable<SaleReceiptLine> lines)
    {
        var data = new SaleReceiptData
        {
            DocNo = _txtDocNo.Text.Trim(),
            Date = DateTime.Now,
            PaymentMethod = DescribePayment(paymentMethod),
            Note = note,
            AccountId = accountId
        };
        foreach (var l in lines) data.Lines.Add(l);
        data.LoadAccountInfo();
        return data;
    }

    private void ShowSaleCompleted(SaleReceiptData data)
    {
        using var dlg = new SaleCompletedDialog(data);
        dlg.ShowDialog(this);
    }

    private bool _isSaving;

    /// <summary>Çift tıklama / çift Enter ile aynı satışın iki kez kaydedilmesini engeller.</summary>
    private void SaveClick(object? sender, EventArgs e)
    {
        if (_isSaving)
        {
            DialogResult = DialogResult.None;
            return;
        }

        _isSaving = true;
        try
        {
            SaveClickCore(sender, e);
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>
    /// Satışı çalıştırır. Stok yetersizliği veya kredi limiti aşımı olursa kullanıcıya sorar;
    /// onay verirse (ve yetkisi varsa) aynı işlemi o kontrol atlanarak yeniden dener.
    /// </summary>
    private bool RunGuarded(Action<SaleGuardOptions> action)
    {
        var guard = new SaleGuardOptions();
        while (true)
        {
            try
            {
                action(guard);
                return true;
            }
            catch (SaleBlockedException ex)
            {
                var curUser = UserService.CurrentUser;
                bool canOverride = curUser == null || curUser.HasPermission(UserPermissions.SalesOverride);
                string title = ex.Kind == SaleBlockKind.InsufficientStock ? "Stok Yetersiz" : "Kredi Limiti Aşılıyor";

                if (!canOverride)
                {
                    MessageBox.Show(ex.Message + "\n\nBu satışı onaylama yetkiniz yok. Yöneticiye başvurun.", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var ask = MessageBox.Show(ex.Message + "\n\nYine de devam etmek istiyor musunuz?", title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (ask != DialogResult.Yes) return false;

                if (ex.Kind == SaleBlockKind.InsufficientStock) guard.AllowNegativeStock = true;
                else guard.AllowOverCreditLimit = true;
            }
        }
    }

    private void SaveClickCore(object? sender, EventArgs e)
    {
        if (_bulkSaleLines.Count > 0)
        {
            SaveBulkQueuedItems();
            return;
        }

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

                if (!RunGuarded(guard => TransactionService.ProcessIntegratedSaleOrPurchaseSplit(
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
                    warehouseId,
                    guard)))
                {
                    DialogResult = DialogResult.None;
                    return;
                }
            }
            else
            {
                if (!RunGuarded(guard => TransactionService.ProcessIntegratedSaleOrPurchase(
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
                    warehouseId,
                    guard)))
                {
                    DialogResult = DialogResult.None;
                    return;
                }
            }

            bool isSale = _cmbOperation.Text == "Satış";
            if (isSale)
            {
                // Fiş yazdırma, cari için PDF belge ve WhatsApp gönderimi tek pencerede
                string unit = (_cmbProduct.SelectedItem is DataRowView urv && urv["Unit"] != DBNull.Value) ? urv["Unit"]?.ToString() ?? "Adet" : "Adet";
                string pname = (_cmbProduct.SelectedItem is DataRowView prv) ? prv["Display"]?.ToString() ?? "Ürün" : "Ürün";
                var receipt = BuildReceiptData(accountId, paymentMethod, finalNote, new[]
                {
                    new SaleReceiptLine { Name = pname, Unit = unit, Quantity = q, UnitPrice = price, DiscountPercent = disc, VatPercent = vat, Total = totalAmount }
                });
                ShowSaleCompleted(receipt);
            }
            else
            {
                var promptWa = MessageBox.Show(
                    "Alış işlemi başarıyla kaydedildi! Stok ve Kasa hareketleri güncellendi.\n\nTedarikçiye veya giriş dekontuna WhatsApp ile bilgi göndermek ister misiniz?",
                    "Alış Kaydedildi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (promptWa == DialogResult.Yes)
                {
                    SendWhatsAppReceiptClick();
                }
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

        // 2. Terazi Barkodu ise: Yetki kontrolü ve PLU koduna göre ara
        if (parsed.IsScaleBarcode)
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.ScaleBarcode) && !curUser.HasPermission(UserPermissions.QuickSale))
            {
                MessageBox.Show("Elektronik barkodlu terazi ve tartılı ürün satışı yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtBarcodeScan.Text = string.Empty;
                return;
            }

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
