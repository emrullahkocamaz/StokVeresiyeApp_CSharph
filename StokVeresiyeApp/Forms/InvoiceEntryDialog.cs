using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class InvoiceEntryDialog : Form
{
    private readonly TextBox _txtInvoiceNo = new();
    private readonly DateTimePicker _dtpDate = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly ComboBox _cmbAccount = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _txtNote = new() { Width = 250 };

    private readonly DataGridView _gridItems = new();
    private readonly Label _lblSubTotal = new() { Text = "0,00 ₺", Font = UITheme.TitleFont, AutoSize = true };
    private readonly Label _lblVatTotal = new() { Text = "0,00 ₺", Font = UITheme.TitleFont, AutoSize = true };
    private readonly Label _lblGrandTotal = new() { Text = "0,00 ₺", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };

    private readonly CheckBox _chkUpdateStock = new() { Text = "Ürünleri seçilen depoya otomatik stok girişi yap (Alış Hareketi)", Checked = true, AutoSize = true };
    private readonly CheckBox _chkUpdateAccount = new() { Text = "Tedarikçi cari hesabına borç/alacak kaydı düş", Checked = true, AutoSize = true };
    private readonly DataGridView _gridReadyProducts = new();

    private string? _selectedFilePath;
    private ParsedInvoiceResult? _parsedResult;
    private readonly DataTable _itemsTable = new();
    private long? _targetUpdateInvoiceId;
    private bool _isSimpleMode = false;
    private readonly Button _btnToggleMode = UITheme.CreateButton("✨ Kolay Mod", Color.FromArgb(245, 158, 11), Color.White, null!, 125, 36);
    private readonly Panel _pnlReconcileWarning = new() { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(254, 242, 242), Visible = false, Padding = new Padding(12, 6, 12, 6) };
    private readonly Label _lblReconcileText = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(185, 28, 28) };

    public InvoiceEntryDialog(string? initialFilePath = null)
    {
        _selectedFilePath = initialFilePath;
        Text = "📄 E-Fatura / Alış Faturası Girişi & Stok Giriş Sihirbazı";
        ClientSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        InitializeTable();
        BuildUI();
        LoadCombos();

        if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            ProcessSelectedFile(_selectedFilePath);
        }
    }

    private void BindReadyProducts()
    {
        try
        {
            var dt = ProductService.GetAllProducts();
            _gridReadyProducts.DataSource = dt;

            if (_gridReadyProducts.Columns.Contains("Id")) _gridReadyProducts.Columns["Id"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Açılış")) _gridReadyProducts.Columns["Açılış"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Gelen")) _gridReadyProducts.Columns["Gelen"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Satılan")) _gridReadyProducts.Columns["Satılan"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Kritik Seviye")) _gridReadyProducts.Columns["Kritik Seviye"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Toplam Tutar")) _gridReadyProducts.Columns["Toplam Tutar"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Fatura No")) _gridReadyProducts.Columns["Fatura No"].Visible = false;
            if (_gridReadyProducts.Columns.Contains("Özellik / Tür")) _gridReadyProducts.Columns["Özellik / Tür"].Visible = false;

            foreach (var columnName in new[] { "Kalan Stok", "Satış Fiyatı" })
            {
                if (_gridReadyProducts.Columns.Contains(columnName))
                {
                    _gridReadyProducts.Columns[columnName].DefaultCellStyle.Format = "N2";
                    _gridReadyProducts.Columns[columnName].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
        }
        catch { }
    }

    private void InitializeTable()
    {
        _itemsTable.Columns.Add("ProductId", typeof(long));
        _itemsTable.Columns.Add("LineNo", typeof(int));
        _itemsTable.Columns.Add("Status", typeof(string));
        _itemsTable.Columns.Add("Barcode", typeof(string));
        _itemsTable.Columns.Add("ItemCode", typeof(string));
        _itemsTable.Columns.Add("ItemName", typeof(string));
        _itemsTable.Columns.Add("Quantity", typeof(double));
        _itemsTable.Columns.Add("Unit", typeof(string));
        _itemsTable.Columns.Add("UnitPrice", typeof(double));
        _itemsTable.Columns.Add("OldPurchasePrice", typeof(double));
        _itemsTable.Columns.Add("PriceDiffText", typeof(string));
        _itemsTable.Columns.Add("OldSalePrice", typeof(double));
        _itemsTable.Columns.Add("NewSalePrice", typeof(double));
        _itemsTable.Columns.Add("ActionDecision", typeof(string));
        _itemsTable.Columns.Add("DiscountPercent", typeof(double));
        _itemsTable.Columns.Add("DiscountAmount", typeof(double));
        _itemsTable.Columns.Add("VatPercent", typeof(double));
        _itemsTable.Columns.Add("VatAmount", typeof(double));
        _itemsTable.Columns.Add("OtherTaxes", typeof(double));
        _itemsTable.Columns.Add("LineTotal", typeof(double));

        _gridItems.DataSource = _itemsTable;
    }

    private void BuildUI()
    {
        // 1. Üst Başlık (Header)
        var header = new CardPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(20, 10, 20, 10) };
        var lblTitle = new Label { Text = "📄 E-Fatura / Alış Faturası Girişi & Akıllı Fiyat Karar Masası", Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 28 };
        var lblSub = new Label { Text = "Faturadaki ürün fiyatları ile sistemdeki fiyatlar anlık kıyaslanır. Zam, indirim veya yeni ürünler için satır bazında veya topluca karar verebilirsiniz.", Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        header.Controls.Add(lblSub);
        header.Controls.Add(lblTitle);
        Controls.Add(header);

        // 2. Toolbar & Dosya Seçimi
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(15, 8, 15, 8) };
        var flowToolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        var btnSelectFile = UITheme.CreateButton("📂 E-Fatura Seç (.pdf / .xml)", UITheme.Primary, Color.White, (s, e) => SelectFile(), 195, 36);
        var btnPasteWizard = UITheme.CreateButton("📋 Metinden Kalem Çıkar (Sağlama)", Color.FromArgb(14, 165, 233), Color.White, (s, e) => OpenTextImportWizard(), 235, 36);
        _btnToggleMode.Click += (s, e) => ToggleSimpleMode();
        var btnOpenPdf = UITheme.CreateButton("👁️ PDF Belgeyi Aç", UITheme.Secondary, Color.White, (s, e) => OpenOriginalPdf(), 145, 36);
        var btnGenBarcodes = UITheme.CreateButton("⚡ Otomatik Barkod Üret", Color.FromArgb(16, 185, 129), Color.White, (s, e) => GenerateMissingBarcodes(false), 170, 36);
        var btnAddRow = UITheme.CreateButton("➕ Manuel Kalem Ekle", Color.FromArgb(79, 70, 229), Color.White, (s, e) => AddManualRow(), 155, 36);
        var btnDeleteRow = UITheme.CreateButton("🗑️ Satırı Sil", UITheme.Danger, Color.White, (s, e) => DeleteSelectedRow(), 115, 36);

        flowToolbar.Controls.Add(btnSelectFile);
        flowToolbar.Controls.Add(btnPasteWizard);
        flowToolbar.Controls.Add(_btnToggleMode);
        flowToolbar.Controls.Add(btnOpenPdf);
        flowToolbar.Controls.Add(btnGenBarcodes);
        flowToolbar.Controls.Add(btnAddRow);
        flowToolbar.Controls.Add(btnDeleteRow);
        toolbar.Controls.Add(flowToolbar);
        Controls.Add(toolbar);

        // 2.B. Uyuşmazlık & Fiyat Karar Sihirbazı Çubuğu (Decision Toolbar)
        var pnlDecisionBar = new CardPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(15, 6, 15, 6), BackColor = Color.FromArgb(248, 250, 252) };
        var flowDecision = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        var lblDecTitle = new Label { Text = "⚡ Toplu Karar:", Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = UITheme.TextPrimary, AutoSize = true, Margin = new Padding(0, 8, 10, 0) };
        var btnApplyAllBuy = UITheme.CreateButton("📈 Tüm Zamları Alışa Yansıt", Color.FromArgb(217, 119, 6), Color.White, (s, e) => BatchSetDecision("Alış Fiyatını Güncelle"), 190, 32);
        var btnApplyAllSale = UITheme.CreateButton("🚀 Kâr Marjını Koru (Satışları da Artır)", Color.FromArgb(16, 185, 129), Color.White, (s, e) => BatchSetDecision("Satış Fiyatına Zam Yap"), 240, 32);
        var btnKeepAllAsIs = UITheme.CreateButton("🛡️ Fiyatlara Dokunma (Sadece Stok Gir)", Color.FromArgb(100, 116, 139), Color.White, (s, e) => BatchSetDecision("Olduğu Gibi Al"), 235, 32);
        var btnExcludeSelected = UITheme.CreateButton("❌ Faturadan Çıkar (Hariç Tut)", Color.FromArgb(239, 68, 68), Color.White, (s, e) => SetSelectedDecision("Faturadan Sil"), 190, 32);

        flowDecision.Controls.Add(lblDecTitle);
        flowDecision.Controls.Add(btnApplyAllBuy);
        flowDecision.Controls.Add(btnApplyAllSale);
        flowDecision.Controls.Add(btnKeepAllAsIs);
        flowDecision.Controls.Add(btnExcludeSelected);
        pnlDecisionBar.Controls.Add(flowDecision);
        Controls.Add(pnlDecisionBar);

        // 3. Fatura Başlık Bilgileri
        var formPanel = new CardPanel { Dock = DockStyle.Top, Height = 115, Padding = new Padding(15, 10, 15, 10) };
        var flowFields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true };

        var readyPanel = new CardPanel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(15, 10, 15, 10), Margin = new Padding(0, 0, 0, 10) };
        var readyTitle = new Label { Text = "📦 Satışa Hazır Ürünler (Depoda Mevcut / Fiyat Görünümü)", Font = UITheme.TitleFont, ForeColor = UITheme.Primary, Dock = DockStyle.Top, Height = 26 };
        _gridReadyProducts.ReadOnly = true;
        _gridReadyProducts.AllowUserToAddRows = false;
        _gridReadyProducts.RowHeadersVisible = false;
        _gridReadyProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridReadyProducts.Font = UITheme.SmallFont;
        _gridReadyProducts.Dock = DockStyle.Fill;
        BindReadyProducts();
        readyPanel.Controls.Add(_gridReadyProducts);
        readyPanel.Controls.Add(readyTitle);
        readyTitle.SendToBack();
        _gridReadyProducts.BringToFront();
        Controls.Add(readyPanel);

        // Fatura No
        var pnlNo = CreateFieldPanel("Fatura No:", _txtInvoiceNo, 160);
        // Tarih
        var pnlDate = CreateFieldPanel("Fatura Tarihi:", _dtpDate, 130);
        // Tedarikçi Cari
        var pnlAccount = CreateFieldPanel("Tedarikçi (Cari):", _cmbAccount, 260);
        var btnNewAccount = UITheme.CreateButton("➕ Yeni Cari", Color.FromArgb(16, 185, 129), Color.White, (s, e) => QuickCreateAccount(), 100, 28);
        btnNewAccount.Margin = new Padding(0, 20, 15, 0);

        // Hedef Depo
        var pnlWarehouse = CreateFieldPanel("Hedef Depo:", _cmbWarehouse, 180);
        var btnNewWarehouse = UITheme.CreateButton("➕ Yeni Depo", Color.FromArgb(13, 148, 136), Color.White, (s, e) => QuickCreateWarehouse(), 105, 28);
        btnNewWarehouse.Margin = new Padding(0, 20, 15, 0);

        // Açıklama
        var pnlNote = CreateFieldPanel("Açıklama / Not:", _txtNote, 220);

        flowFields.Controls.Add(pnlNo);
        flowFields.Controls.Add(pnlDate);
        flowFields.Controls.Add(pnlAccount);
        flowFields.Controls.Add(btnNewAccount);
        flowFields.Controls.Add(pnlWarehouse);
        flowFields.Controls.Add(btnNewWarehouse);
        flowFields.Controls.Add(pnlNote);

        formPanel.Controls.Add(flowFields);
        Controls.Add(formPanel);

        // 4. Alt Toplamlar ve Onay Bölümü
        var bottomPanel = new CardPanel { Dock = DockStyle.Bottom, Height = 95, Padding = new Padding(20, 10, 20, 10) };

        var pnlLeftBottom = new Panel { Dock = DockStyle.Left, Width = 440 };
        _chkUpdateStock.Location = new Point(5, 12);
        _chkUpdateAccount.Location = new Point(5, 42);
        pnlLeftBottom.Controls.Add(_chkUpdateStock);
        pnlLeftBottom.Controls.Add(_chkUpdateAccount);
        bottomPanel.Controls.Add(pnlLeftBottom);

        var pnlRightBottom = new Panel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var flowTotals = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };

        var cardMatrah = CreateMetricBox("Matrah (KDV Hariç)", _lblSubTotal, Color.FromArgb(71, 85, 105), 150);
        var cardVat = CreateMetricBox("KDV Toplamı", _lblVatTotal, Color.FromArgb(99, 102, 241), 140);
        var cardGrand = CreateMetricBox("GENEL TOPLAM", _lblGrandTotal, Color.FromArgb(16, 185, 129), 180, isHighlight: true);

        var btnSave = UITheme.CreateButton("💾 Faturayı Onayla & Depoya Giriş Yap", UITheme.Success, Color.White, (s, e) => SaveInvoiceClick(), 290, 52);
        btnSave.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        btnSave.Margin = new Padding(12, 6, 0, 0);

        flowTotals.Controls.Add(cardMatrah);
        flowTotals.Controls.Add(cardVat);
        flowTotals.Controls.Add(cardGrand);
        flowTotals.Controls.Add(btnSave);

        pnlRightBottom.Controls.Add(flowTotals);
        bottomPanel.Controls.Add(pnlRightBottom);

        Controls.Add(bottomPanel);

        // 5. Grid (Orta Alan)
        var gridPanel = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        UITheme.ApplyGridStyle(_gridItems);
        _gridItems.ReadOnly = false;
        _gridItems.AllowUserToAddRows = false;
        _gridItems.RowHeadersVisible = false;
        _gridItems.CellValueChanged += (s, e) => RecalculateTotals();

        _pnlReconcileWarning.Controls.Add(_lblReconcileText);
        gridPanel.Controls.Add(_gridItems);
        gridPanel.Controls.Add(_pnlReconcileWarning);
        Controls.Add(gridPanel);

        // Grid Olayları (Renklendirme ve Karar Menüsü)
        _gridItems.CellFormatting += GridItems_CellFormatting;
        _gridItems.CellClick += GridItems_CellClick;

        // Z-Index düzeni: arka plan başlık ve toolbar, orta form ve grid önde kalır.
        Controls.SetChildIndex(header, 0);
        Controls.SetChildIndex(toolbar, 1);
        Controls.SetChildIndex(formPanel, 2);
        Controls.SetChildIndex(bottomPanel, 3);
        Controls.SetChildIndex(gridPanel, 4);
        gridPanel.BringToFront();

        FormatGridColumns();
    }

    private void FormatGridColumns()
    {
        if (_gridItems.Columns.Count == 0) return;

        if (_gridItems.Columns["ProductId"] is { } colProductId)
            colProductId.Visible = false;

        if (_gridItems.Columns["LineNo"] is { } colLineNo)
        {
            colLineNo.HeaderText = "Sıra";
            colLineNo.Width = 45;
            colLineNo.ReadOnly = true;
            colLineNo.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["Status"] is { } colStatus)
        {
            colStatus.HeaderText = "Durum";
            colStatus.Width = 100;
            colStatus.ReadOnly = true;
            colStatus.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["Barcode"] is { } colBarcode)
        {
            colBarcode.HeaderText = "Barkod No";
            colBarcode.Width = 125;
        }

        if (_gridItems.Columns["ItemCode"] is { } colItemCode)
        {
            colItemCode.HeaderText = "Ürün Kodu";
            colItemCode.Width = 95;
            colItemCode.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["ItemName"] is { } colItemName)
        {
            colItemName.HeaderText = "Mal / Hizmet (Ürün Adı)";
            colItemName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        if (_gridItems.Columns["Quantity"] is { } colQuantity)
        {
            colQuantity.HeaderText = "Miktar";
            colQuantity.Width = 65;
            colQuantity.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["Unit"] is { } colUnit)
        {
            colUnit.HeaderText = "Birim";
            colUnit.Width = 55;
        }

        if (_gridItems.Columns["UnitPrice"] is { } colUnitPrice)
        {
            colUnitPrice.HeaderText = "Faturadaki Alış (₺)";
            colUnitPrice.Width = 110;
            colUnitPrice.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["OldPurchasePrice"] is { } colOldBuy)
        {
            colOldBuy.HeaderText = "Eski Alış (₺)";
            colOldBuy.Width = 90;
            colOldBuy.DefaultCellStyle.Format = "N2";
            colOldBuy.ReadOnly = true;
            colOldBuy.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["PriceDiffText"] is { } colDiff)
        {
            colDiff.HeaderText = "Fiyat Değişimi";
            colDiff.Width = 125;
            colDiff.ReadOnly = true;
        }

        if (_gridItems.Columns["OldSalePrice"] is { } colOldSale)
        {
            colOldSale.HeaderText = "Eski Satış (₺)";
            colOldSale.Width = 90;
            colOldSale.DefaultCellStyle.Format = "N2";
            colOldSale.ReadOnly = true;
            colOldSale.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["NewSalePrice"] is { } colNewSale)
        {
            colNewSale.HeaderText = "Yeni Satış (₺)";
            colNewSale.Width = 100;
            colNewSale.DefaultCellStyle.Format = "N2";
        }

        if (_gridItems.Columns["ActionDecision"] is { } colAction)
        {
            colAction.HeaderText = "⚡ Karar (Tıkla)";
            colAction.Width = 145;
            colAction.ReadOnly = true;
        }

        if (_gridItems.Columns["DiscountPercent"] is { } colDiscPct)
        {
            colDiscPct.HeaderText = "İsk %";
            colDiscPct.Width = 50;
            colDiscPct.DefaultCellStyle.Format = "N0";
            colDiscPct.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["DiscountAmount"] is { } colDiscAmount)
        {
            colDiscAmount.HeaderText = "İskonto";
            colDiscAmount.Width = 70;
            colDiscAmount.DefaultCellStyle.Format = "N2";
            colDiscAmount.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["VatPercent"] is { } colVatPct)
        {
            colVatPct.HeaderText = "KDV %";
            colVatPct.Width = 55;
            colVatPct.DefaultCellStyle.Format = "N0";
            colVatPct.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["VatAmount"] is { } colVatAmount)
        {
            colVatAmount.HeaderText = "KDV Tutarı";
            colVatAmount.Width = 80;
            colVatAmount.DefaultCellStyle.Format = "N2";
            colVatAmount.ReadOnly = true;
            colVatAmount.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["OtherTaxes"] is { } colOtherTaxes)
        {
            colOtherTaxes.HeaderText = "Diğer Verg.";
            colOtherTaxes.Width = 70;
            colOtherTaxes.DefaultCellStyle.Format = "N2";
            colOtherTaxes.Visible = !_isSimpleMode;
        }

        if (_gridItems.Columns["LineTotal"] is { } colLineTotal)
        {
            colLineTotal.HeaderText = "Mal Hizmet Tutarı";
            colLineTotal.Width = 110;
            colLineTotal.DefaultCellStyle.Format = "N2";
            colLineTotal.ReadOnly = true;
        }
    }

    private void GridItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridItems.Rows.Count) return;
        var row = _gridItems.Rows[e.RowIndex];
        if (row.DataBoundItem is not DataRowView drv) return;

        string decision = drv["ActionDecision"]?.ToString() ?? "";
        string diffText = drv["PriceDiffText"]?.ToString() ?? "";
        string status = drv["Status"]?.ToString() ?? "";

        // Faturadan çıkarılmış / silinmiş kalemler: soluk gri
        if (decision.Contains("Sil") || decision.Contains("Hariç"))
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            return;
        }

        // Fiyat artışı / Zam: hafif sarı/turuncu
        if (diffText.Contains("🔺") || diffText.Contains("ZAM"))
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
        }
        // İndirim: hafif yeşil
        else if (diffText.Contains("🔻"))
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
        }
        // Yeni ürün: hafif mor
        else if (status.Contains("Yeni"))
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(243, 232, 255);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(88, 28, 135);
        }
        else
        {
            row.DefaultCellStyle.BackColor = Color.White;
            row.DefaultCellStyle.ForeColor = UITheme.TextPrimary;
        }

        // Karar hücresini belirginleştir
        if (_gridItems.Columns[e.ColumnIndex].Name == "ActionDecision")
        {
            e.CellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            if (decision == "Satış Fiyatına Zam Yap")
                e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
            else if (decision == "Alış Fiyatını Güncelle")
                e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6);
            else if (decision == "Olduğu Gibi Al")
                e.CellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            else if (decision == "Faturadan Sil")
                e.CellStyle.ForeColor = Color.FromArgb(239, 68, 68);
        }
    }

    private void GridItems_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string colName = _gridItems.Columns[e.ColumnIndex].Name;

        if (colName == "ActionDecision")
        {
            if (_gridItems.Rows[e.RowIndex].DataBoundItem is not DataRowView drv) return;

            var menu = new ContextMenuStrip();
            menu.Items.Add("📈 Alış Fiyatını Güncelle (Sadece maliyet)", null, (s, ev) =>
            {
                drv["ActionDecision"] = "Alış Fiyatını Güncelle";
                _gridItems.InvalidateRow(e.RowIndex);
            });
            menu.Items.Add("🚀 Satış Fiyatına Zam Yap (Kâr marjını koru)", null, (s, ev) =>
            {
                drv["ActionDecision"] = "Satış Fiyatına Zam Yap";
                _gridItems.InvalidateRow(e.RowIndex);
            });
            menu.Items.Add("🛡️ Olduğu Gibi Al (Fiyatlara dokunma, sadece stok gir)", null, (s, ev) =>
            {
                drv["ActionDecision"] = "Olduğu Gibi Al";
                _gridItems.InvalidateRow(e.RowIndex);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("❌ Faturadan Sil / Hariç Tut (Bu kalemi alma)", null, (s, ev) =>
            {
                drv["ActionDecision"] = "Faturadan Sil";
                _gridItems.InvalidateRow(e.RowIndex);
                RecalculateTotals();
            });

            var cellRect = _gridItems.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            menu.Show(_gridItems, cellRect.Left, cellRect.Bottom);
        }
    }

    private void BatchSetDecision(string decision)
    {
        int count = 0;
        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            string curDecision = row["ActionDecision"]?.ToString() ?? "";
            if (curDecision == "Faturadan Sil") continue; // Önceden silinenleri elle değiştirmediyse dokunma

            row["ActionDecision"] = decision;
            count++;
        }
        _gridItems.Invalidate();
        MessageBox.Show($"Tablodaki {count} kalemin işlemi '{decision}' olarak ayarlandı.", "Toplu Karar Verildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetSelectedDecision(string decision)
    {
        if (_gridItems.CurrentRow?.DataBoundItem is DataRowView drv)
        {
            drv["ActionDecision"] = decision;
            _gridItems.InvalidateRow(_gridItems.CurrentRow.Index);
            RecalculateTotals();
        }
    }

    private void ToggleSimpleMode()
    {
        _isSimpleMode = !_isSimpleMode;
        _btnToggleMode.Text = _isSimpleMode ? "⚙️ Detaylı Mod" : "✨ Kolay Mod";
        _btnToggleMode.BackColor = _isSimpleMode ? Color.FromArgb(71, 85, 105) : Color.FromArgb(245, 158, 11);
        FormatGridColumns();
    }

    private void OpenTextImportWizard()
    {
        string? textToPass = _parsedResult?.RawText;
        if (string.IsNullOrWhiteSpace(textToPass) && !string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            try
            {
                if (Path.GetExtension(_selectedFilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = UglyToad.PdfPig.PdfDocument.Open(_selectedFilePath);
                    var sb = new System.Text.StringBuilder();
                    foreach (var p in doc.GetPages())
                    {
                        sb.AppendLine(p.Text);
                    }
                    textToPass = sb.ToString();
                }
            }
            catch { }
        }

        using var dlg = new InvoiceTextImportDialog(textToPass);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ExtractedItems.Count > 0)
        {
            _itemsTable.Rows.Clear();
            int counter = 1;
            foreach (var item in dlg.ExtractedItems)
            {
                AddInvoiceRow(item, counter++);
            }

            RecalculateTotals();
            FormatGridColumns();

            MessageBox.Show(
                $"{dlg.ExtractedItems.Count} adet ürün kalem metninden başarıyla ayrıştırıldı ve fatura tablosuna aktarıldı!\n\n" +
                "Ürünlerin fiyat değişimlerini ve kararlarını tablodan inceleyebilirsiniz.",
                "Kalemler Aktarıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    /// <summary>
    /// Fatura kalemini sistemdeki ürün fiyatlarıyla kıyaslayarak analitik satır olarak ekler.
    /// </summary>
    private void AddInvoiceRow(ParsedInvoiceItem item, int lineNo)
    {
        var matchedProduct = InvoiceService.FindProductByNameOrCode(item.ItemName, item.Barcode ?? item.ItemCode);
        var row = _itemsTable.NewRow();

        double oldBuy = matchedProduct?.PurchasePrice ?? 0;
        double oldSale = matchedProduct?.SalePrice ?? 0;
        double newBuy = item.UnitPrice;

        string diffText = "➖ Aynı Fiyat";
        string defaultDecision = "Alış Fiyatını Güncelle";
        double newSale = oldSale;

        if (matchedProduct == null)
        {
            diffText = "✨ Yeni Ürün";
            defaultDecision = "Yeni Ürün Kartı Aç";
            newSale = Math.Round(newBuy * 1.30, 2); // Varsayılan %30 kâr
        }
        else if (oldBuy > 0)
        {
            double diff = newBuy - oldBuy;
            double pct = Math.Round((diff / oldBuy) * 100.0, 1);
            if (diff > 0.01)
            {
                diffText = $"🔺 +{diff:N2} ₺ (%{pct}) ZAM";
                defaultDecision = "Alış Fiyatını Güncelle";
                // Eski kâr marjını koruyarak yeni satış fiyatı hesapla
                if (oldSale > oldBuy)
                {
                    double marginRatio = oldSale / oldBuy;
                    newSale = Math.Round(newBuy * marginRatio, 2);
                }
                else
                {
                    newSale = Math.Round(newBuy * 1.30, 2);
                }
            }
            else if (diff < -0.01)
            {
                diffText = $"🔻 {diff:N2} ₺ (%{Math.Abs(pct)})";
                defaultDecision = "Alış Fiyatını Güncelle";
                newSale = oldSale;
            }
            else
            {
                diffText = "➖ Aynı Fiyat";
                defaultDecision = "Olduğu Gibi Al";
                newSale = oldSale;
            }
        }
        else
        {
            diffText = "➖ Eski Fiyat: 0";
            defaultDecision = "Alış Fiyatını Güncelle";
            newSale = oldSale > 0 ? oldSale : Math.Round(newBuy * 1.30, 2);
        }

        double finalQty = item.Quantity > 0 ? item.Quantity : 1;
        double finalUnitPrice = item.UnitPrice;
        double finalDiscPercent = item.DiscountPercent;
        double finalLineTotal = item.LineTotal > 0 ? item.LineTotal : Math.Round(finalQty * finalUnitPrice, 2);
        double finalDiscAmount = item.DiscountAmount;

        // Çifte İskonto Koruması:
        // Eğer faturadaki alış birim fiyatı ile miktar çarpımı zaten net satır toplamına eşitse,
        // faturadaki fiyat zaten iskontolu fiyattır. Bir daha iskonto düşülmemelidir!
        if (Math.Abs((finalQty * finalUnitPrice) - finalLineTotal) < 1.0 && finalDiscPercent > 0)
        {
            finalDiscPercent = 0;
            finalDiscAmount = 0;
        }
        else if (finalUnitPrice > 0 && finalDiscPercent > 0 && finalDiscAmount == 0)
        {
            finalDiscAmount = Math.Round((finalQty * finalUnitPrice) * (finalDiscPercent / 100.0), 2);
            finalLineTotal = Math.Round((finalQty * finalUnitPrice) - finalDiscAmount, 2);
        }

        row["ProductId"] = matchedProduct?.Id ?? (object)DBNull.Value;
        row["LineNo"] = lineNo;
        row["Status"] = matchedProduct != null ? "✅ Kayıtlı" : "⚠️ Yeni Ürün";
        row["Barcode"] = matchedProduct?.Barcode ?? item.Barcode ?? "";
        row["ItemCode"] = matchedProduct?.Code ?? item.ItemCode ?? "";
        row["ItemName"] = matchedProduct?.Name ?? item.ItemName;
        row["Quantity"] = finalQty;
        row["Unit"] = !string.IsNullOrWhiteSpace(item.Unit) ? item.Unit : "Adet";
        row["UnitPrice"] = finalUnitPrice;
        row["OldPurchasePrice"] = oldBuy;
        row["PriceDiffText"] = diffText;
        row["OldSalePrice"] = oldSale;
        row["NewSalePrice"] = newSale;
        row["ActionDecision"] = defaultDecision;
        row["DiscountPercent"] = finalDiscPercent;
        row["DiscountAmount"] = finalDiscAmount;
        row["VatPercent"] = item.VatPercent;
        row["VatAmount"] = item.VatAmount > 0 ? item.VatAmount : Math.Round(finalLineTotal * (item.VatPercent / 100.0), 2);
        row["OtherTaxes"] = item.OtherTaxes;
        row["LineTotal"] = finalLineTotal;

        _itemsTable.Rows.Add(row);
    }

    private Panel CreateFieldPanel(string labelText, Control control, int width)
    {
        var pnl = new Panel { Width = width, Height = 55, Margin = new Padding(0, 0, 10, 0) };
        var lbl = new Label { Text = labelText, Font = UITheme.SmallFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 18 };
        control.Dock = DockStyle.Bottom;
        pnl.Controls.Add(lbl);
        pnl.Controls.Add(control);
        return pnl;
    }

    private static Panel CreateMetricBox(string title, Label valueLabel, Color accentColor, int width, bool isHighlight = false)
    {
        var box = new Panel
        {
            Width = width,
            Height = 62,
            Margin = new Padding(6, 4, 6, 4),
            BackColor = isHighlight ? Color.FromArgb(240, 253, 244) : Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 6, 10, 6)
        };
        box.Paint += (s, e) =>
        {
            using var pen = new Pen(isHighlight ? accentColor : Color.FromArgb(203, 213, 225), isHighlight ? 2f : 1f);
            e.Graphics.DrawRectangle(pen, 0, 0, box.Width - 1, box.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Top,
            Height = 18,
            TextAlign = ContentAlignment.TopRight
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = isHighlight ? new Font("Segoe UI", 13.5f, FontStyle.Bold) : new Font("Segoe UI", 11f, FontStyle.Bold);
        valueLabel.ForeColor = isHighlight ? accentColor : Color.FromArgb(30, 41, 59);
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.AutoSize = false;

        box.Controls.Add(valueLabel);
        box.Controls.Add(lblTitle);
        return box;
    }

    private void LoadCombos()
    {
        // 1. Tedarikçiler
        var accounts = Database.Query("SELECT Id, Name FROM Accounts WHERE IsActive = 1 ORDER BY Name;");
        _cmbAccount.Items.Clear();
        _cmbAccount.DisplayMember = "Name";
        _cmbAccount.ValueMember = "Id";
        foreach (DataRow row in accounts.Rows)
        {
            _cmbAccount.Items.Add(new ComboBoxItem(Convert.ToInt64(row["Id"]), row["Name"].ToString()!));
        }

        // 2. Depolar
        var warehouses = Database.Query("SELECT Id, Name FROM Warehouses WHERE IsActive = 1 ORDER BY IsDefault DESC, Name;");
        _cmbWarehouse.Items.Clear();
        _cmbWarehouse.DisplayMember = "Name";
        _cmbWarehouse.ValueMember = "Id";
        foreach (DataRow row in warehouses.Rows)
        {
            _cmbWarehouse.Items.Add(new ComboBoxItem(Convert.ToInt64(row["Id"]), row["Name"].ToString()!));
        }
        if (_cmbWarehouse.Items.Count > 0)
            _cmbWarehouse.SelectedIndex = 0;
    }

    private void SelectFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "E-Fatura Dosyası Seçin (.pdf veya .xml)",
            Filter = "E-Fatura Dosyaları (*.pdf;*.xml)|*.pdf;*.xml|PDF Belgeleri (*.pdf)|*.pdf|XML Belgeleri (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            ProcessSelectedFile(ofd.FileName);
        }
    }

    private void ProcessSelectedFile(string filePath)
    {
        _selectedFilePath = filePath;
        Cursor = Cursors.WaitCursor;

        try
        {
            _parsedResult = InvoiceParserService.ParseInvoiceFile(filePath);

            if (!_parsedResult.Success)
            {
                MessageBox.Show(
                    "Fatura okunurken uyarı oluştu:\n\n" + _parsedResult.ErrorMessage + 
                    "\n\nFatura bilgilerini manuel olarak düzenleyebilirsiniz.",
                    "Fatura Okuma",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }

            // Alanları doldur
            if (!string.IsNullOrWhiteSpace(_parsedResult.InvoiceNumber))
            {
                _txtInvoiceNo.Text = _parsedResult.InvoiceNumber;

                // Mükerrer Fatura Kontrolü
                var existingInv = InvoiceService.GetInvoiceByNumber(_parsedResult.InvoiceNumber);
                if (existingInv != null)
                {
                    long existId = Convert.ToInt64(existingInv["Id"]);
                    string existDate = existingInv["InvoiceDate"]?.ToString() ?? "";
                    string existAcc = existingInv["AccountName"]?.ToString() ?? "";
                    double existTotal = Convert.ToDouble(existingInv["GrandTotal"]);

                    var res = MessageBox.Show(
                        $"⚠️ DİKKAT: MÜKERRER FATURA TESPİT EDİLDİ!\n\n" +
                        $"'{_parsedResult.InvoiceNumber}' numaralı fatura daha önce sisteme kaydedilmiştir:\n" +
                        $"• Kayıtlı Tarih: {existDate}\n" +
                        $"• Tedarikçi Cari: {existAcc}\n" +
                        $"• Kayıtlı Tutar: {existTotal:N2} ₺\n\n" +
                        $"Ne yapmak istersiniz?\n\n" +
                        $"• [EVET] : MEVCUT FATURAYI GÜNCELLE / DEĞİŞTİR (Eski kaydın üzerine yeni kalemleri yazar, mükerrerliği önler).\n" +
                        $"• [HAYIR] : ATLA / İPTAL ET (Bu faturayı yüklemekten vazgeç).\n" +
                        $"• [İPTAL] : YİNE DE YENİ BİR FATURA OLARAK KAYDET.",
                        "Mükerrer Fatura Uyarısı (Atla / Değiştir)",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning
                    );

                    if (res == DialogResult.No)
                    {
                        Close();
                        return;
                    }
                    else if (res == DialogResult.Yes)
                    {
                        _targetUpdateInvoiceId = existId;
                    }
                    else
                    {
                        _targetUpdateInvoiceId = null;
                    }
                }
            }

            _dtpDate.Value = _parsedResult.InvoiceDate;

            // Cari Eşleme
            try
            {
                AutoMatchAccount(_parsedResult.SupplierTaxNumber, _parsedResult.SupplierName);
            }
            catch { }

            // Kalemleri doldur
            _itemsTable.Rows.Clear();
            int counter = 1;
            foreach (var item in _parsedResult.Items)
            {
                AddInvoiceRow(item, item.LineNo > 0 ? item.LineNo : counter++);
            }

            RecalculateTotals();

            // Eğer kalem toplamı 0 ama faturadan toplam tutar çekildiyse alt toplamlara yansıt
            if (_itemsTable.Rows.Count == 0 && _parsedResult.GrandTotal > 0)
            {
                _lblSubTotal.Text = _parsedResult.SubTotal.ToString("N2") + " ₺";
                _lblVatTotal.Text = _parsedResult.VatTotal.ToString("N2") + " ₺";
                _lblGrandTotal.Text = _parsedResult.GrandTotal.ToString("N2") + " ₺";
            }

            FormatGridColumns();

            string countMsg = _parsedResult.Items.Count > 0 
                ? $"{_parsedResult.Items.Count} adet kalem başarıyla aktarıldı." 
                : "Fatura tutarları aktarıldı. 'Manuel Kalem Ekle' butonuyla ürünleri girebilirsiniz.";

            MessageBox.Show(
                $"Fatura ({_parsedResult.FileType}) başarıyla yüklendi!\n\n" +
                $"Fatura No: {_parsedResult.InvoiceNumber}\n" +
                $"Tarih: {_parsedResult.InvoiceDate:dd.MM.yyyy}\n" +
                $"Tedarikçi: {(!string.IsNullOrWhiteSpace(_parsedResult.SupplierName) ? _parsedResult.SupplierName : "[Seçiniz]")}\n" +
                $"Genel Toplam: {_parsedResult.GrandTotal:N2} ₺\n\n" +
                countMsg,
                "E-Fatura Yüklendi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            // Barkod kontrolü: Faturada veya sistemde barkodu olmayan ürünleri tespit et
            int missingBarcodeCount = 0;
            foreach (DataRow row in _itemsTable.Rows)
            {
                string b = row["Barcode"]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(b))
                {
                    missingBarcodeCount++;
                }
            }

            if (missingBarcodeCount > 0)
            {
                var askResult = MessageBox.Show(
                    $"Faturadaki {missingBarcodeCount} adet ürünün barkod numarası bulunamadı veya okunamadı.\n\n" +
                    "Bu ürünler için sistem tarafından dükkanınıza özel benzersiz EAN-13 iç barkod numarası (örn: 200...) otomatik oluşturulsun mu?\n\n" +
                    "• [Evet] : Tüm barkodsuz ürünlere anında benzersiz barkod atanır (Raf etiketi ve barkod basımına hazır olur).\n" +
                    "• [Hayır] : Ürünler barkodsuz olarak bırakılır.",
                    "Barkod Numarası Oluşturulsun mu?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (askResult == DialogResult.Yes)
                {
                    GenerateMissingBarcodes(false);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fatura işlenirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void AutoMatchAccount(string? taxNumber, string? name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                name = name.Trim();
                if (name.Length > 120) name = name.Substring(0, 120);
            }

            var matched = InvoiceService.FindAccountByTaxOrName(taxNumber, name);
            if (matched != null)
            {
                for (int i = 0; i < _cmbAccount.Items.Count; i++)
                {
                    if (_cmbAccount.Items[i] is ComboBoxItem item && item.Id == matched.Id)
                    {
                        _cmbAccount.SelectedIndex = i;
                        return;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(name) && name.Length >= 4 && !name.Contains("Toplam") && !name.Contains("Matrah") && !name.Contains("KDV"))
            {
                var res = MessageBox.Show(
                    $"Faturadaki satıcı firma '{name}' sistemde bulunamadı.\n\n" +
                    "Bu firmayı sisteminize yeni bir Tedarikçi Cari olarak otomatik eklemek ister misiniz?",
                    "Yeni Cari Oluşturulsun mu?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (res == DialogResult.Yes)
                {
                    Database.Execute(@"
INSERT INTO Accounts (Name, Type, Phone, TaxOffice, TaxNumber, Address, PriceGroup, DefaultDiscountPercent, BalanceLimit, IsActive)
VALUES (@n, 'Tedarikçi', '', @to, @tax, @addr, 'Perakende', 0, 0, 1);",
                        ("@n", name),
                        ("@to", (object?)_parsedResult?.SupplierTaxOffice ?? DBNull.Value),
                        ("@tax", (object?)taxNumber ?? DBNull.Value),
                        ("@addr", (object?)_parsedResult?.SupplierAddress ?? DBNull.Value)
                    );

                    LoadCombos();
                    AutoMatchAccount(taxNumber, name);
                }
            }
        }
        catch { }
    }

    private void QuickCreateAccount()
    {
        using var accForm = new AccountForm();
        if (accForm.ShowDialog() == DialogResult.OK)
        {
            LoadCombos();
            if (!string.IsNullOrWhiteSpace(_parsedResult?.SupplierName))
            {
                AutoMatchAccount(_parsedResult.SupplierTaxNumber, _parsedResult.SupplierName);
            }
        }
    }

    private void QuickCreateWarehouse()
    {
        using var whForm = new WarehouseManageDialog();
        whForm.ShowDialog();
        LoadCombos();
    }

    /// <summary>
    /// Barkodu boş olan ürünlere otomatik benzersiz EAN-13 mağaza iç barkodu üretir.
    /// </summary>
    private void GenerateMissingBarcodes(bool silent = false)
    {
        int generatedCount = 0;
        foreach (DataRow row in _itemsTable.Rows)
        {
            string b = row["Barcode"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(b))
            {
                string newBarcode = BarcodeHelper.GenerateUniqueInternalBarcode("200");
                row["Barcode"] = newBarcode;
                string status = row["Status"]?.ToString() ?? "";
                if (status.Contains("Yeni"))
                {
                    row["Status"] = "⚠️ Yeni (İç Barkodlu)";
                }
                generatedCount++;
            }
        }

        if (generatedCount > 0)
        {
            if (!silent)
            {
                MessageBox.Show(
                    $"{generatedCount} adet ürüne dükkanınıza özel benzersiz EAN-13 iç barkod numarası başarıyla atandı!\n\n" +
                    "Ürünler kaydedildiğinde bu barkodlar ürün kartına işlenecek ve barkod basımına hazır olacaktır.",
                    "İç Barkodlar Oluşturuldu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }
        else if (!silent)
        {
            MessageBox.Show(
                "Faturadaki tüm ürünlerin zaten barkod numarası tanımlıdır.",
                "Bilgi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    private void AddManualRow()
    {
        var row = _itemsTable.NewRow();
        row["ProductId"] = DBNull.Value;
        row["LineNo"] = _itemsTable.Rows.Count + 1;
        row["Status"] = "⚠️ Yeni Ürün";
        row["Barcode"] = "";
        row["ItemCode"] = "URN-" + DateTime.Now.ToString("HHmmss");
        row["ItemName"] = "Yeni Ürün";
        row["Quantity"] = 1.0;
        row["Unit"] = "Adet";
        row["UnitPrice"] = 0.0;
        row["OldPurchasePrice"] = 0.0;
        row["PriceDiffText"] = "✨ Yeni Ürün";
        row["OldSalePrice"] = 0.0;
        row["NewSalePrice"] = 0.0;
        row["ActionDecision"] = "Yeni Ürün Kartı Aç";
        row["DiscountPercent"] = 0.0;
        row["DiscountAmount"] = 0.0;
        row["VatPercent"] = 20.0;
        row["VatAmount"] = 0.0;
        row["OtherTaxes"] = 0.0;
        row["LineTotal"] = 0.0;
        _itemsTable.Rows.Add(row);

        RecalculateTotals();
    }

    private void DeleteSelectedRow()
    {
        if (_gridItems.CurrentRow != null && !_gridItems.CurrentRow.IsNewRow)
        {
            _gridItems.Rows.Remove(_gridItems.CurrentRow);
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        double subTotal = 0;
        double vatTotal = 0;

        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;

            string decision = row["ActionDecision"]?.ToString() ?? "";
            // Faturadan çıkarılmış / silinmiş kalemler toplama katılmaz!
            if (decision == "Faturadan Sil" || decision.Contains("Hariç"))
            {
                continue;
            }

            double qty = Convert.ToDouble(row["Quantity"] == DBNull.Value ? 0 : row["Quantity"]);
            double price = Convert.ToDouble(row["UnitPrice"] == DBNull.Value ? 0 : row["UnitPrice"]);
            double discPercent = Convert.ToDouble(row["DiscountPercent"] == DBNull.Value ? 0 : row["DiscountPercent"]);
            double vatPercent = Convert.ToDouble(row["VatPercent"] == DBNull.Value ? 20 : row["VatPercent"]);
            double otherTaxes = Convert.ToDouble(row["OtherTaxes"] == DBNull.Value ? 0 : row["OtherTaxes"]);

            double gross = Math.Round(qty * price, 2);
            double discAmount = Math.Round(gross * (discPercent / 100.0), 2);
            row["DiscountAmount"] = discAmount;

            double netLine = gross - discAmount;
            double lineVat = Math.Round(netLine * (vatPercent / 100.0), 2);
            double lineTotal = netLine; // Mal Hizmet Tutarı = Net Matrah

            row["VatAmount"] = lineVat;
            row["LineTotal"] = lineTotal;

            subTotal += netLine;
            vatTotal += lineVat;
        }

        // Eğer faturadan genel toplam ve matrah çekildiyse ve tablodaki satırlar boşsa veya toplam 0 ise faturadakini koru
        if (subTotal == 0 && _parsedResult != null && _parsedResult.SubTotal > 0)
        {
            subTotal = _parsedResult.SubTotal;
            vatTotal = _parsedResult.VatTotal;
        }

        double grandTotal = subTotal + vatTotal;

        _lblSubTotal.Text = subTotal.ToString("N2") + " ₺";
        _lblVatTotal.Text = vatTotal.ToString("N2") + " ₺";
        _lblGrandTotal.Text = grandTotal.ToString("N2") + " ₺";

        // Tutar Sağlama Uyarısı (Fatura genel toplamı ile kalemlerin toplamı uyuşmazsa)
        if (_parsedResult != null && _parsedResult.GrandTotal > 0 && Math.Abs(_parsedResult.GrandTotal - grandTotal) > 1.0 && _itemsTable.Rows.Count > 0)
        {
            _pnlReconcileWarning.Visible = true;
            _lblReconcileText.Text = $"⚠️ SAĞLAMA UYARISI: Fatura Belge Tutarı ({_parsedResult.GrandTotal:N2} ₺) ile tablodaki kalemler toplamı ({grandTotal:N2} ₺) uyuşmuyor! Kalemleri veya 'Metinden Kalem Çıkar' sihirbazını kontrol ediniz.";
        }
        else
        {
            _pnlReconcileWarning.Visible = false;
        }
    }

    private void OpenOriginalPdf()
    {
        if (!string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            FileLauncherHelper.OpenDocument(_selectedFilePath, "E-Fatura PDF Belgesi");
        }
        else
        {
            MessageBox.Show("Açılacak bir PDF dosyası henüz seçilmedi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveInvoiceClick()
    {
        // Doğrulamalar
        if (string.IsNullOrWhiteSpace(_txtInvoiceNo.Text))
        {
            MessageBox.Show("Lütfen fatura numarasını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbAccount.SelectedItem is not ComboBoxItem accItem)
        {
            MessageBox.Show("Lütfen faturanın ait olduğu tedarikçi cariyi seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbWarehouse.SelectedItem is not ComboBoxItem wItem)
        {
            MessageBox.Show("Lütfen ürünlerin gireceği hedef depoyu seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_itemsTable.Rows.Count == 0)
        {
            MessageBox.Show("Faturada en az 1 adet ürün kalemi olmalıdır.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Barkodu olmayan ürünler için kullanıcıya oluşturulsun mu diye sor
        bool hasMissingBarcode = false;
        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;
            string b = row["Barcode"]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(b))
            {
                hasMissingBarcode = true;
                break;
            }
        }

        if (hasMissingBarcode)
        {
            var askBarcode = MessageBox.Show(
                "Faturadaki bazı ürünlerde barkod numarası bulunmuyor.\n\n" +
                "Barkodu olmayan bu ürünler için dükkanınıza özel otomatik iç barkod (örn: 200...) üretilsin mi?\n\n" +
                "• [Evet]: Eksik barkodları otomatik üretip kaydeder.\n" +
                "• [Hayır]: Barkodsuz olarak kayda devam eder.\n" +
                "• [İptal]: Kayıt işlemini durdurur.",
                "Barkod Oluşturulsun mu?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (askBarcode == DialogResult.Cancel)
            {
                return;
            }
            if (askBarcode == DialogResult.Yes)
            {
                GenerateMissingBarcodes(silent: true);
            }
        }

        // Invoice nesnesini doldur
        var invoice = new Invoice
        {
            InvoiceNumber = _txtInvoiceNo.Text.Trim(),
            InvoiceDate = _dtpDate.Value,
            InvoiceType = "Alış Faturası",
            AccountId = accItem.Id,
            AccountName = accItem.Text,
            WarehouseId = wItem.Id,
            WarehouseName = wItem.Text,
            PdfPath = _selectedFilePath,
            Note = _txtNote.Text.Trim(),
            CustomizationId = _parsedResult?.CustomizationId,
            Scenario = _parsedResult?.Scenario,
            InvoiceKind = _parsedResult?.InvoiceKind,
            OrderNumber = _parsedResult?.OrderNumber,
            OrderDate = _parsedResult?.OrderDate,
            RelatedStore = _parsedResult?.RelatedStore,
            CargoId = _parsedResult?.CargoId,
            ReferenceNo = _parsedResult?.ReferenceNo,
            IssueTime = _parsedResult?.IssueTime,
            CreatedAt = DateTime.Now
        };

        double subTotal = 0;
        double vatTotal = 0;
        int rowIdx = 1;

        foreach (DataRow row in _itemsTable.Rows)
        {
            if (row.RowState == DataRowState.Deleted) continue;

            string decision = row["ActionDecision"]?.ToString() ?? "Alış Fiyatını Güncelle";
            // Eğer kullanıcı bu kalemi faturadan hariç tuttuysa faturaya ve stoğa sokma
            if (decision == "Faturadan Sil" || decision.Contains("Hariç"))
            {
                continue;
            }

            double qty = Convert.ToDouble(row["Quantity"] == DBNull.Value ? 0 : row["Quantity"]);
            double price = Convert.ToDouble(row["UnitPrice"] == DBNull.Value ? 0 : row["UnitPrice"]);
            double discPercent = Convert.ToDouble(row["DiscountPercent"] == DBNull.Value ? 0 : row["DiscountPercent"]);
            double discAmount = Convert.ToDouble(row["DiscountAmount"] == DBNull.Value ? 0 : row["DiscountAmount"]);
            double vatPercent = Convert.ToDouble(row["VatPercent"] == DBNull.Value ? 0 : row["VatPercent"]);
            double vatAmount = Convert.ToDouble(row["VatAmount"] == DBNull.Value ? 0 : row["VatAmount"]);
            double otherTaxes = Convert.ToDouble(row["OtherTaxes"] == DBNull.Value ? 0 : row["OtherTaxes"]);
            double lineTotal = Convert.ToDouble(row["LineTotal"] == DBNull.Value ? 0 : row["LineTotal"]);
            double newSale = Convert.ToDouble(row["NewSalePrice"] == DBNull.Value ? 0 : row["NewSalePrice"]);

            subTotal += (qty * price) - discAmount;
            vatTotal += vatAmount;

            invoice.Items.Add(new InvoiceItem
            {
                ProductId = row["ProductId"] != DBNull.Value ? Convert.ToInt64(row["ProductId"]) : null,
                LineNo = rowIdx++,
                Barcode = row["Barcode"]?.ToString()?.Trim(),
                ItemCode = row["ItemCode"]?.ToString()?.Trim(),
                ItemName = row["ItemName"]?.ToString()?.Trim() ?? "Ürün",
                Quantity = qty > 0 ? qty : 1,
                Unit = row["Unit"]?.ToString() ?? "Adet",
                UnitPrice = price,
                DiscountPercent = discPercent,
                DiscountAmount = discAmount,
                VatPercent = vatPercent,
                VatAmount = vatAmount,
                OtherTaxes = otherTaxes,
                LineTotal = lineTotal,
                ActionDecision = decision,
                NewSalePrice = newSale
            });
        }

        invoice.SubTotal = subTotal;
        invoice.VatTotal = vatTotal;
        invoice.GrandTotal = subTotal + vatTotal;

        // Mükerrer Fatura Kontrolü ve Güncelleme İşlemi (eski fatura, yenisi kaydedilirken aynı transaction içinde geri alınır)
        long? replaceInvoiceId = null;
        if (_targetUpdateInvoiceId.HasValue && _targetUpdateInvoiceId.Value > 0)
        {
            replaceInvoiceId = _targetUpdateInvoiceId.Value;
        }
        else
        {
            var existingBeforeSave = InvoiceService.GetInvoiceByNumber(invoice.InvoiceNumber);
            if (existingBeforeSave != null)
            {
                long existId = Convert.ToInt64(existingBeforeSave["Id"]);
                var askSave = MessageBox.Show(
                    $"'{invoice.InvoiceNumber}' numaralı fatura zaten sistemde kayıtlı!\n\n" +
                    "Mevcut faturanın üzerine yazarak güncellemek istiyor musunuz?",
                    "Mükerrer Fatura",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (askSave == DialogResult.Yes)
                {
                    replaceInvoiceId = existId;
                }
                else
                {
                    return; // İptal
                }
            }
        }

        try
        {
            InvoiceService.SaveInvoice(invoice, _chkUpdateStock.Checked, _chkUpdateAccount.Checked, replaceInvoiceId);

            MessageBox.Show(
                $"Fatura ({invoice.InvoiceNumber}) başarıyla sisteme kaydedildi ve işlendi!\n\n" +
                $"Tedarikçi: {invoice.AccountName}\n" +
                $"Hedef Depo: {invoice.WarehouseName}\n" +
                $"Kalem Sayısı: {invoice.Items.Count}\n" +
                $"Genel Toplam: {invoice.GrandTotal:N2} ₺\n\n" +
                (_chkUpdateStock.Checked ? "✅ Ürünler hedef depoya stok girişi yapıldı.\n" : "") +
                (_chkUpdateStock.Checked ? "✅ Sistemde olmayan ürünler için otomatik stok kartı ve barkod oluşturuldu.\n" : "") +
                (_chkUpdateAccount.Checked ? "✅ Tedarikçi cari ekstresine alış kaydı düşüldü.\n" : "") +
                "✅ PDF belgesi güvenle arşivlendi.",
                "Fatura ve Stok Girişi Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            if (!string.IsNullOrWhiteSpace(invoice.PdfPath))
                PdfArchiveService.OfferDriveCopy(this, new[] { invoice.PdfPath });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Fatura kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private class ComboBoxItem
    {
        public long Id { get; set; }
        public string Text { get; set; }

        public ComboBoxItem(long id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() => Text;
    }
}
