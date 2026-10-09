using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class BatchInvoiceImportDialog : Form
{
    private readonly List<BatchInvoiceItemModel> _items = new();
    private readonly List<BatchInvoiceLineItemViewModel> _allFlatItems = new();

    // Sekmeler
    private readonly TabControl _mainTabs = new();
    private readonly TabPage _tabInvoices = new() { Text = "📋 Faturalar & Kalem Detayları (Meta Bilgiler Dahil)" };
    private readonly TabPage _tabAllProducts = new() { Text = "📦 Tüm Taranan Ürünler (Aktarılan / Aktarılmayan / Stok Değişimi)" };

    // Fatura Grid & Detay
    private readonly SplitContainer _splitContainer = new();
    private readonly DataGridView _gridInvoices = new();
    private readonly DataGridView _gridDetailItems = new();

    // Üstte / Altta Bilgi Panelleri
    private readonly Panel _pnlInvoiceHeaderInfo = new();
    private readonly Label _lblDetailHeader = new();
    private readonly Label _lblInvoiceMetaBadge = new();
    private readonly Label _lblWarningBanner = new();

    // Okunamayan Fatura Teşhis Kartı
    private readonly Panel _pnlDiagnosticFailed = new();
    private readonly Label _lblDiagFileName = new();
    private readonly Label _lblDiagReason = new();
    private readonly Label _lblDiagAdvice = new();

    // Tüm Ürünler Sekmesi Kontrolleri
    private readonly DataGridView _gridAllProducts = new();
    private readonly TextBox _txtSearchAllProducts = new();
    private readonly ComboBox _cmbAllProductStatusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblAllProductsSummary = new();

    // İlerleme Çubuğu & Durum
    private readonly Panel _pnlProgress = new() { Visible = false, Height = 42, Dock = DockStyle.Bottom, BackColor = UITheme.CardBg, Padding = new Padding(8, 2, 8, 2) };
    private readonly ProgressBar _progressBar = new() { Height = 16, Dock = DockStyle.Bottom };
    private readonly Label _lblProgress = new() { Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = UITheme.Primary, Dock = DockStyle.Top, Height = 20 };

    // Sayaç Kartları
    private readonly Label _lblTotalCount = new() { Text = "0", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = UITheme.TextPrimary, AutoSize = true };
    private readonly Label _lblSuccessCount = new() { Text = "0", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), AutoSize = true };
    private readonly Label _lblWarningCount = new() { Text = "0", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(217, 119, 6), AutoSize = true };
    private readonly Label _lblFailedCount = new() { Text = "0", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(239, 68, 68), AutoSize = true };
    private readonly Label _lblTotalAmount = new() { Text = "0,00 ₺", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };

    // Filtre Butonları
    private Button _btnFilterAll = null!;
    private Button _btnFilterSuccess = null!;
    private Button _btnFilterWarning = null!;
    private Button _btnFilterFailed = null!;
    private string _currentFilter = "ALL";

    // Seçenekler & Depo
    private readonly ComboBox _cmbWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly CheckBox _chkUpdateStock = new() { Text = "Stoklara giriş yapılsın (Depo Bakiyesi Artar)", Checked = true, AutoSize = true, Font = UITheme.BoldFont };
    private readonly CheckBox _chkUpdateAccount = new() { Text = "Cari bakiyeye işlensin (Tedarikçi Borcu)", Checked = true, AutoSize = true };
    private readonly CheckBox _chkAutoCreateProducts = new() { Text = "Yeni ürünler için otomatik stok kartı açılsın", Checked = true, AutoSize = true };

    private CancellationTokenSource? _cts;
    private bool _isProcessing = false;

    public BatchInvoiceImportDialog()
    {
        Text = "📂 Toplu PDF Fatura İçe Aktarma, Stok Girişi & Teşhis Merkezi";
        ClientSize = new Size(1380, 860);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterParent;
        Icon = AppResources.AppIcon;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        AllowDrop = true;

        DragEnter += BatchInvoiceImportDialog_DragEnter;
        DragDrop += BatchInvoiceImportDialog_DragDrop;

        BuildUI();
        LoadWarehouses();
    }

    private void BuildUI()
    {
        var mainContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };
        Controls.Add(mainContainer);

        // 1. ÜST BİLGİ & SAYAÇ KARTLARI
        var topSection = new Panel { Dock = DockStyle.Top, Height = 105 };
        mainContainer.Controls.Add(topSection);

        var headerPnl = new Panel { Dock = DockStyle.Top, Height = 34 };
        var lblTitle = new Label
        {
            Text = "📂 Toplu Çoklu PDF Fatura İçe Aktarma & Stok Entegrasyon Merkezi",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = UITheme.Primary,
            Dock = DockStyle.Left,
            AutoSize = true
        };
        var lblSub = new Label
        {
            Text = "Çoklu faturaları yükleyin, eksikleri/uyarıları inceleyin, meta verileri kontrol edin ve stoklara sorunsuz giriş yapın.",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            Location = new Point(0, 18),
            AutoSize = true
        };
        headerPnl.Controls.Add(lblSub);
        headerPnl.Controls.Add(lblTitle);
        topSection.Controls.Add(headerPnl);

        // Sayaç Kartları (Taşma yapmayan esnek yatay akış)
        var cardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        topSection.Controls.Add(cardsFlow);

        cardsFlow.Controls.Add(CreateStatCard("Toplam Belge", _lblTotalCount, UITheme.CardBg, UITheme.Primary));
        cardsFlow.Controls.Add(CreateStatCard("🟢 Başarılı / Tam", _lblSuccessCount, Color.FromArgb(240, 253, 244), Color.FromArgb(16, 185, 129)));
        cardsFlow.Controls.Add(CreateStatCard("🟡 Uyarı / Eksik", _lblWarningCount, Color.FromArgb(254, 252, 232), Color.FromArgb(217, 119, 6)));
        cardsFlow.Controls.Add(CreateStatCard("🔴 Okunamadı", _lblFailedCount, Color.FromArgb(254, 242, 242), Color.FromArgb(239, 68, 68)));
        cardsFlow.Controls.Add(CreateStatCard("💰 Toplam Tutar", _lblTotalAmount, Color.FromArgb(238, 242, 255), Color.FromArgb(79, 70, 229)));

        // 2. AKSİYON VE FİLTRE ÇUBUĞU (2 SATIRLI ESNEK DÜZEN - ÇAKIŞMA KESİNLİKLE İMKANSIZ)
        var actionToolBar = new Panel { Dock = DockStyle.Top, Height = 74, Padding = new Padding(0, 2, 0, 2) };
        mainContainer.Controls.Add(actionToolBar);

        // Satır 1: Dosya / Klasör Seçme Butonları
        var row1Files = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 35,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        actionToolBar.Controls.Add(row1Files);

        var btnAddFiles = UITheme.CreateButton("➕ Çoklu PDF / XML Seç...", UITheme.Primary, Color.White, (s, e) => PickFilesClick(), 175, 30);
        var btnAddFolder = UITheme.CreateButton("📁 Klasörden Toplu Tara...", Color.FromArgb(14, 165, 233), Color.White, (s, e) => PickFolderClick(), 175, 30);
        var btnClear = UITheme.CreateButton("🧹 Listeyi Temizle", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => ClearListClick(), 125, 30);

        row1Files.Controls.Add(btnAddFiles);
        row1Files.Controls.Add(btnAddFolder);
        row1Files.Controls.Add(btnClear);

        // Satır 2: Filtreleme Butonları (Geniş, Ferah ve Parantez İçinde Canlı Sayaçlı)
        var row2Filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 35,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        actionToolBar.Controls.Add(row2Filters);

        _btnFilterAll = UITheme.CreateButton("📋 Tümü (0)", Color.FromArgb(51, 65, 85), Color.White, (s, e) => SetFilter("ALL"), 105, 30);
        _btnFilterSuccess = UITheme.CreateButton("🟢 Sorunsuz Başarılı (0)", Color.White, Color.FromArgb(16, 185, 129), (s, e) => SetFilter("SUCCESS"), 170, 30);
        _btnFilterWarning = UITheme.CreateButton("🟡 UYARI / EKSİKLER (0)", Color.White, Color.FromArgb(217, 119, 6), (s, e) => SetFilter("WARNING"), 175, 30);
        _btnFilterFailed = UITheme.CreateButton("🔴 OKUNAMAYANLAR (0)", Color.White, Color.FromArgb(239, 68, 68), (s, e) => SetFilter("FAILED"), 175, 30);

        row2Filters.Controls.Add(new Label { Text = "Hızlı Filtre:", AutoSize = true, Padding = new Padding(0, 6, 6, 0), Font = UITheme.BoldFont });
        row2Filters.Controls.Add(_btnFilterAll);
        row2Filters.Controls.Add(_btnFilterSuccess);
        row2Filters.Controls.Add(_btnFilterWarning);
        row2Filters.Controls.Add(_btnFilterFailed);

        // 3. ALT İŞLEM PANELİ (DÜZENLİ, FERAH VE İKİ BÖLMELİ)
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 105,
            BackColor = UITheme.CardBg,
            Padding = new Padding(12, 6, 12, 6)
        };
        mainContainer.Controls.Add(bottomPanel);

        // İlerleme paneli (Alt panelin hemen üzerinde)
        _pnlProgress.Controls.Add(_lblProgress);
        _pnlProgress.Controls.Add(_progressBar);
        mainContainer.Controls.Add(_pnlProgress);

        // Alt Satır 1: Ayarlar & Depo
        var rowSettings = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        rowSettings.Controls.Add(new Label { Text = "Giriş Deposu:", AutoSize = true, Padding = new Padding(0, 6, 4, 0), Font = UITheme.BoldFont });
        rowSettings.Controls.Add(_cmbWarehouse);
        rowSettings.Controls.Add(new Panel { Width = 15, Height = 10 });
        rowSettings.Controls.Add(_chkUpdateStock);
        rowSettings.Controls.Add(new Panel { Width = 15, Height = 10 });
        rowSettings.Controls.Add(_chkUpdateAccount);
        rowSettings.Controls.Add(new Panel { Width = 15, Height = 10 });
        rowSettings.Controls.Add(_chkAutoCreateProducts);
        bottomPanel.Controls.Add(rowSettings);

        // Alt Satır 2: İşlem Butonları (TableLayoutPanel: Sol %65, Sağ %35)
        var rowActionsTable = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        rowActionsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65f));
        rowActionsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
        bottomPanel.Controls.Add(rowActionsTable);

        var pnlLeftTools = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        var btnOpenPdf = UITheme.CreateButton("📄 Seçili PDF'i Aç", Color.FromArgb(71, 85, 105), Color.White, (s, e) => OpenSelectedPdf(), 140, 38);
        var btnExportSingle = UITheme.CreateButton("💾 O PDF'i Dışa Aktar", Color.FromArgb(100, 116, 139), Color.White, (s, e) => ExportSelectedPdf(), 150, 38);
        var btnExportFailedAll = UITheme.CreateButton("📁 Okunamayanları Klasöre Topla", Color.FromArgb(239, 68, 68), Color.White, (s, e) => ExportAllFailedPdfs(), 230, 38);
        var btnEditSingle = UITheme.CreateButton("✏️ Faturayı Düzenle / Tamamla", Color.FromArgb(16, 185, 129), Color.White, (s, e) => EditSelectedInvoice(), 200, 38);

        pnlLeftTools.Controls.Add(btnOpenPdf);
        pnlLeftTools.Controls.Add(btnExportSingle);
        pnlLeftTools.Controls.Add(btnExportFailedAll);
        pnlLeftTools.Controls.Add(btnEditSingle);
        rowActionsTable.Controls.Add(pnlLeftTools, 0, 0);

        var btnBulkSave = UITheme.CreateButton("🚀 SEÇİLİ FATURALARI SİSTEME VE STOKLARA AKTAR", UITheme.Primary, Color.White, (s, e) => BulkSaveInvoicesClick(), 360, 42);
        btnBulkSave.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        btnBulkSave.Dock = DockStyle.Right;
        rowActionsTable.Controls.Add(btnBulkSave, 1, 0);

        // 4. ORTA ALAN: TAB CONTROL
        _mainTabs.Dock = DockStyle.Fill;
        _mainTabs.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _mainTabs.TabPages.Add(_tabInvoices);
        _mainTabs.TabPages.Add(_tabAllProducts);
        mainContainer.Controls.Add(_mainTabs);

        BuildInvoicesTabPage();
        BuildAllProductsTabPage();
    }

    private void BuildInvoicesTabPage()
    {
        _splitContainer.Dock = DockStyle.Fill;
        _splitContainer.Orientation = Orientation.Horizontal;
        _splitContainer.SplitterDistance = 320;
        _splitContainer.SplitterWidth = 6;
        _splitContainer.BackColor = UITheme.BorderColor;

        _tabInvoices.Controls.Add(_splitContainer);

        // Üst Bölüm: Fatura Listesi Tablosu
        var pnlTopGrid = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.CardBg };
        _splitContainer.Panel1.Controls.Add(pnlTopGrid);
        SetupInvoicesGrid();
        pnlTopGrid.Controls.Add(_gridInvoices);

        // Alt Bölüm: Seçili Fatura Detayı & Uyarı / Meta Kartı
        var pnlBottomDetail = new Panel { Dock = DockStyle.Fill, BackColor = UITheme.CardBg, Padding = new Padding(6) };
        _splitContainer.Panel2.Controls.Add(pnlBottomDetail);

        // Bilgi Başlığı & Meta Rozeti Paneli
        _pnlInvoiceHeaderInfo.Dock = DockStyle.Top;
        _pnlInvoiceHeaderInfo.Height = 62;
        _pnlInvoiceHeaderInfo.BackColor = UITheme.CardBg;

        _lblDetailHeader.Dock = DockStyle.Top;
        _lblDetailHeader.Height = 22;
        _lblDetailHeader.Font = UITheme.BoldFont;
        _lblDetailHeader.ForeColor = UITheme.TextPrimary;
        _lblDetailHeader.Text = "Detaylarını görmek için yukarıdaki tablodan bir fatura seçiniz.";

        _lblInvoiceMetaBadge.Dock = DockStyle.Top;
        _lblInvoiceMetaBadge.Height = 20;
        _lblInvoiceMetaBadge.Font = UITheme.SmallFont;
        _lblInvoiceMetaBadge.ForeColor = Color.FromArgb(79, 70, 229);
        _lblInvoiceMetaBadge.Text = "";

        _lblWarningBanner.Dock = DockStyle.Bottom;
        _lblWarningBanner.Height = 20;
        _lblWarningBanner.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        _lblWarningBanner.ForeColor = Color.FromArgb(185, 28, 28);
        _lblWarningBanner.Text = "";

        _pnlInvoiceHeaderInfo.Controls.Add(_lblWarningBanner);
        _pnlInvoiceHeaderInfo.Controls.Add(_lblInvoiceMetaBadge);
        _pnlInvoiceHeaderInfo.Controls.Add(_lblDetailHeader);
        pnlBottomDetail.Controls.Add(_pnlInvoiceHeaderInfo);

        // Kalem Tablosu
        SetupDetailGrid();
        pnlBottomDetail.Controls.Add(_gridDetailItems);

        // Okunamayan Fatura Teşhis Kartı
        SetupDiagnosticPanel();
        pnlBottomDetail.Controls.Add(_pnlDiagnosticFailed);

        _gridDetailItems.BringToFront();
    }

    private void SetupDiagnosticPanel()
    {
        _pnlDiagnosticFailed.Dock = DockStyle.Fill;
        _pnlDiagnosticFailed.BackColor = Color.FromArgb(254, 242, 242);
        _pnlDiagnosticFailed.Padding = new Padding(16);
        _pnlDiagnosticFailed.Visible = false;

        var lblDiagTitle = new Label
        {
            Text = "🔴 BU PDF FATURA AYRIŞTIRILAMADI (OKUNAMAYAN DOSYA)",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38),
            Dock = DockStyle.Top,
            Height = 28
        };

        _lblDiagFileName.Dock = DockStyle.Top;
        _lblDiagFileName.Height = 24;
        _lblDiagFileName.Font = UITheme.BoldFont;
        _lblDiagFileName.ForeColor = UITheme.TextPrimary;

        _lblDiagReason.Dock = DockStyle.Top;
        _lblDiagReason.Height = 44;
        _lblDiagReason.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _lblDiagReason.ForeColor = Color.FromArgb(185, 28, 28);

        _lblDiagAdvice.Dock = DockStyle.Top;
        _lblDiagAdvice.Height = 32;
        _lblDiagAdvice.Font = UITheme.SmallFont;
        _lblDiagAdvice.ForeColor = UITheme.TextSecondary;
        _lblDiagAdvice.Text = "💡 Bu dosya taranmış bir resim (OCR gerekli) olabilir, bozuk olabilir veya şablonu desteklenmiyor olabilir. Aşağıdaki butonlarla PDF'i doğrudan açabilir veya manuel tamamlayabilirsiniz:";

        var flowDiagBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            FlowDirection = FlowDirection.LeftToRight
        };

        var btnDiagOpen = UITheme.CreateButton("📄 Bu PDF Belgesini Aç / İncele", Color.FromArgb(220, 38, 38), Color.White, (s, e) => OpenSelectedPdf(), 220, 34);
        var btnDiagExport = UITheme.CreateButton("💾 Bu PDF'i Farklı Kaydet", Color.FromArgb(100, 116, 139), Color.White, (s, e) => ExportSelectedPdf(), 190, 34);
        var btnDiagManual = UITheme.CreateButton("✏️ Faturayı Manuel Girişte Aç", Color.FromArgb(16, 185, 129), Color.White, (s, e) => EditSelectedInvoice(), 210, 34);

        flowDiagBtns.Controls.Add(btnDiagOpen);
        flowDiagBtns.Controls.Add(btnDiagExport);
        flowDiagBtns.Controls.Add(btnDiagManual);

        _pnlDiagnosticFailed.Controls.Add(flowDiagBtns);
        _pnlDiagnosticFailed.Controls.Add(_lblDiagAdvice);
        _pnlDiagnosticFailed.Controls.Add(_lblDiagReason);
        _pnlDiagnosticFailed.Controls.Add(_lblDiagFileName);
        _pnlDiagnosticFailed.Controls.Add(lblDiagTitle);
    }

    private void BuildAllProductsTabPage()
    {
        var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(8, 6, 8, 6), BackColor = UITheme.CardBg };
        _tabAllProducts.Controls.Add(pnlHeader);

        _txtSearchAllProducts.Width = 240;
        _txtSearchAllProducts.PlaceholderText = "🔍 Ürün adı veya barkod ara...";
        _txtSearchAllProducts.TextChanged += (s, e) => RefreshAllProductsGrid();

        _cmbAllProductStatusFilter.Width = 190;
        _cmbAllProductStatusFilter.Items.AddRange(new object[] { "Tüm Ürünler", "✅ Sadece Aktarılanlar", "⏳ Aktarılmayı Bekleyenler" });
        _cmbAllProductStatusFilter.SelectedIndex = 0;
        _cmbAllProductStatusFilter.SelectedIndexChanged += (s, e) => RefreshAllProductsGrid();

        _lblAllProductsSummary.AutoSize = true;
        _lblAllProductsSummary.Font = UITheme.BoldFont;
        _lblAllProductsSummary.ForeColor = UITheme.Primary;
        _lblAllProductsSummary.Padding = new Padding(12, 6, 0, 0);
        _lblAllProductsSummary.Text = "Toplam 0 Kalem Ürün";

        var flowLeft = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        flowLeft.Controls.Add(_txtSearchAllProducts);
        flowLeft.Controls.Add(new Label { Text = "Filtre:", AutoSize = true, Padding = new Padding(8, 6, 4, 0), Font = UITheme.BoldFont });
        flowLeft.Controls.Add(_cmbAllProductStatusFilter);
        flowLeft.Controls.Add(_lblAllProductsSummary);
        pnlHeader.Controls.Add(flowLeft);

        SetupAllProductsGrid();
        _tabAllProducts.Controls.Add(_gridAllProducts);
        _gridAllProducts.BringToFront();
    }

    private Control CreateStatCard(string title, Label lblValue, Color bg, Color border)
    {
        var card = new Panel
        {
            Width = 180,
            Height = 58,
            BackColor = bg,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(8, 4, 8, 4)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(border, 1.2f);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var lblT = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 16
        };
        lblValue.Dock = DockStyle.Bottom;
        lblValue.Height = 28;

        card.Controls.Add(lblT);
        card.Controls.Add(lblValue);
        return card;
    }

    private void SetupInvoicesGrid()
    {
        _gridInvoices.Dock = DockStyle.Fill;
        _gridInvoices.BackgroundColor = UITheme.CardBg;
        _gridInvoices.BorderStyle = BorderStyle.None;
        _gridInvoices.RowHeadersVisible = false;
        _gridInvoices.AllowUserToAddRows = false;
        _gridInvoices.AllowUserToDeleteRows = false;
        _gridInvoices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridInvoices.MultiSelect = false;
        _gridInvoices.RowTemplate.Height = 32;
        _gridInvoices.Font = UITheme.RegularFont;
        _gridInvoices.AutoGenerateColumns = false;

        // Kolonlar (Kullanıcının talep ettiği meta veriler ve eksiklikler dahil)
        var colCheck = new DataGridViewCheckBoxColumn { Name = "IsSelected", HeaderText = "Seç", Width = 40 };
        var colBadge = new DataGridViewTextBoxColumn { Name = "StatusBadge", HeaderText = "Durum", Width = 110 };
        var colOpenBtn = new DataGridViewButtonColumn { Name = "OpenPdfBtn", HeaderText = "PDF", Text = "📄 Aç", UseColumnTextForButtonValue = true, Width = 55 };
        var colFile = new DataGridViewTextBoxColumn { Name = "FileName", HeaderText = "Dosya Adı", Width = 150 };
        var colInvNo = new DataGridViewTextBoxColumn { Name = "InvoiceNumber", HeaderText = "Fatura No", Width = 125 };
        var colDate = new DataGridViewTextBoxColumn { Name = "InvoiceDate", HeaderText = "Tarih", Width = 85 };
        var colTime = new DataGridViewTextBoxColumn { Name = "IssueTime", HeaderText = "Saat", Width = 65 };
        var colScenario = new DataGridViewTextBoxColumn { Name = "Scenario", HeaderText = "Senaryo", Width = 95 };
        var colKind = new DataGridViewTextBoxColumn { Name = "InvoiceKind", HeaderText = "Tür", Width = 75 };
        var colSupplier = new DataGridViewTextBoxColumn { Name = "SupplierName", HeaderText = "Tedarikçi", Width = 160 };
        var colTaxNo = new DataGridViewTextBoxColumn { Name = "SupplierTaxNo", HeaderText = "VKN / TCKN", Width = 95 };
        var colTaxOffice = new DataGridViewTextBoxColumn { Name = "SupplierTaxOffice", HeaderText = "Vergi Dairesi", Width = 95 };
        var colEttn = new DataGridViewTextBoxColumn { Name = "ReferenceNo", HeaderText = "ETTN / UUID", Width = 120 };
        var colItems = new DataGridViewTextBoxColumn { Name = "ItemCount", HeaderText = "Kalem", Width = 55 };
        var colSubTotal = new DataGridViewTextBoxColumn { Name = "SubTotal", HeaderText = "Ara Toplam", Width = 95 };
        var colVat = new DataGridViewTextBoxColumn { Name = "VatTotal", HeaderText = "KDV", Width = 80 };
        var colTotal = new DataGridViewTextBoxColumn { Name = "GrandTotal", HeaderText = "Genel Toplam", Width = 105 };
        var colDiag = new DataGridViewTextBoxColumn { Name = "DiagnosticMessage", HeaderText = "⚠️ Eksikler, Uyarılar & Teşhis Bilgisi", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };

        _gridInvoices.Columns.AddRange(
            colCheck, colBadge, colOpenBtn, colFile, colInvNo, colDate, colTime,
            colScenario, colKind, colSupplier, colTaxNo, colTaxOffice, colEttn,
            colItems, colSubTotal, colVat, colTotal, colDiag
        );

        _gridInvoices.CellFormatting += GridInvoices_CellFormatting;
        _gridInvoices.SelectionChanged += (s, e) => ShowSelectedInvoiceItems();
        _gridInvoices.CellContentClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _gridInvoices.Columns[e.ColumnIndex].Name == "OpenPdfBtn")
            {
                OpenSelectedPdf();
            }
        };
        _gridInvoices.CellDoubleClick += (s, e) =>
        {
            var item = GetSelectedItem();
            if (item != null)
            {
                if (item.Status == BatchInvoiceStatus.Failed) OpenSelectedPdf();
                else EditSelectedInvoice();
            }
        };

        var ctx = new ContextMenuStrip();
        ctx.Items.Add("📄 PDF Belgesini Aç / İncele", null, (s, e) => OpenSelectedPdf());
        ctx.Items.Add("💾 O PDF'i Dışa Aktar...", null, (s, e) => ExportSelectedPdf());
        ctx.Items.Add("📁 Klasörde Göster", null, (s, e) => ShowInExplorer());
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add("✏️ Faturayı Düzenle / İncele", null, (s, e) => EditSelectedInvoice());
        _gridInvoices.ContextMenuStrip = ctx;
    }

    private void SetupDetailGrid()
    {
        _gridDetailItems.Dock = DockStyle.Fill;
        _gridDetailItems.BackgroundColor = UITheme.CardBg;
        _gridDetailItems.BorderStyle = BorderStyle.None;
        _gridDetailItems.RowHeadersVisible = false;
        _gridDetailItems.AllowUserToAddRows = false;
        _gridDetailItems.AllowUserToDeleteRows = false;
        _gridDetailItems.ReadOnly = true;
        _gridDetailItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridDetailItems.RowTemplate.Height = 26;
        _gridDetailItems.Font = new Font("Segoe UI", 9f);
        _gridDetailItems.AutoGenerateColumns = false;

        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ImportStatusBadge", HeaderText = "Aktarım Durumu", Width = 135 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineNo", HeaderText = "Sıra", Width = 40 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Barcode", HeaderText = "Barkod", Width = 110 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemCode", HeaderText = "Ürün Kodu", Width = 95 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Ürün Adı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Giren Miktar", Width = 85 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Birim", Width = 55 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Birim Fiyat", Width = 90 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "VatPercent", HeaderText = "KDV %", Width = 55 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineTotal", HeaderText = "Tutar", Width = 95 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "MatchStatus", HeaderText = "Stok Eşleşmesi", Width = 160 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentStock", HeaderText = "Mevcut Stok", Width = 85 });
        _gridDetailItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProjectedStock", HeaderText = "Yeni Stok (Giriş Sonrası)", Width = 125 });

        _gridDetailItems.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _gridDetailItems.Rows.Count) return;
            var colName = _gridDetailItems.Columns[e.ColumnIndex].Name;

            if (colName == "ImportStatusBadge" && e.Value != null)
            {
                string sVal = e.Value.ToString() ?? "";
                if (sVal.Contains("Aktarıldı"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                    e.CellStyle.Font = new Font(_gridDetailItems.Font, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6);
                }
            }
            else if (colName == "ProjectedStock")
            {
                e.CellStyle.ForeColor = Color.FromArgb(79, 70, 229);
                e.CellStyle.Font = new Font(_gridDetailItems.Font, FontStyle.Bold);
            }
            else if (colName == "UnitPrice" || colName == "LineTotal")
            {
                if (e.Value is double d)
                {
                    e.Value = d.ToString("N2") + " ₺";
                    e.FormattingApplied = true;
                }
            }
        };
    }

    private void SetupAllProductsGrid()
    {
        _gridAllProducts.Dock = DockStyle.Fill;
        _gridAllProducts.BackgroundColor = UITheme.CardBg;
        _gridAllProducts.BorderStyle = BorderStyle.None;
        _gridAllProducts.RowHeadersVisible = false;
        _gridAllProducts.AllowUserToAddRows = false;
        _gridAllProducts.AllowUserToDeleteRows = false;
        _gridAllProducts.ReadOnly = true;
        _gridAllProducts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridAllProducts.RowTemplate.Height = 28;
        _gridAllProducts.Font = new Font("Segoe UI", 9f);
        _gridAllProducts.AutoGenerateColumns = false;

        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ImportStatusBadge", HeaderText = "Aktarım Durumu", Width = 135 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "InvoiceNumber", HeaderText = "Fatura No", Width = 120 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "SupplierName", HeaderText = "Tedarikçi", Width = 150 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Barcode", HeaderText = "Barkod", Width = 110 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemCode", HeaderText = "Ürün Kodu", Width = 95 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemName", HeaderText = "Ürün Adı", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Miktar", Width = 65 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "Birim", Width = 55 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Birim Fiyat", Width = 90 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineTotal", HeaderText = "Tutar", Width = 95 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "MatchStatus", HeaderText = "Stok Eşleşmesi", Width = 160 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "CurrentStock", HeaderText = "Mevcut Stok", Width = 85 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProjectedStock", HeaderText = "Giriş Sonrası Stok", Width = 115 });
        _gridAllProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourceFileName", HeaderText = "PDF Dosyası", Width = 130 });

        _gridAllProducts.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _gridAllProducts.Rows.Count) return;
            var colName = _gridAllProducts.Columns[e.ColumnIndex].Name;

            if (colName == "ImportStatusBadge" && e.Value != null)
            {
                string sVal = e.Value.ToString() ?? "";
                if (sVal.Contains("Aktarıldı"))
                {
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                    e.CellStyle.Font = new Font(_gridAllProducts.Font, FontStyle.Bold);
                }
                else
                {
                    e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6);
                }
            }
            else if (colName == "ProjectedStock")
            {
                e.CellStyle.ForeColor = Color.FromArgb(79, 70, 229);
                e.CellStyle.Font = new Font(_gridAllProducts.Font, FontStyle.Bold);
            }
            else if (colName == "UnitPrice" || colName == "LineTotal")
            {
                if (e.Value is double d)
                {
                    e.Value = d.ToString("N2") + " ₺";
                    e.FormattingApplied = true;
                }
            }
        };
    }

    private void GridInvoices_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridInvoices.Rows.Count) return;

        var row = _gridInvoices.Rows[e.RowIndex];
        if (row.DataBoundItem is not BatchInvoiceItemModel item) return;

        if (item.Status == BatchInvoiceStatus.Success)
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
        else if (item.Status == BatchInvoiceStatus.Warning || item.Status == BatchInvoiceStatus.Duplicate)
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 252, 232);
        else if (item.Status == BatchInvoiceStatus.Failed)
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);

        var colName = _gridInvoices.Columns[e.ColumnIndex].Name;
        if ((colName == "GrandTotal" || colName == "SubTotal" || colName == "VatTotal") && e.Value is double grand)
        {
            e.Value = grand.ToString("N2") + " ₺";
            e.FormattingApplied = true;
        }
        else if (colName == "InvoiceDate" && e.Value is DateTime dt)
        {
            e.Value = dt.ToString("dd.MM.yyyy");
            e.FormattingApplied = true;
        }
    }

    private void LoadWarehouses()
    {
        try
        {
            var dt = Database.Query("SELECT Id, Name FROM Warehouses WHERE IsActive=1 ORDER BY IsDefault DESC, Name;");
            _cmbWarehouse.Items.Clear();
            foreach (DataRow r in dt.Rows)
            {
                _cmbWarehouse.Items.Add(new WarehouseComboItem(Convert.ToInt64(r["Id"]), r["Name"].ToString() ?? "Depo"));
            }
            if (_cmbWarehouse.Items.Count > 0) _cmbWarehouse.SelectedIndex = 0;
        }
        catch { }
    }

    private class WarehouseComboItem
    {
        public long Id { get; }
        public string Name { get; }
        public WarehouseComboItem(long id, string name) { Id = id; Name = name; }
        public override string ToString() => Name;
    }

    private void BatchInvoiceImportDialog_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void BatchInvoiceImportDialog_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            var pdfFiles = files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
            if (pdfFiles.Count > 0) ProcessFilesAsync(pdfFiles);
        }
    }

    private void PickFilesClick()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "İçe Aktarılacak PDF / XML Faturaları Seçiniz",
            Filter = "Fatura Dosyaları (*.pdf;*.xml)|*.pdf;*.xml|PDF Dosyaları (*.pdf)|*.pdf|XML Dosyaları (*.xml)|*.xml|Tüm Dosyalar (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog() == DialogResult.OK && ofd.FileNames.Length > 0)
        {
            ProcessFilesAsync(ofd.FileNames);
        }
    }

    private void PickFolderClick()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "İçerisindeki tüm PDF faturaların taranacağı klasörü seçiniz:",
            UseDescriptionForTitle = true
        };

        if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            var files = Directory.GetFiles(fbd.SelectedPath, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
            if (files.Count == 0)
            {
                MessageBox.Show("Seçilen klasörde hiç .pdf uzantılı fatura dosyası bulunamadı.", "PDF Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ProcessFilesAsync(files);
        }
    }

    private void ClearListClick()
    {
        if (_isProcessing)
        {
            _cts?.Cancel();
            return;
        }
        _items.Clear();
        _allFlatItems.Clear();
        RefreshInvoicesGrid();
        RefreshAllProductsGrid();
        UpdateStats();
        _gridDetailItems.DataSource = null;
        _lblDetailHeader.Text = "Detaylarını görmek için yukarıdaki tablodan bir fatura seçiniz.";
        _lblInvoiceMetaBadge.Text = "";
        _lblWarningBanner.Text = "";
        _pnlDiagnosticFailed.Visible = false;
        _gridDetailItems.Visible = true;
    }

    private async void ProcessFilesAsync(IEnumerable<string> filePaths)
    {
        var existingPaths = new HashSet<string>(_items.Select(x => x.FilePath), StringComparer.OrdinalIgnoreCase);
        var newFiles = filePaths.Where(f => !existingPaths.Contains(f) && File.Exists(f)).ToList();

        if (newFiles.Count == 0)
        {
            MessageBox.Show("Seçilen dosyalar zaten listede mevcut.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _isProcessing = true;
        _cts = new CancellationTokenSource();
        _pnlProgress.Visible = true;
        _progressBar.Value = 0;
        _progressBar.Maximum = newFiles.Count;

        int processed = 0;
        int total = newFiles.Count;

        await Task.Run(() =>
        {
            foreach (var file in newFiles)
            {
                if (_cts.IsCancellationRequested) break;

                var model = ParseAndDiagnoseFile(file, _items.Count + 1);

                Invoke(new Action(() =>
                {
                    _items.Add(model);
                    _allFlatItems.AddRange(model.LineItems);
                    processed++;
                    _progressBar.Value = Math.Min(processed, _progressBar.Maximum);
                    _lblProgress.Text = $"Faturalar taranıyor: {processed} / {total} ({Path.GetFileName(file)})...";
                    UpdateStats();
                }));
            }
        });

        _isProcessing = false;
        _pnlProgress.Visible = false;
        RefreshInvoicesGrid();
        RefreshAllProductsGrid();
        UpdateStats();

        int failedCount = _items.Count(x => x.Status == BatchInvoiceStatus.Failed);
        int warningCount = _items.Count(x => x.Status == BatchInvoiceStatus.Warning);

        if (failedCount > 0 || warningCount > 0)
        {
            MessageBox.Show(
                $"Toplu tarama tamamlandı!\n\n" +
                $"• Toplam Taranan: {_items.Count}\n" +
                $"• Sorunsuz Başarılı: {_items.Count(x => x.Status == BatchInvoiceStatus.Success)}\n" +
                $"• 🟡 Eksikli / Uyarılı: {warningCount}\n" +
                $"• 🔴 Okunamayan: {failedCount}\n\n" +
                $"💡 İpucu: Eksikleri ve okunamayanları görmek için yukarıdaki '🟡 UYARI / EKSİKLER' veya '🔴 OKUNAMAYANLAR' filtre butonlarını kullanabilirsiniz.",
                "Toplu Tarama Sonucu",
                MessageBoxButtons.OK,
                failedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
            );
        }
    }

    private BatchInvoiceItemModel ParseAndDiagnoseFile(string filePath, int index)
    {
        var fi = new FileInfo(filePath);
        var model = new BatchInvoiceItemModel
        {
            Index = index,
            FilePath = filePath,
            FileSizeBytes = fi.Exists ? fi.Length : 0
        };

        try
        {
            var res = InvoiceParserService.ParseInvoiceFile(filePath);
            model.ParsedResult = res;
            model.InvoiceNumber = res.InvoiceNumber ?? string.Empty;
            model.InvoiceDate = res.InvoiceDate != default ? res.InvoiceDate : DateTime.Today;
            model.SupplierName = res.SupplierName ?? string.Empty;
            model.SupplierTaxNo = res.SupplierTaxNumber ?? string.Empty;
            model.SupplierTaxOffice = res.SupplierTaxOffice ?? string.Empty;
            model.SupplierAddress = res.SupplierAddress ?? string.Empty;
            model.ReferenceNo = res.ReferenceNo ?? string.Empty;
            model.Scenario = res.Scenario ?? string.Empty;
            model.InvoiceKind = res.InvoiceKind ?? string.Empty;
            model.IssueTime = res.IssueTime ?? string.Empty;
            model.OrderNumber = res.OrderNumber ?? string.Empty;
            model.OrderDate = res.OrderDate ?? string.Empty;

            model.ItemCount = res.Items.Count;
            model.SubTotal = res.SubTotal;
            model.VatTotal = res.VatTotal;
            model.GrandTotal = res.GrandTotal;
            model.ItemsCalculatedTotal = res.Items.Sum(x => x.LineTotal);

            // 1. Mükerrer Kontrolü
            if (!string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                var existing = InvoiceService.GetInvoiceByNumber(model.InvoiceNumber);
                if (existing != null)
                {
                    model.Status = BatchInvoiceStatus.Duplicate;
                    model.DiagnosticMessage = $"⚠️ Bu fatura numarası ({model.InvoiceNumber}) veritabanında zaten kayıtlı!";
                    model.MissingFieldsWarning = "Mükerrer Fatura";
                    model.IsSelected = false;
                }
            }

            // 2. Cari Eşleme Kontrolü
            MatchSupplierAccount(model);

            // 3. Kalemleri ve Eşleşmeleri Çözümle
            if (res.Success && res.Items.Count > 0)
            {
                foreach (var pi in res.Items)
                {
                    var lineVm = new BatchInvoiceLineItemViewModel
                    {
                        LineNo = pi.LineNo,
                        Barcode = pi.Barcode,
                        ItemCode = pi.ItemCode,
                        ItemName = pi.ItemName,
                        Quantity = pi.Quantity,
                        Unit = pi.Unit,
                        UnitPrice = pi.UnitPrice,
                        VatPercent = pi.VatPercent,
                        LineTotal = pi.LineTotal,
                        InvoiceNumber = model.InvoiceNumber,
                        SupplierName = model.SupplierName,
                        InvoiceDate = model.InvoiceDate,
                        SourceFileName = model.FileName,
                        IsImported = false
                    };

                    MatchProductForLine(lineVm);
                    model.LineItems.Add(lineVm);
                }
            }

            // 4. Başarısızlık Teşhisi
            if (!res.Success || res.Items.Count == 0)
            {
                model.Status = BatchInvoiceStatus.Failed;
                model.IsSelected = false;

                if (string.IsNullOrWhiteSpace(res.RawText) || res.RawText.Length < 40)
                {
                    model.DiagnosticMessage = "🔴 Taranmış Resim / Metin Katmanı Yok: Belge resim formatında, metinler okunamadı.";
                    model.MissingFieldsWarning = "Görsel PDF (OCR Gerekli)";
                }
                else if (res.Items.Count == 0)
                {
                    model.DiagnosticMessage = $"🔴 Ürün Tablosu Bulunamadı: Fatura başlığı okundu ancak satır kalemleri algılanamadı ({res.ErrorMessage ?? "Kalem tablosu yok"}).";
                    model.MissingFieldsWarning = "Ürün Tablosu Algılanamadı";
                }
                else
                {
                    model.DiagnosticMessage = $"🔴 Okunamadı: {res.ErrorMessage}";
                    model.MissingFieldsWarning = "Ayrıştırma Başarısız";
                }
                return model;
            }

            // 5. Uyarı & Eksik Alan Tespiti
            if (model.Status != BatchInvoiceStatus.Duplicate)
            {
                var missingList = new List<string>();

                if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
                    missingList.Add("Fatura No Okunamadı");

                if (!model.MatchedAccountId.HasValue)
                    missingList.Add("Tedarikçi Sistemde Yok (Yeni Cari Açılacak)");

                if (string.IsNullOrWhiteSpace(model.SupplierTaxNo))
                    missingList.Add("VKN / TCKN Eksik");

                if (model.GrandTotal > 0 && model.Difference > 1.0)
                    missingList.Add($"Toplam Farkı ({model.Difference:N2} ₺)");

                if (missingList.Count > 0)
                {
                    model.Status = BatchInvoiceStatus.Warning;
                    model.MissingFieldsWarning = string.Join(" • ", missingList);
                    model.DiagnosticMessage = "🟡 Eksikler: " + model.MissingFieldsWarning;
                }
                else
                {
                    model.Status = BatchInvoiceStatus.Success;
                    model.MissingFieldsWarning = "Eksiksiz";
                    model.DiagnosticMessage = $"🟢 {model.ItemCount} kalem ve fatura meta bilgileri eksiksiz okundu.";
                }
            }
        }
        catch (Exception ex)
        {
            model.Status = BatchInvoiceStatus.Failed;
            model.DiagnosticMessage = $"🔴 Ayrıştırma hatası: {ex.Message}";
            model.MissingFieldsWarning = "Hata";
            model.IsSelected = false;
        }

        return model;
    }

    private void MatchProductForLine(BatchInvoiceLineItemViewModel lineVm)
    {
        try
        {
            // 1. Barkod ile ara
            if (!string.IsNullOrWhiteSpace(lineVm.Barcode))
            {
                var dtB = Database.Query("SELECT TOP 1 Id, Code, Name FROM Products WHERE IsActive=1 AND Barcode=@b;", ("@b", lineVm.Barcode));
                if (dtB.Rows.Count > 0)
                {
                    lineVm.MatchedProductId = Convert.ToInt64(dtB.Rows[0]["Id"]);
                    lineVm.MatchedProductCode = dtB.Rows[0]["Code"]?.ToString();
                    lineVm.MatchedProductName = dtB.Rows[0]["Name"]?.ToString();
                    lineVm.MatchStatus = $"✅ Barkod Eşleşti ({lineVm.MatchedProductCode})";
                    lineVm.CurrentStock = ProductService.GetStock(lineVm.MatchedProductId.Value);
                    return;
                }
            }

            // 2. Ürün Kodu ile ara
            if (!string.IsNullOrWhiteSpace(lineVm.ItemCode))
            {
                var dtC = Database.Query("SELECT TOP 1 Id, Code, Name FROM Products WHERE IsActive=1 AND Code=@c;", ("@c", lineVm.ItemCode));
                if (dtC.Rows.Count > 0)
                {
                    lineVm.MatchedProductId = Convert.ToInt64(dtC.Rows[0]["Id"]);
                    lineVm.MatchedProductCode = dtC.Rows[0]["Code"]?.ToString();
                    lineVm.MatchedProductName = dtC.Rows[0]["Name"]?.ToString();
                    lineVm.MatchStatus = $"✅ Kod Eşleşti ({lineVm.MatchedProductCode})";
                    lineVm.CurrentStock = ProductService.GetStock(lineVm.MatchedProductId.Value);
                    return;
                }
            }

            // 3. Ürün Adı ile ara
            if (!string.IsNullOrWhiteSpace(lineVm.ItemName))
            {
                var dtN = Database.Query("SELECT TOP 1 Id, Code, Name FROM Products WHERE IsActive=1 AND Name=@n;", ("@n", lineVm.ItemName.Trim()));
                if (dtN.Rows.Count > 0)
                {
                    lineVm.MatchedProductId = Convert.ToInt64(dtN.Rows[0]["Id"]);
                    lineVm.MatchedProductCode = dtN.Rows[0]["Code"]?.ToString();
                    lineVm.MatchedProductName = dtN.Rows[0]["Name"]?.ToString();
                    lineVm.MatchStatus = $"✅ İsim Eşleşti ({lineVm.MatchedProductCode})";
                    lineVm.CurrentStock = ProductService.GetStock(lineVm.MatchedProductId.Value);
                    return;
                }
            }

            lineVm.MatchStatus = "🆕 Yeni Kart Açılacak";
            lineVm.CurrentStock = 0;
        }
        catch { }
    }

    private void MatchSupplierAccount(BatchInvoiceItemModel model)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(model.SupplierTaxNo))
            {
                var dt = Database.Query("SELECT TOP 1 Id, Name FROM Accounts WHERE IsActive=1 AND (TaxNumber=@tax OR IdentityNumber=@tax);", ("@tax", model.SupplierTaxNo));
                if (dt.Rows.Count > 0)
                {
                    model.MatchedAccountId = Convert.ToInt64(dt.Rows[0]["Id"]);
                    model.MatchedAccountName = dt.Rows[0]["Name"]?.ToString();
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(model.SupplierName))
            {
                string clean = model.SupplierName.Split(' ')[0];
                var dtName = Database.Query("SELECT TOP 1 Id, Name FROM Accounts WHERE IsActive=1 AND (Name LIKE @full OR Name LIKE @first);",
                    ("@full", "%" + model.SupplierName + "%"),
                    ("@first", "%" + clean + "%"));
                if (dtName.Rows.Count > 0)
                {
                    model.MatchedAccountId = Convert.ToInt64(dtName.Rows[0]["Id"]);
                    model.MatchedAccountName = dtName.Rows[0]["Name"]?.ToString();
                }
            }
        }
        catch { }
    }

    private void RefreshInvoicesGrid()
    {
        List<BatchInvoiceItemModel> filtered = _currentFilter switch
        {
            "SUCCESS" => _items.Where(x => x.Status == BatchInvoiceStatus.Success).ToList(),
            "WARNING" => _items.Where(x => x.Status == BatchInvoiceStatus.Warning || x.Status == BatchInvoiceStatus.Duplicate).ToList(),
            "FAILED" => _items.Where(x => x.Status == BatchInvoiceStatus.Failed).ToList(),
            _ => _items.ToList()
        };

        _gridInvoices.DataSource = null;
        _gridInvoices.DataSource = filtered;

        for (int i = 0; i < _gridInvoices.Rows.Count; i++)
        {
            var item = filtered[i];
            _gridInvoices.Rows[i].Cells["IsSelected"].Value = item.IsSelected;
        }
    }

    private void RefreshAllProductsGrid()
    {
        string search = _txtSearchAllProducts.Text.Trim().ToLowerInvariant();
        int statusFilterIndex = _cmbAllProductStatusFilter.SelectedIndex;

        var filtered = _allFlatItems.Where(it =>
        {
            if (statusFilterIndex == 1 && !it.IsImported) return false;
            if (statusFilterIndex == 2 && it.IsImported) return false;

            if (!string.IsNullOrEmpty(search))
            {
                bool matchName = (it.ItemName ?? "").ToLowerInvariant().Contains(search);
                bool matchBarcode = (it.Barcode ?? "").ToLowerInvariant().Contains(search);
                bool matchCode = (it.ItemCode ?? "").ToLowerInvariant().Contains(search);
                bool matchInv = (it.InvoiceNumber ?? "").ToLowerInvariant().Contains(search);
                if (!matchName && !matchBarcode && !matchCode && !matchInv) return false;
            }
            return true;
        }).ToList();

        _gridAllProducts.DataSource = null;
        _gridAllProducts.DataSource = filtered;

        int importedCount = _allFlatItems.Count(x => x.IsImported);
        int pendingCount = _allFlatItems.Count - importedCount;
        _lblAllProductsSummary.Text = $"Toplam: {_allFlatItems.Count} Ürün | ✅ Aktarılan: {importedCount} | ⏳ Bekleyen: {pendingCount}";
    }

    private void SetFilter(string filter)
    {
        _currentFilter = filter;
        _btnFilterAll.BackColor = filter == "ALL" ? Color.FromArgb(51, 65, 85) : Color.White;
        _btnFilterAll.ForeColor = filter == "ALL" ? Color.White : UITheme.TextPrimary;

        _btnFilterSuccess.BackColor = filter == "SUCCESS" ? Color.FromArgb(16, 185, 129) : Color.White;
        _btnFilterSuccess.ForeColor = filter == "SUCCESS" ? Color.White : Color.FromArgb(16, 185, 129);

        _btnFilterWarning.BackColor = filter == "WARNING" ? Color.FromArgb(217, 119, 6) : Color.White;
        _btnFilterWarning.ForeColor = filter == "WARNING" ? Color.White : Color.FromArgb(217, 119, 6);

        _btnFilterFailed.BackColor = filter == "FAILED" ? Color.FromArgb(239, 68, 68) : Color.White;
        _btnFilterFailed.ForeColor = filter == "FAILED" ? Color.White : Color.FromArgb(239, 68, 68);

        RefreshInvoicesGrid();
    }

    private void UpdateStats()
    {
        int total = _items.Count;
        int success = _items.Count(x => x.Status == BatchInvoiceStatus.Success);
        int warning = _items.Count(x => x.Status == BatchInvoiceStatus.Warning || x.Status == BatchInvoiceStatus.Duplicate);
        int failed = _items.Count(x => x.Status == BatchInvoiceStatus.Failed);

        _lblTotalCount.Text = total.ToString();
        _lblSuccessCount.Text = success.ToString();
        _lblWarningCount.Text = warning.ToString();
        _lblFailedCount.Text = failed.ToString();

        double totalSum = _items.Where(x => x.Status != BatchInvoiceStatus.Failed).Sum(x => x.GrandTotal);
        _lblTotalAmount.Text = totalSum.ToString("N2") + " ₺";

        _btnFilterAll.Text = $"📋 Tümü ({total})";
        _btnFilterSuccess.Text = $"🟢 Başarılı ({success})";
        _btnFilterWarning.Text = $"🟡 UYARI / EKSİK ({warning})";
        _btnFilterFailed.Text = $"🔴 OKUNAMAYAN ({failed})";
    }

    private BatchInvoiceItemModel? GetSelectedItem()
    {
        if (_gridInvoices.CurrentRow?.DataBoundItem is BatchInvoiceItemModel item)
            return item;
        return null;
    }

    private void ShowSelectedInvoiceItems()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            _pnlDiagnosticFailed.Visible = false;
            _gridDetailItems.Visible = true;
            _gridDetailItems.DataSource = null;
            _lblDetailHeader.Text = "Detaylarını görmek için yukarıdaki tablodan bir fatura seçiniz.";
            _lblInvoiceMetaBadge.Text = "";
            _lblWarningBanner.Text = "";
            return;
        }

        // Fatura OKUNAMADIYSA teşhis kartını aç
        if (item.Status == BatchInvoiceStatus.Failed)
        {
            _gridDetailItems.Visible = false;
            _pnlDiagnosticFailed.Visible = true;
            _pnlDiagnosticFailed.BringToFront();

            _lblDiagFileName.Text = $"📄 Dosya: {item.FileName} ({item.FileSizeFormatted})";
            _lblDiagReason.Text = $"Neden Okunamadı: {item.DiagnosticMessage}";
            _lblDetailHeader.Text = $"🔴 {item.FileName} faturası okunamadı!";
            _lblInvoiceMetaBadge.Text = "";
            _lblWarningBanner.Text = $"EKSİK / ARIZA: {item.DiagnosticMessage}";
            return;
        }

        // Fatura OKUNDUYSA: Kalem tablosunu ve meta rozetlerini göster
        _pnlDiagnosticFailed.Visible = false;
        _gridDetailItems.Visible = true;
        _gridDetailItems.BringToFront();

        string savedState = item.IsSaved ? "✅ Veritabanına ve Stoklara Aktarıldı" : "⏳ Aktarılmayı Bekliyor";
        _lblDetailHeader.Text = $"📄 {item.FileName} • {item.SupplierName} • {item.ItemCount} Kalem • Toplam: {item.GrandTotal:N2} ₺ • [{savedState}]";

        // Meta Rozetleri
        var metaParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.ReferenceNo)) metaParts.Add($"ETTN: {item.ReferenceNo}");
        if (!string.IsNullOrWhiteSpace(item.Scenario)) metaParts.Add($"Senaryo: {item.Scenario}");
        if (!string.IsNullOrWhiteSpace(item.InvoiceKind)) metaParts.Add($"Tür: {item.InvoiceKind}");
        if (!string.IsNullOrWhiteSpace(item.SupplierTaxNo)) metaParts.Add($"VKN/TCKN: {item.SupplierTaxNo}");
        if (!string.IsNullOrWhiteSpace(item.SupplierTaxOffice)) metaParts.Add($"VD: {item.SupplierTaxOffice}");
        if (!string.IsNullOrWhiteSpace(item.OrderNumber)) metaParts.Add($"Sipariş No: {item.OrderNumber}");

        _lblInvoiceMetaBadge.Text = metaParts.Count > 0 ? "🔹 " + string.Join(" | ", metaParts) : "";

        // Uyarı / Eksik Bildirimi
        if (item.Status == BatchInvoiceStatus.Warning || item.Status == BatchInvoiceStatus.Duplicate)
        {
            _lblWarningBanner.Text = $"⚠️ DİKKAT: {item.MissingFieldsWarning}";
            _lblWarningBanner.Visible = true;
        }
        else
        {
            _lblWarningBanner.Text = "🟢 Eksiksiz Fatura: Tüm alanlar ve ürün tablosu başarıyla doğrulandı.";
            _lblWarningBanner.ForeColor = Color.FromArgb(16, 185, 129);
            _lblWarningBanner.Visible = true;
        }

        _gridDetailItems.DataSource = null;
        _gridDetailItems.DataSource = item.LineItems;
    }

    // --- "O PDF'Yİ ALSAM" VE İNCELEME METOTLARI ---

    private void OpenSelectedPdf()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            MessageBox.Show("Lütfen açmak istediğiniz faturayı tablodan seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        FileLauncherHelper.OpenDocument(item.FilePath, $"{item.FileName} Faturası");
    }

    private void ExportSelectedPdf()
    {
        var item = GetSelectedItem();
        if (item == null)
        {
            MessageBox.Show("Lütfen dışa aktarmak istediğiniz faturayı tablodan seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "PDF Faturayı Dışa Aktar / Farklı Kaydet",
            FileName = item.FileName,
            Filter = "PDF Dosyası (*.pdf)|*.pdf|Tüm Dosyalar (*.*)|*.*"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.Copy(item.FilePath, sfd.FileName, true);
                MessageBox.Show($"Fatura başarıyla kaydedildi:\n{sfd.FileName}", "Dışa Aktarıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dosya kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ShowInExplorer()
    {
        var item = GetSelectedItem();
        if (item != null && File.Exists(item.FilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
        }
    }

    private void ExportAllFailedPdfs()
    {
        var problemItems = _items.Where(x => x.Status == BatchInvoiceStatus.Failed || x.Status == BatchInvoiceStatus.Warning).ToList();
        if (problemItems.Count == 0)
        {
            MessageBox.Show("Harika! Listede okunamayan veya problemli hiçbir PDF fatura bulunmuyor.", "Hata Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var fbd = new FolderBrowserDialog
        {
            Description = $"Okunamayan veya uyarılı {problemItems.Count} adet PDF faturanın toplanacağı hedef klasörü seçiniz:",
            UseDescriptionForTitle = true
        };

        if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            try
            {
                string targetDir = Path.Combine(fbd.SelectedPath, $"Okunamayan_Faturalar_{DateTime.Now:yyyyMMdd_HHmm}");
                Directory.CreateDirectory(targetDir);

                var reportLines = new List<string>
                {
                    "================================================================================",
                    $"BİLENSİS - OKUNAMAYAN / EKSİKLİ PDF FATURALAR RAPORU ({DateTime.Now:dd.MM.yyyy HH:mm})",
                    "================================================================================",
                    $"Toplam Sorunlu Dosya Sayısı: {problemItems.Count}",
                    ""
                };

                int copied = 0;
                foreach (var it in problemItems)
                {
                    if (File.Exists(it.FilePath))
                    {
                        string destFile = Path.Combine(targetDir, it.FileName);
                        File.Copy(it.FilePath, destFile, true);
                        copied++;

                        reportLines.Add($"• Dosya: {it.FileName}");
                        reportLines.Add($"  Durum: {it.StatusBadge}");
                        reportLines.Add($"  Fatura No: {(string.IsNullOrWhiteSpace(it.InvoiceNumber) ? "(Tespit Edilemedi)" : it.InvoiceNumber)}");
                        reportLines.Add($"  ETTN: {(string.IsNullOrWhiteSpace(it.ReferenceNo) ? "(Yok)" : it.ReferenceNo)}");
                        reportLines.Add($"  Tedarikçi: {(string.IsNullOrWhiteSpace(it.SupplierName) ? "(Tespit Edilemedi)" : it.SupplierName)}");
                        reportLines.Add($"  Eksik / Uyarı Nedeni: {it.MissingFieldsWarning}");
                        reportLines.Add($"  Teşhis Açıklaması: {it.DiagnosticMessage}");
                        reportLines.Add("--------------------------------------------------------------------------------");
                    }
                }

                string reportPath = Path.Combine(targetDir, "Okunamayan_Faturalar_Raporu.txt");
                File.WriteAllLines(reportPath, reportLines);

                var res = MessageBox.Show(
                    $"İşlem tamamlandı!\n\n" +
                    $"• {copied} adet sorunlu PDF ve detaylı teşhis raporu klasöre kaydedildi:\n{targetDir}\n\n" +
                    $"Hedef klasörü şimdi açmak ister misiniz?",
                    "Faturalar Dışa Aktarıldı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );

                if (res == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo { FileName = targetDir, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dışa aktarma sırasında hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void EditSelectedInvoice()
    {
        var item = GetSelectedItem();
        if (item == null) return;

        using var dlg = new InvoiceEntryDialog(item.FilePath);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            item.Status = BatchInvoiceStatus.Success;
            item.StatusBadge = "🟢 Manuel Kaydedildi";
            item.DiagnosticMessage = "✅ Fatura kullanıcı tarafından manuel incelenip başarıyla kaydedildi.";
            item.MissingFieldsWarning = "Eksiksiz (Manuel Kaydedildi)";
            item.IsSaved = true;
            item.IsSelected = false;
            foreach (var line in item.LineItems) line.IsImported = true;

            RefreshInvoicesGrid();
            RefreshAllProductsGrid();
            UpdateStats();
            ShowSelectedInvoiceItems();
        }
    }

    // --- TOPLU SİSTEME VE STOKLARA AKTARMA ---

    private async void BulkSaveInvoicesClick()
    {
        for (int i = 0; i < _gridInvoices.Rows.Count; i++)
        {
            if (_gridInvoices.Rows[i].DataBoundItem is BatchInvoiceItemModel it)
            {
                it.IsSelected = Convert.ToBoolean(_gridInvoices.Rows[i].Cells["IsSelected"].Value);
            }
        }

        var toSave = _items.Where(x => x.IsSelected && !x.IsSaved && x.ParsedResult != null && x.ParsedResult.Items.Count > 0).ToList();

        if (toSave.Count == 0)
        {
            MessageBox.Show("Kaydedilecek seçili geçerli fatura bulunamadı. Lütfen aktarmak istediğiniz faturaların 'Seç' kutucuğunu işaretleyiniz.", "Fatura Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long targetWarehouseId = 1;
        if (_cmbWarehouse.SelectedItem is WarehouseComboItem ci && ci.Id > 0)
        {
            targetWarehouseId = ci.Id;
        }
        else
        {
            try
            {
                var dtDefWh = Database.Query("SELECT TOP 1 Id FROM Warehouses WHERE IsActive=1 ORDER BY IsDefault DESC;");
                if (dtDefWh.Rows.Count > 0) targetWarehouseId = Convert.ToInt64(dtDefWh.Rows[0]["Id"]);
            }
            catch { }
        }

        var confirm = MessageBox.Show(
            $"Seçilen {toSave.Count} adet fatura sisteme ve stoklara kaydedilecek:\n\n" +
            $"• Giriş Deposu: {_cmbWarehouse.Text}\n" +
            $"• Stok Girişi (Stok Hareketi): {(_chkUpdateStock.Checked ? "EVET (Depo Mevcudu Artacak)" : "HAYIR")}\n" +
            $"• Cari Bakiye Güncellemesi: {(_chkUpdateAccount.Checked ? "EVET (Tedarikçi Borcu İşlenecek)" : "HAYIR")}\n" +
            $"• Yeni Stok Kartı Açma: {(_chkAutoCreateProducts.Checked ? "EVET" : "HAYIR")}\n\n" +
            $"İşlemi onaylıyor musunuz?",
            "Toplu Fatura & Stok Aktarımı Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (confirm != DialogResult.Yes) return;

        _pnlProgress.Visible = true;
        _progressBar.Value = 0;
        _progressBar.Maximum = toSave.Count;

        int savedCount = 0;
        var savedPdfPaths = new List<string>();
        int errorCount = 0;

        await Task.Run(() =>
        {
            foreach (var item in toSave)
            {
                try
                {
                    Invoke(new Action(() =>
                    {
                        _lblProgress.Text = $"Fatura ve stoklar kaydediliyor: {item.InvoiceNumber} ({item.SupplierName})...";
                    }));

                    // 1. Cari Yoksa Otomatik Oluştur
                    long accountId = item.MatchedAccountId ?? 0;
                    if (accountId <= 0)
                    {
                        string accName = !string.IsNullOrWhiteSpace(item.SupplierName) ? item.SupplierName.Trim() : "Tedarikçi " + item.InvoiceNumber;
                        var accIdObj = Database.ExecuteScalar(@"
INSERT INTO Accounts (Code, Name, Type, TaxNumber, Address, Balance, CreatedAt)
VALUES (@code, @name, 'Tedarikçi', @tax, @addr, 0, @created);
SELECT SCOPE_IDENTITY();",
                            ("@code", "TED-" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99)),
                            ("@name", accName),
                            ("@tax", (object?)item.SupplierTaxNo ?? DBNull.Value),
                            ("@addr", (object?)item.ParsedResult?.SupplierAddress ?? DBNull.Value),
                            ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                        );
                        accountId = Convert.ToInt64(accIdObj);
                        item.MatchedAccountId = accountId;
                        item.MatchedAccountName = accName;
                    }

                    // 2. Invoice Modelini Oluştur
                    var inv = new Invoice
                    {
                        InvoiceNumber = !string.IsNullOrWhiteSpace(item.InvoiceNumber) ? item.InvoiceNumber : "FAT-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        InvoiceDate = item.InvoiceDate,
                        InvoiceType = "Alış Faturası",
                        AccountId = accountId,
                        AccountName = item.MatchedAccountName ?? item.SupplierName,
                        WarehouseId = targetWarehouseId,
                        WarehouseName = _cmbWarehouse.Text,
                        SubTotal = item.SubTotal,
                        VatTotal = item.VatTotal,
                        GrandTotal = item.GrandTotal,
                        PdfPath = item.FilePath,
                        Note = "Toplu PDF Fatura Sihirbazı ile otomatik yüklendi.",
                        CustomizationId = item.ParsedResult?.CustomizationId,
                        Scenario = item.ParsedResult?.Scenario,
                        InvoiceKind = item.ParsedResult?.InvoiceKind,
                        OrderNumber = item.ParsedResult?.OrderNumber,
                        OrderDate = item.ParsedResult?.OrderDate,
                        RelatedStore = item.ParsedResult?.RelatedStore,
                        CargoId = item.ParsedResult?.CargoId,
                        ReferenceNo = item.ParsedResult?.ReferenceNo,
                        IssueTime = item.ParsedResult?.IssueTime
                    };

                    // Kalemleri Dönüştür ve Ürün Eşle
                    foreach (var parsedLine in item.ParsedResult!.Items)
                    {
                        long? matchedProdId = null;
                        if (!string.IsNullOrWhiteSpace(parsedLine.Barcode))
                        {
                            var dtP = Database.Query("SELECT TOP 1 Id FROM Products WHERE IsActive=1 AND Barcode=@b;", ("@b", parsedLine.Barcode));
                            if (dtP.Rows.Count > 0) matchedProdId = Convert.ToInt64(dtP.Rows[0]["Id"]);
                        }
                        if (!matchedProdId.HasValue && !string.IsNullOrWhiteSpace(parsedLine.ItemCode))
                        {
                            var dtC = Database.Query("SELECT TOP 1 Id FROM Products WHERE IsActive=1 AND Code=@c;", ("@c", parsedLine.ItemCode));
                            if (dtC.Rows.Count > 0) matchedProdId = Convert.ToInt64(dtC.Rows[0]["Id"]);
                        }
                        if (!matchedProdId.HasValue && !string.IsNullOrWhiteSpace(parsedLine.ItemName))
                        {
                            var dtN = Database.Query("SELECT TOP 1 Id FROM Products WHERE IsActive=1 AND Name=@n;", ("@n", parsedLine.ItemName.Trim()));
                            if (dtN.Rows.Count > 0) matchedProdId = Convert.ToInt64(dtN.Rows[0]["Id"]);
                        }

                        var invItem = new InvoiceItem
                        {
                            LineNo = parsedLine.LineNo,
                            ProductId = matchedProdId,
                            Barcode = parsedLine.Barcode,
                            ItemCode = parsedLine.ItemCode,
                            ItemName = parsedLine.ItemName,
                            Quantity = parsedLine.Quantity,
                            Unit = parsedLine.Unit,
                            UnitPrice = parsedLine.UnitPrice,
                            DiscountPercent = parsedLine.DiscountPercent,
                            DiscountAmount = parsedLine.DiscountAmount,
                            VatPercent = parsedLine.VatPercent,
                            VatAmount = parsedLine.VatAmount,
                            LineTotal = parsedLine.LineTotal,
                            ActionDecision = "Güncelle"
                        };
                        inv.Items.Add(invItem);
                    }

                    // 3. Faturayı Kaydet (Stok hareketleri Gelen olarak işlenir, depolar güncellenir)
                    InvoiceService.SaveInvoice(inv, _chkUpdateStock.Checked, _chkUpdateAccount.Checked);
                    if (!string.IsNullOrWhiteSpace(inv.PdfPath)) lock (savedPdfPaths) savedPdfPaths.Add(inv.PdfPath);

                    item.IsSaved = true;
                    item.IsSelected = false;
                    item.Status = BatchInvoiceStatus.Success;
                    item.StatusBadge = "✅ Sisteme Kaydedildi";
                    item.DiagnosticMessage = $"✅ Fatura ve {inv.Items.Count} kalem ürün veritabanına ve depoya başarıyla aktarıldı.";

                    // Faturadaki kalemlerin güncel stok miktarlarını tazele
                    foreach (var lineVm in item.LineItems)
                    {
                        lineVm.IsImported = true;
                        // Güncel stoku yeniden oku
                        if (lineVm.MatchedProductId.HasValue)
                        {
                            lineVm.CurrentStock = ProductService.GetStock(lineVm.MatchedProductId.Value);
                        }
                    }

                    savedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    item.SaveError = ex.Message;
                    item.DiagnosticMessage = $"❌ Kayıt hatası: {ex.Message}";
                }

                Invoke(new Action(() =>
                {
                    _progressBar.Value = Math.Min(savedCount + errorCount, _progressBar.Maximum);
                }));
            }
        });

        _pnlProgress.Visible = false;
        RefreshInvoicesGrid();
        RefreshAllProductsGrid();
        UpdateStats();
        ShowSelectedInvoiceItems();

        MessageBox.Show(
            $"Toplu fatura ve stok aktarımı tamamlandı!\n\n" +
            $"• Başarıyla Kaydedilen Fatura: {savedCount}\n" +
            $"• Depoya Girişi Yapılan Kalemler: {toSave.Sum(x => x.ItemCount)}\n" +
            $"• Hata Oluşan: {errorCount}\n\n" +
            $"Tüm ürünlerin yeni stok mevcudunu 'Tüm Taranan Ürünler' sekmesinden ve Ana Stok Listesinden görebilirsiniz.",
            "Stok ve Fatura Aktarımı Başarılı",
            MessageBoxButtons.OK,
            errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information
        );

        // PDF'ler güvenli arşive alındı; Google Drive'a da yedeklensin mi (ayara göre sorar / otomatik yapar)
        PdfArchiveService.OfferDriveCopy(this, savedPdfPaths);
    }
}
