using System.Data;
using System.Diagnostics;
using Krypton.Ribbon;
using Krypton.Toolkit;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp;

public class MainForm : KryptonForm
{
    // Ribbon & Navigasyon
    private readonly KryptonRibbon _ribbon = new();
    private readonly Panel _contentArea = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusUser = new();
    private readonly ToolStripStatusLabel _statusLicense = new();
    private readonly ToolStripStatusLabel _statusDb = new();
    private readonly ToolStripStatusLabel _statusClock = new();
    private KryptonRibbonGroupButton? _ribbonBtnNotify;
    private int _currentNavIndex = 0;
    public bool IsLoggedOut { get; private set; } = false;

    // Sayfa Panelleri
    private Panel _pnlDashboard = new();
    private Panel _pnlProducts = new();
    private Panel _pnlAccounts = new();
    private Panel _pnlStockMovements = new();
    private Panel _pnlAccountMovements = new();
    private Panel _pnlAuditLogs = new();
    private Panel _pnlUsers = new();
    private Panel _pnlLicense = new();
    private Panel _pnlSettings = new();

    // Kullanıcı Sayfası Kontrolleri
    private KryptonDataGridView _gridUsers = new();
    private TextBox _txtUserSearch = new();

    // Audit Log Sayfası Kontrolleri
    private KryptonDataGridView _gridAuditLogs = new();
    private TextBox _txtAuditSearch = new();
    private ComboBox _cmbAuditEntity = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbAuditAction = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpAuditStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private DateTimePicker _dtpAuditEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

    // Dashboard Kontrolleri & Bölümleri (Özelleştirilebilir)
    private StatCard _cardStockVal = new();
    private StatCard _cardReceivable = new();
    private StatCard _cardPayable = new();
    private StatCard _cardTodayCash = new();
    private StatCard _cardOverdue = new();
    private KryptonDataGridView _gridCriticalStock = new();
    private KryptonDataGridView _gridTopDebtors = new();
    private KryptonDataGridView _gridRecentTransactions = new();
    private TextBox _txtDashProductSearch = new();
    private KryptonDataGridView _gridDashProductSearch = new();

    private Panel _dashHeader = new();
    private CardPanel _dashPnlCustomizer = new();
    private FlowLayoutPanel _dashCardsFlow = new();
    private CardPanel _dashPnlSearch = new();
    private TableLayoutPanel _dashSplitTable = new();
    private CardPanel _dashPnlCrit = new();
    private CardPanel _dashPnlDebtors = new();
    private CardPanel _dashPnlRecent = new();
    private ComboBox _cmbDashLayoutMode = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private CheckBox _chkDashCards = new() { Text = "📊 Özet Kartlar", AutoSize = true, Checked = true, Font = UITheme.SmallFont };
    private CheckBox _chkDashSearch = new() { Text = "🔍 Hızlı Satış Arama", AutoSize = true, Checked = true, Font = UITheme.SmallFont };
    private CheckBox _chkDashCrit = new() { Text = "⚠️ Kritik Stoklar", AutoSize = true, Checked = true, Font = UITheme.SmallFont };
    private CheckBox _chkDashDebtors = new() { Text = "💰 Borçlu Müşteriler", AutoSize = true, Checked = true, Font = UITheme.SmallFont };
    private CheckBox _chkDashRecent = new() { Text = "🕒 Son Finansallar", AutoSize = true, Checked = true, Font = UITheme.SmallFont };

    // Kasa / Finans Fatura & Ödeme Takibi Kontrolleri
    private KryptonDataGridView _gridInvoices = new();
    private TextBox _txtInvSearch = new();
    private ComboBox _cmbInvType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbInvStatus = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpInvStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-3) };
    private DateTimePicker _dtpInvEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private Label _lblInvSummaryCount = new() { Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, AutoSize = true };
    private Label _lblInvSummaryTotal = new() { Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = UITheme.TextPrimary, AutoSize = true };
    private Label _lblInvSummaryPaid = new() { Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = UITheme.Success, AutoSize = true };
    private Label _lblInvSummaryRemaining = new() { Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Color.FromArgb(220, 38, 38), AutoSize = true };

    // Ürünler Sayfası Kontrolleri
    private KryptonDataGridView _gridProducts = new();
    private TextBox _txtProductSearch = new();
    private ComboBox _cmbProductCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbProductWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private CheckBox _chkOnlyCritical = new() { Text = "Sadece Kritik Stoklar", AutoSize = true, Font = UITheme.RegularFont };

    // Cari Sayfası Kontrolleri
    private KryptonDataGridView _gridAccounts = new();
    private TextBox _txtAccountSearch = new();
    private ComboBox _cmbAccountType = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // Stok Hareketleri Sayfası Kontrolleri
    private KryptonDataGridView _gridStockMov = new();
    private TextBox _txtStockSearch = new();
    private ComboBox _cmbStockType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbStockWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpStockStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private DateTimePicker _dtpStockEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

    // Kasa & Finansal Hareketler Sayfası Kontrolleri
    private KryptonDataGridView _gridAccMov = new();
    private TextBox _txtAccMovSearch = new();
    private ComboBox _cmbAccMovType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpAccStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private DateTimePicker _dtpAccEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private TabControl _tabsFinance = new();
    private bool _isRefreshing = false;

    public MainForm()
    {
        Text = "Bilensis - Stok & Cari Yönetim Sistemi";
        Width = 1380;
        Height = 850;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        BuildLayout();
        SyncProductCategories();
        SyncWarehouses();
        ShowPage(0);

        // Hızlı İşlem Kısayolları (F2: Cari Ara, F3: Hızlı Satış, F4: Hızlı Alış, F5: Borç Ekle, F6: Tahsilat, F10: Ekstre)
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.S)
            {
                OpenSuperUserKeygen();
            }
            else if (e.KeyCode == Keys.F2)
            {
                ShowPage(2); // Cari & Veresiye Sayfası
                _txtAccountSearch.Focus();
                _txtAccountSearch.SelectAll();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F3)
            {
                OpenQuickSale("Satış");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F4)
            {
                OpenQuickSale("Alış");
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F5)
            {
                ShowPage(2);
                AddAccountMovementForSelected("Satış"); // F5: Seçili Cariye Borç Yaz
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F6)
            {
                ShowPage(2);
                AddAccountMovementForSelected("Tahsilat"); // F6: Seçili Cariden Tahsilat Al
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F7)
            {
                OpenInvoiceEntry(); // F7: E-Fatura / Alış Faturası Girişi
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F10)
            {
                ShowPage(2);
                ViewAccountStatement(); // F10: Ekstre Görüntüle
                e.Handled = true;
            }
        };

        MobileScannerService.BarcodeScannedFromMobile += OnBarcodeScannedFromMobile;
        FormClosed += (s, e) => MobileScannerService.BarcodeScannedFromMobile -= OnBarcodeScannedFromMobile;

        // Günlük Otomatik Bulut Yedekleme Kontrolü
        Task.Run(() => CloudBackupService.AutoCheckDailyBackup());

        // Program Kapanışında Otomatik Bulut Yedekleme (Aktifse sessiz çalışır)
        FormClosing += (s, e) =>
        {
            try
            {
                if (CloudBackupService.Config.BackupOnExit)
                {
                    CloudBackupService.ExecuteBackup(silent: true);
                }
            }
            catch { }
        };
    }

    private void BuildLayout()
    {
        // 1. Ribbon Menüsü (En Üst)
        BuildRibbon();
        Controls.Add(_ribbon);
        _ribbon.BringToFront();

        // 2. Alt Durum Çubuğu (StatusStrip)
        BuildStatusStrip();
        Controls.Add(_statusStrip);
        _statusStrip.BringToFront();

        // 3. İçerik Alanı (Kalan tüm alan %100 Fill, z-order'da en arkada kalarak ribbon ve statusstrip ile asla çakışmaz)
        _contentArea.Dock = DockStyle.Fill;
        _contentArea.BackColor = UITheme.Background;
        _contentArea.Padding = new Padding(12);
        Controls.Add(_contentArea);
        _contentArea.SendToBack();

        // Sayfaları İnşa Et
        BuildDashboardPage();
        BuildProductsPage();
        BuildAccountsPage();
        BuildStockMovementsPage();
        BuildAccountMovementsPage();
        BuildAuditLogsPage();
        BuildUsersPage();
        BuildLicensePage();
        BuildSettingsPage();
    }

    private void BuildStatusStrip()
    {
        _statusStrip.Dock = DockStyle.Bottom;
        _statusStrip.Height = 28;
        _statusStrip.BackColor = Color.FromArgb(15, 23, 42);
        _statusStrip.ForeColor = Color.White;
        _statusStrip.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _statusStrip.SizingGrip = false;

        var curUser = UserService.CurrentUser;
        _statusUser.Text = $"👤 {curUser?.FullName ?? "Admin"} ({curUser?.Role ?? "Yönetici"})";
        _statusUser.ForeColor = Color.FromArgb(226, 232, 240);
        _statusUser.Margin = new Padding(8, 0, 16, 0);

        var lic = LicenseService.CurrentLicense;
        _statusLicense.Text = $"🔑 Lisans: {lic.DaysRemaining} Gün Kaldı ({lic.LicensedTo})";
        _statusLicense.ForeColor = lic.DaysRemaining < 30 ? Color.FromArgb(248, 113, 113) : Color.FromArgb(52, 211, 153);
        _statusLicense.Margin = new Padding(0, 0, 16, 0);

        _statusDb.Text = "🗄️ SQL Server: Bağlı";
        _statusDb.ForeColor = Color.FromArgb(56, 189, 248);
        _statusDb.Margin = new Padding(0, 0, 16, 0);

        _statusClock.Text = $"🕒 {DateTime.Now:HH:mm:ss}";
        _statusClock.ForeColor = Color.FromArgb(148, 163, 184);
        _statusClock.Alignment = ToolStripItemAlignment.Right;
        _statusClock.Margin = new Padding(0, 0, 12, 0);

        var clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        clockTimer.Tick += (s, e) => _statusClock.Text = $"🕒 {DateTime.Now:HH:mm:ss}";
        clockTimer.Start();

        _statusStrip.Items.Add(_statusUser);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_statusLicense);
        _statusStrip.Items.Add(new ToolStripSeparator());
        _statusStrip.Items.Add(_statusDb);
        _statusStrip.Items.Add(_statusClock);
    }

    private void BuildRibbon()
    {
        _ribbon.Dock = DockStyle.Top;
        _ribbon.RibbonFileAppTab.FileAppTabText = "BİLENSİS";
        _ribbon.RibbonFileAppButton.AppButtonShowRecentDocs = false;
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Clear();

        var itemDb = new KryptonContextMenuItem("⚙️ SQL Veritabanı Ayarları", (s, e) => 
        {
            using var dlg = new DatabaseConfigDialog();
            dlg.ShowDialog(this);
        });
        itemDb.Image = RibbonIconFactory.CreateIcon("settings", 24);

        var itemNotif = new KryptonContextMenuItem("🔔 Bildirim & Uyarı Merkezi", (s, e) => OpenNotificationCenter());
        itemNotif.Image = RibbonIconFactory.CreateIcon("notification", 24);

        var itemZ = new KryptonContextMenuItem("🔒 Kasa Gün Sonu & Z Raporu", (s, e) => OpenDailyCashClosing());
        itemZ.Image = RibbonIconFactory.CreateIcon("closing", 24);

        var itemLogout = new KryptonContextMenuItem("👤 Oturumu Kapat", (s, e) => LogoutAction());
        itemLogout.Image = RibbonIconFactory.CreateIcon("logout", 24);

        var itemExit = new KryptonContextMenuItem("🚪 Programdan Çıkış", (s, e) => Close());
        itemExit.Image = RibbonIconFactory.CreateIcon("danger", 24);

        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(itemDb);
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(itemNotif);
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(itemZ);
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(new KryptonContextMenuSeparator());
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(itemLogout);
        _ribbon.RibbonFileAppButton.AppButtonMenuItems.Add(itemExit);

        _ribbon.RibbonTabs.Clear();

        var curUser = UserService.CurrentUser;

        // ==========================================
        // SEKME 1: 📊 GENEL BAKIŞ (DASHBOARD)
        // ==========================================
        var tabDash = new KryptonRibbonTab { Text = "📊 Genel Bakış" };

        var grpDashMain = new KryptonRibbonGroup { TextLine1 = "Ana Kontrol" };
        var tripDashMain = new KryptonRibbonGroupTriple();

        var btnDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📊 Kontrol Paneli", 
            TextLine2 = "Özet Pano",
            ImageLarge = RibbonIconFactory.CreateIcon("dashboard", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("dashboard", 16)
        };
        btnDash.Click += (s, e) => ShowPage(0);

        var btnZReportDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔒 Gün Sonu (Z)", 
            TextLine2 = "Kasa Raporu",
            ImageLarge = RibbonIconFactory.CreateIcon("zreport", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("zreport", 16)
        };
        btnZReportDash.Click += (s, e) => OpenDailyCashClosing();

        var btnNotifDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔔 Bildirimler", 
            TextLine2 = "Kritik Uyarılar",
            ImageLarge = RibbonIconFactory.CreateIcon("notification", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("notification", 16)
        };
        btnNotifDash.Click += (s, e) => OpenNotificationCenter();
        _ribbonBtnNotify = btnNotifDash;

        tripDashMain.Items.Add(btnDash);
        tripDashMain.Items.Add(btnZReportDash);
        tripDashMain.Items.Add(btnNotifDash);
        grpDashMain.Items.Add(tripDashMain);
        tabDash.Groups.Add(grpDashMain);

        // --- 2. EKRAN DÜZENİ GRUBU (Ribbon Menüye Taşındı) ---
        var grpDashLayout = new KryptonRibbonGroup { TextLine1 = "👁️ Ekran Düzeni" };
        var tripLayout1 = new KryptonRibbonGroupTriple();

        var btnLayoutStd = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🌐 Standart Pano", 
            TextLine2 = "Tüm Bölümler",
            ImageLarge = RibbonIconFactory.CreateIcon("layout", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("layout", 16)
        };
        btnLayoutStd.Click += (s, e) => SetDashboardLayout(0);

        var btnLayoutCrit = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚠️ Kritik Stok", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnLayoutCrit.Click += (s, e) => SetDashboardLayout(1);

        var btnLayoutDebtors = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💰 Borçlular", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("accounts", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("accounts", 16)
        };
        btnLayoutDebtors.Click += (s, e) => SetDashboardLayout(3);

        tripLayout1.Items.Add(btnLayoutStd);
        tripLayout1.Items.Add(btnLayoutCrit);
        tripLayout1.Items.Add(btnLayoutDebtors);
        grpDashLayout.Items.Add(tripLayout1);

        var tripLayout2 = new KryptonRibbonGroupTriple();

        var btnLayoutRecent = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🕒 Son İşlemler", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnLayoutRecent.Click += (s, e) => SetDashboardLayout(2);

        var btnLayoutSearch = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔍 Hızlı Arama", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnLayoutSearch.Click += (s, e) => SetDashboardLayout(4);

        var btnRefreshDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔄 Verileri Yenile", 
            TextLine2 = "Canlı Güncelle",
            ImageLarge = RibbonIconFactory.CreateIcon("refresh", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("refresh", 16)
        };
        btnRefreshDash.Click += (s, e) => RefreshDashboard();

        tripLayout2.Items.Add(btnLayoutRecent);
        tripLayout2.Items.Add(btnLayoutSearch);
        tripLayout2.Items.Add(btnRefreshDash);
        grpDashLayout.Items.Add(tripLayout2);
        tabDash.Groups.Add(grpDashLayout);

        // --- 4. RİSK & ENTEGRASYON GRUBU ---
        var grpDashDue = new KryptonRibbonGroup { TextLine1 = "Risk & Entegrasyon" };
        var tripDashDue = new KryptonRibbonGroupTriple();

        var btnOverdueDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚠️ Vadesi Dolanlar", 
            TextLine2 = "Geciken Borçlar",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnOverdueDash.Click += (s, e) => OpenNotificationCenter(1);

        var btnTgDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🤖 Telegram Cep", 
            TextLine2 = "Anlık Durum",
            ImageLarge = RibbonIconFactory.CreateIcon("telegram", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("telegram", 16)
        };
        btnTgDash.Click += (s, e) => OpenTelegramConfig();

        var btnMobDash = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📱 Canlı Barkod", 
            TextLine2 = "Kamera Okuyucu",
            ImageLarge = RibbonIconFactory.CreateIcon("mobilescanner", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("mobilescanner", 16)
        };
        btnMobDash.Click += (s, e) => OpenMobileScanner();

        tripDashDue.Items.Add(btnOverdueDash);
        tripDashDue.Items.Add(btnTgDash);
        tripDashDue.Items.Add(btnMobDash);
        grpDashDue.Items.Add(tripDashDue);
        tabDash.Groups.Add(grpDashDue);

        _ribbon.RibbonTabs.Add(tabDash);

        // ==========================================
        // SEKME 2: ⚡ HIZLI SATIŞ & POS
        // ==========================================
        var tabSales = new KryptonRibbonTab { Text = "⚡ Hızlı Satış & POS" };

        var grpSale = new KryptonRibbonGroup { TextLine1 = "Satış & Fiş" };
        var tripSale = new KryptonRibbonGroupTriple();

        var btnSale = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚡ Hızlı Satış", 
            TextLine2 = "(F3 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnSale.Click += (s, e) => OpenQuickSale("Satış");

        var btnBuy = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📥 Hızlı Alış", 
            TextLine2 = "(F4 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("quickbuy", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quickbuy", 16)
        };
        btnBuy.Click += (s, e) => OpenQuickSale("Alış");

        var btnParked = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📂 Bekleyen Fişler", 
            TextLine2 = "Sepet / Askı",
            ImageLarge = RibbonIconFactory.CreateIcon("parked", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("parked", 16)
        };
        btnParked.Click += (s, e) =>
        {
            using var dlg = new ParkedSalesDialog();
            dlg.ShowDialog(this);
        };

        tripSale.Items.Add(btnSale);
        tripSale.Items.Add(btnBuy);
        tripSale.Items.Add(btnParked);
        grpSale.Items.Add(tripSale);
        tabSales.Groups.Add(grpSale);

        var grpCash = new KryptonRibbonGroup { TextLine1 = "Tahsilat & Kasa" };
        var tripCash = new KryptonRibbonGroupTriple();

        var btnDebt = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "➕ Borç Ekle", 
            TextLine2 = "(F5 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("debt", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("debt", 16)
        };
        btnDebt.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Satış"); };

        var btnCollect = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💵 Tahsilat Yap", 
            TextLine2 = "(F6 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("collect", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("collect", 16)
        };
        btnCollect.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Tahsilat"); };

        var btnCloseDay = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔒 Gün Sonu (Z)", 
            TextLine2 = "Kasa Raporu",
            ImageLarge = RibbonIconFactory.CreateIcon("zreport", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("zreport", 16)
        };
        btnCloseDay.Click += (s, e) => OpenDailyCashClosing();

        tripCash.Items.Add(btnDebt);
        tripCash.Items.Add(btnCollect);
        tripCash.Items.Add(btnCloseDay);
        grpCash.Items.Add(tripCash);
        tabSales.Groups.Add(grpCash);

        var grpHardware = new KryptonRibbonGroup { TextLine1 = "POS Donanım" };
        var tripHardware = new KryptonRibbonGroupTriple();

        var btnCustomerDisplay = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📺 Müşteri Ekranı", 
            TextLine2 = "2. Ekran POS",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnCustomerDisplay.Click += (s, e) =>
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.CustomerDisplay) && !curUser.HasPermission(UserPermissions.QuickSale))
            {
                MessageBox.Show("Bu işlem için 'Çift Ekran / Müşteri Bilgi Ekranı' yetkiniz bulunmuyor.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CustomerDisplayForm.ShowOrToggle();
        };

        tripHardware.Items.Add(btnCustomerDisplay);
        grpHardware.Items.Add(tripHardware);
        tabSales.Groups.Add(grpHardware);

        _ribbon.RibbonTabs.Add(tabSales);

        // ==========================================
        // SEKME 3: 📦 ÜRÜNLER & STOK
        // ==========================================
        var tabStock = new KryptonRibbonTab { Text = "📦 Ürünler & Stok" };

        var grpProduct = new KryptonRibbonGroup { TextLine1 = "Ürün Kartları" };
        var tripProd = new KryptonRibbonGroupTriple();

        var btnProdList = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📦 Ürün Listesi", 
            TextLine2 = "Tüm Stok",
            ImageLarge = RibbonIconFactory.CreateIcon("products", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("products", 16)
        };
        btnProdList.Click += (s, e) => ShowPage(1);

        var btnNewProd = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "➕ Yeni Ürün", 
            TextLine2 = "Kart Tanımla",
            ImageLarge = RibbonIconFactory.CreateIcon("newproduct", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("newproduct", 16)
        };
        btnNewProd.Click += (s, e) => AddProduct();

        var btnBarcode = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🏷️ Barkod & Etiket", 
            TextLine2 = "Fiyat Basımı",
            ImageLarge = RibbonIconFactory.CreateIcon("barcode", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("barcode", 16)
        };
        btnBarcode.Click += (s, e) => PrintBarcodeLabelForSelected();

        tripProd.Items.Add(btnProdList);
        tripProd.Items.Add(btnNewProd);
        tripProd.Items.Add(btnBarcode);
        grpProduct.Items.Add(tripProd);
        tabStock.Groups.Add(grpProduct);

        var grpFast = new KryptonRibbonGroup { TextLine1 = "Seri Giriş & Geçmiş" };
        var tripFast = new KryptonRibbonGroupTriple();

        var btnFastEntry = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚡ Seri Ürün Girişi", 
            TextLine2 = "Manuel Ekleme",
            ImageLarge = RibbonIconFactory.CreateIcon("fastentry", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("fastentry", 16)
        };
        btnFastEntry.Click += (s, e) => OpenFastProductEntry();

        var btnHistoryRib = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📜 Fiyat & Stok", 
            TextLine2 = "Tarihçesi",
            ImageLarge = RibbonIconFactory.CreateIcon("pricehistory", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("pricehistory", 16)
        };
        btnHistoryRib.Click += (s, e) => { ShowPage(1); OpenSelectedProductPriceHistory(); };

        var btnBulkDel = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🗑️ Çoklu Silme", 
            TextLine2 = "Seçilenleri Sil",
            ImageLarge = RibbonIconFactory.CreateIcon("bulkdelete", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("bulkdelete", 16)
        };
        btnBulkDel.Click += (s, e) => { ShowPage(1); DeleteProductsBulkAction(); };

        tripFast.Items.Add(btnFastEntry);
        tripFast.Items.Add(btnHistoryRib);
        tripFast.Items.Add(btnBulkDel);
        grpFast.Items.Add(tripFast);
        tabStock.Groups.Add(grpFast);

        var grpWh = new KryptonRibbonGroup { TextLine1 = "Depo & Envanter" };
        var tripWh = new KryptonRibbonGroupTriple();

        var btnWhManage = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🏢 Depo & Şube", 
            TextLine2 = "Yönetim",
            ImageLarge = RibbonIconFactory.CreateIcon("warehouse", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("warehouse", 16)
        };
        btnWhManage.Click += (s, e) => OpenWarehouseManage();

        var btnStockMov = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔄 Stok Hareketleri", 
            TextLine2 = "Giriş/Çıkış",
            ImageLarge = RibbonIconFactory.CreateIcon("stockmovement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("stockmovement", 16)
        };
        btnStockMov.Click += (s, e) => ShowPage(3);

        var btnCount = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚖️ Hızlı Sayım", 
            TextLine2 = "Envanter Eşitle",
            ImageLarge = RibbonIconFactory.CreateIcon("stockcount", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("stockcount", 16)
        };
        btnCount.Click += (s, e) => OpenStockCount();

        tripWh.Items.Add(btnWhManage);
        tripWh.Items.Add(btnStockMov);
        tripWh.Items.Add(btnCount);
        grpWh.Items.Add(tripWh);
        tabStock.Groups.Add(grpWh);

        _ribbon.RibbonTabs.Add(tabStock);

        // ==========================================
        // SEKME 4: 👥 CARİ & VERESİYE
        // ==========================================
        var tabAccounts = new KryptonRibbonTab { Text = "👥 Cari & Veresiye" };

        var grpAcc = new KryptonRibbonGroup { TextLine1 = "Cari Hesaplar" };
        var tripAcc = new KryptonRibbonGroupTriple();

        var btnAccList = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "👥 Cari Listesi", 
            TextLine2 = "Müşteri & Bayi",
            ImageLarge = RibbonIconFactory.CreateIcon("accounts", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("accounts", 16)
        };
        btnAccList.Click += (s, e) => ShowPage(2);

        var btnNewAcc = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "➕ Yeni Cari", 
            TextLine2 = "Müşteri Ekle",
            ImageLarge = RibbonIconFactory.CreateIcon("newaccount", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("newaccount", 16)
        };
        btnNewAcc.Click += (s, e) => AddAccount();

        var btnStmt = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📄 Hesap Ekstresi", 
            TextLine2 = "(F10 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnStmt.Click += (s, e) => { ShowPage(2); ViewAccountStatement(); };

        tripAcc.Items.Add(btnAccList);
        tripAcc.Items.Add(btnNewAcc);
        tripAcc.Items.Add(btnStmt);
        grpAcc.Items.Add(tripAcc);
        tabAccounts.Groups.Add(grpAcc);

        var grpAccActions = new KryptonRibbonGroup { TextLine1 = "Borç & Tahsilat" };
        var tripAccActions = new KryptonRibbonGroupTriple();

        var btnAccCollect = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💵 Tahsilat Al", 
            TextLine2 = "Ödeme Kaydet",
            ImageLarge = RibbonIconFactory.CreateIcon("collect", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("collect", 16)
        };
        btnAccCollect.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Tahsilat"); };

        var btnAccDebt = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "➕ Borç Yaz", 
            TextLine2 = "Veresiye Ekle",
            ImageLarge = RibbonIconFactory.CreateIcon("debt", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("debt", 16)
        };
        btnAccDebt.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Satış"); };

        var btnAccBulkDel = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🗑️ Çoklu Silme", 
            TextLine2 = "Seçilen Carileri Sil",
            ImageLarge = RibbonIconFactory.CreateIcon("bulkdelete", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("bulkdelete", 16)
        };
        btnAccBulkDel.Click += (s, e) => { ShowPage(2); DeleteAccountsBulkAction(); };

        tripAccActions.Items.Add(btnAccCollect);
        tripAccActions.Items.Add(btnAccDebt);
        tripAccActions.Items.Add(btnAccBulkDel);
        grpAccActions.Items.Add(tripAccActions);
        tabAccounts.Groups.Add(grpAccActions);

        var grpRisk = new KryptonRibbonGroup { TextLine1 = "Risk & Takip" };
        var tripRisk = new KryptonRibbonGroupTriple();

        var btnDue = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚠️ Vadesi Dolanlar", 
            TextLine2 = "Geciken Borçlar",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnDue.Click += (s, e) => OpenNotificationCenter(1);

        var btnNotifyAcc = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔔 SMS & Bildirim", 
            TextLine2 = "Otomatik Hatırlat",
            ImageLarge = RibbonIconFactory.CreateIcon("notification", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("notification", 16)
        };
        btnNotifyAcc.Click += (s, e) => OpenNotificationCenter();

        var btnAccStmtRisk = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📄 Hesap Ekstresi", 
            TextLine2 = "(F10 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnAccStmtRisk.Click += (s, e) => { ShowPage(2); ViewAccountStatement(); };

        tripRisk.Items.Add(btnDue);
        tripRisk.Items.Add(btnNotifyAcc);
        tripRisk.Items.Add(btnAccStmtRisk);
        grpRisk.Items.Add(tripRisk);
        tabAccounts.Groups.Add(grpRisk);

        _ribbon.RibbonTabs.Add(tabAccounts);

        // ==========================================
        // SEKME 5: 💳 FATURALAR & FİNANS
        // ==========================================
        var tabFin = new KryptonRibbonTab { Text = "💳 Faturalar & Finans" };

        var grpInv = new KryptonRibbonGroup { TextLine1 = "Faturalar" };
        var tripInv = new KryptonRibbonGroupTriple();

        var btnInvEntry = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📄 E-Fatura Girişi", 
            TextLine2 = "XML/PDF Aktar (F7)",
            ImageLarge = RibbonIconFactory.CreateIcon("invoiceentry", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("invoiceentry", 16)
        };
        btnInvEntry.Click += (s, e) => OpenInvoiceEntry();

        var btnInvList = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📋 Fatura Listesi", 
            TextLine2 = "Alış & Satış",
            ImageLarge = RibbonIconFactory.CreateIcon("invoices", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("invoices", 16)
        };
        btnInvList.Click += (s, e) => OpenInvoicesPage();

        var btnExcelInv = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📊 Excel Raporu", 
            TextLine2 = "Faturaları Aktar",
            ImageLarge = RibbonIconFactory.CreateIcon("excel", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("excel", 16)
        };
        btnExcelInv.Click += (s, e) => ExportInvoicesToExcel(null, EventArgs.Empty);

        tripInv.Items.Add(btnInvEntry);
        tripInv.Items.Add(btnInvList);
        tripInv.Items.Add(btnExcelInv);
        grpInv.Items.Add(tripInv);
        tabFin.Groups.Add(grpInv);

        var grpCashFlow = new KryptonRibbonGroup { TextLine1 = "Kasa & Nakit Akışı" };
        var tripCashFlow = new KryptonRibbonGroupTriple();

        var btnCashMov = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💳 Kasa Hareketleri", 
            TextLine2 = "Nakit Akışı & Kasa",
            ImageLarge = RibbonIconFactory.CreateIcon("cash", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("cash", 16)
        };
        btnCashMov.Click += (s, e) => ShowPage(4);

        var btnFinZ = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔒 Gün Sonu (Z)", 
            TextLine2 = "Kasa Kapat & Rapor",
            ImageLarge = RibbonIconFactory.CreateIcon("zreport", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("zreport", 16)
        };
        btnFinZ.Click += (s, e) => OpenDailyCashClosing();

        var btnCashCollect = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💵 Hızlı Tahsilat", 
            TextLine2 = "(F6 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("collect", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("collect", 16)
        };
        btnCashCollect.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Tahsilat"); };

        tripCashFlow.Items.Add(btnCashMov);
        tripCashFlow.Items.Add(btnFinZ);
        tripCashFlow.Items.Add(btnCashCollect);
        grpCashFlow.Items.Add(tripCashFlow);
        tabFin.Groups.Add(grpCashFlow);

        _ribbon.RibbonTabs.Add(tabFin);

        // ==========================================
        // SEKME 6: 📱 ARAÇLAR & MOBİL
        // ==========================================
        var tabMob = new KryptonRibbonTab { Text = "📱 Araçlar & Mobil" };

        var grpSmart = new KryptonRibbonGroup { TextLine1 = "Akıllı Cihaz & Bot" };
        var tripSmart = new KryptonRibbonGroupTriple();

        var btnMobScan = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📱 Mobil Barkod", 
            TextLine2 = "Kamera Okuyucu",
            ImageLarge = RibbonIconFactory.CreateIcon("mobilescanner", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("mobilescanner", 16)
        };
        btnMobScan.Click += (s, e) => OpenMobileScanner();

        var btnTg = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🤖 Telegram Botu", 
            TextLine2 = "Cep Takip & Rapor",
            ImageLarge = RibbonIconFactory.CreateIcon("telegram", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("telegram", 16)
        };
        btnTg.Click += (s, e) => OpenTelegramConfig();

        var btnNotifCenter = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔔 SMS & Bildirim", 
            TextLine2 = "Mesaj Merkezi",
            ImageLarge = RibbonIconFactory.CreateIcon("notification", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("notification", 16)
        };
        btnNotifCenter.Click += (s, e) => OpenNotificationCenter();

        tripSmart.Items.Add(btnMobScan);
        tripSmart.Items.Add(btnTg);
        tripSmart.Items.Add(btnNotifCenter);
        grpSmart.Items.Add(tripSmart);
        tabMob.Groups.Add(grpSmart);

        _ribbon.RibbonTabs.Add(tabMob);

        // ==========================================
        // SEKME 7: ⚙️ SİSTEM & YÖNETİM
        // ==========================================
        var tabSys = new KryptonRibbonTab { Text = "⚙️ Sistem & Yönetim" };

        var grpSys = new KryptonRibbonGroup { TextLine1 = "Sistem & Güvenlik" };
        var tripSys = new KryptonRibbonGroupTriple();

        var btnUsers = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "👤 Kullanıcılar", 
            TextLine2 = "Rol & Depo Yetki",
            ImageLarge = RibbonIconFactory.CreateIcon("users", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("users", 16)
        };
        btnUsers.Click += (s, e) => ShowPage(6);

        var btnLogs = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "📜 İşlem Logları", 
            TextLine2 = "Denetim İzi & Audit",
            ImageLarge = RibbonIconFactory.CreateIcon("auditlogs", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("auditlogs", 16)
        };
        btnLogs.Click += (s, e) => ShowPage(5);

        var btnBackup = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "💾 SQL Yedek Al", 
            TextLine2 = "Veritabanı Yedek",
            ImageLarge = RibbonIconFactory.CreateIcon("settings", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("settings", 16)
        };
        btnBackup.Click += (s, e) => BackupDatabaseClick(null, EventArgs.Empty);

        tripSys.Items.Add(btnUsers);
        tripSys.Items.Add(btnLogs);
        tripSys.Items.Add(btnBackup);
        grpSys.Items.Add(tripSys);
        tabSys.Groups.Add(grpSys);

        var grpCloud = new KryptonRibbonGroup { TextLine1 = "Bulut & Harici" };
        var tripCloud = new KryptonRibbonGroupTriple();

        var btnCloudBackup = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "☁️ Bulut Yedekle", 
            TextLine2 = "Google Drive / Zip",
            ImageLarge = RibbonIconFactory.CreateIcon("closing", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("closing", 16)
        };
        btnCloudBackup.Click += (s, e) =>
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.CloudBackup) && !curUser.IsSuperUser)
            {
                MessageBox.Show("Bu işlem için 'Otomatik Bulut Yedekleme' yetkiniz bulunmuyor.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var dlg = new CloudBackupManageDialog();
            dlg.ShowDialog(this);
        };

        tripCloud.Items.Add(btnCloudBackup);
        grpCloud.Items.Add(tripCloud);
        tabSys.Groups.Add(grpCloud);

        var grpConfig = new KryptonRibbonGroup { TextLine1 = "Yapılandırma & Menü" };
        var tripConfig = new KryptonRibbonGroupTriple();

        var btnSettings = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "⚙️ Genel Ayarlar", 
            TextLine2 = "Sistem & Donanım",
            ImageLarge = RibbonIconFactory.CreateIcon("settings", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("settings", 16)
        };
        btnSettings.Click += (s, e) => ShowPage(8);

        var btnLicense = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🔑 Lisans Durumu", 
            TextLine2 = "Aktivasyon & Süre",
            ImageLarge = RibbonIconFactory.CreateIcon("license", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("license", 16)
        };
        btnLicense.Click += (s, e) => ShowPage(7);

        var btnTheme = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🎨 Tema Değiştir", 
            TextLine2 = "Görünüm Ayarları",
            ImageLarge = RibbonIconFactory.CreateIcon("theme", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("theme", 16)
        };
        btnTheme.Click += (s, e) => ShowPage(8);

        tripConfig.Items.Add(btnSettings);
        tripConfig.Items.Add(btnLicense);
        tripConfig.Items.Add(btnTheme);
        grpConfig.Items.Add(tripConfig);

        var tripPuzzle = new KryptonRibbonGroupTriple();
        var btnPuzzle = new KryptonRibbonGroupButton
        {
            TextLine1 = "🧩 Menü Düzenle",
            TextLine2 = "Puzzle / Kilitle",
            ImageLarge = RibbonIconFactory.CreateIcon("settings", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("settings", 16)
        };
        btnPuzzle.Click += (s, e) =>
        {
            using var dlg = new RibbonCustomizerDialog(_ribbon, () => { });
            dlg.ShowDialog(this);
        };
        tripPuzzle.Items.Add(btnPuzzle);
        grpConfig.Items.Add(tripPuzzle);

        tabSys.Groups.Add(grpConfig);

        var grpLogout = new KryptonRibbonGroup { TextLine1 = "Oturum" };
        var tripLogout = new KryptonRibbonGroupTriple();

        var btnLogout = new KryptonRibbonGroupButton 
        { 
            TextLine1 = "🚪 Oturumu Kapat", 
            TextLine2 = "Güvenli Çıkış",
            ImageLarge = RibbonIconFactory.CreateIcon("logout", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("logout", 16)
        };
        btnLogout.Click += (s, e) => LogoutAction();

        tripLogout.Items.Add(btnLogout);

        if (curUser?.IsSuperUser == true)
        {
            var btnKeygen = new KryptonRibbonGroupButton 
            { 
                TextLine1 = "👑 Lisans Üretici", 
                TextLine2 = "Süper Yönetici",
                ImageLarge = RibbonIconFactory.CreateIcon("license", 32),
                ImageSmall = RibbonIconFactory.CreateIcon("license", 16)
            };
            btnKeygen.Click += (s, e) => ShowPage(99);
            tripLogout.Items.Add(btnKeygen);
        }

        grpLogout.Items.Add(tripLogout);
        tabSys.Groups.Add(grpLogout);

        _ribbon.RibbonTabs.Add(tabSys);
        RibbonCustomizerDialog.ApplyToRibbon(_ribbon);
    }

    private void LogoutAction()
    {
        if (MessageBox.Show("Oturumu kapatmak istediğinize emin misiniz?", "Oturumu Kapat", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            UserService.Logout();
            IsLoggedOut = true;
            Close();
        }
    }

    private void OpenDailyCashClosing()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.DailyRegister) && !curUser.HasPermission(UserPermissions.AccountMovements))
        {
            MessageBox.Show("Kasa gün sonu ve Z raporu kapatma modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new DailyCashClosingDialog();
        dlg.ShowDialog(this);
        RefreshDashboard();
        RefreshAccountMovements();
    }

    private void OpenNotificationCenter(int initialTab = 0)
    {
        using var dlg = new NotificationCenterDialog(initialTab);
        dlg.ShowDialog(this);
        UpdateNotificationBadge();
        RefreshDashboard();
    }

    private void UpdateNotificationBadge()
    {
        try
        {
            var counts = NotificationService.GetAlertCounts();
            if (_ribbonBtnNotify != null)
            {
                if (counts.TotalAlertCount > 0)
                {
                    _ribbonBtnNotify.TextLine1 = $"🔔 Bildirim ({counts.TotalAlertCount})";
                    _ribbonBtnNotify.TextLine2 = "Kritik Uyarı!";
                }
                else
                {
                    _ribbonBtnNotify.TextLine1 = "🔔 Bildirimler";
                    _ribbonBtnNotify.TextLine2 = "Kritik Uyarılar";
                }
            }
        }
        catch { }
    }

    private void ShowPage(int index)
    {
        if (index == 99)
        {
            if (UserService.CurrentUser?.IsSuperUser == true)
            {
                using var gen = new LicenseGeneratorForm();
                gen.ShowDialog(this);
                RefreshLicensePage();
            }
            return;
        }

        var currentUser = UserService.CurrentUser;
        if (currentUser != null)
        {
            if (index == 1 && !currentUser.HasPermission(UserPermissions.Products))
            {
                MessageBox.Show("Ürün ve stok modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 2 && !currentUser.HasPermission(UserPermissions.Accounts))
            {
                MessageBox.Show("Cari ve veresiye modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 3 && !currentUser.HasPermission(UserPermissions.StockMovements))
            {
                MessageBox.Show("Stok hareketleri modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 4 && !currentUser.HasPermission(UserPermissions.AccountMovements))
            {
                MessageBox.Show("Kasa ve finansal hareketler modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 5 && !currentUser.HasPermission(UserPermissions.AuditLogs))
            {
                MessageBox.Show("İşlem logları modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 6 && !currentUser.HasPermission(UserPermissions.Users))
            {
                MessageBox.Show("Kullanıcı ve yetki yönetimi modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 7 && !currentUser.HasPermission(UserPermissions.License) && !currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Lisans yönetimi ve anahtar üreticiye erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (index == 8 && !currentUser.HasPermission(UserPermissions.SettingsBackup) && !currentUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !currentUser.IsSuperUser)
            {
                MessageBox.Show("Sistem ayarları ve veritabanı yedekleme modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        _currentNavIndex = index;

        _contentArea.Controls.Clear();
        switch (index)
        {
            case 0:
                _contentArea.Controls.Add(_pnlDashboard);
                RefreshDashboard();
                break;
            case 1:
                _contentArea.Controls.Add(_pnlProducts);
                SyncProductCategories();
                RefreshProducts();
                break;
            case 2:
                _contentArea.Controls.Add(_pnlAccounts);
                RefreshAccounts();
                break;
            case 3:
                _contentArea.Controls.Add(_pnlStockMovements);
                RefreshStockMovements();
                break;
            case 4:
                _contentArea.Controls.Add(_pnlAccountMovements);
                RefreshAccountMovements();
                RefreshInvoices();
                break;
            case 5:
                _contentArea.Controls.Add(_pnlAuditLogs);
                RefreshAuditLogs();
                break;
            case 6:
                _contentArea.Controls.Add(_pnlUsers);
                RefreshUsers();
                break;
            case 7:
                _contentArea.Controls.Add(_pnlLicense);
                RefreshLicensePage();
                break;
            case 8:
                _contentArea.Controls.Add(_pnlSettings);
                break;
        }
    }

    #region 1. Dashboard Page
    private void BuildDashboardPage()
    {
        _pnlDashboard = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        // Başlık
        var header = CreatePageHeader("Kontrol Paneli (Genel Bakış)", "İşletmenizin anlık stok, alacak, borç ve kasa durumları");

        // KPI Kartları Paneli
        var cardsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 115,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 5, 0, 5)
        };

        _cardStockVal = new StatCard { Title = "TOPLAM STOK DEĞERİ", ValueText = "0,00 ₺", SubText = "Mevcut Depo Varlığı", AccentColor = UITheme.Primary, Width = 260 };
        _cardReceivable = new StatCard { Title = "MÜŞTERİ ALACAKLARI (VERESİYE)", ValueText = "0,00 ₺", SubText = "Tahsil Edilecek Tutar", AccentColor = UITheme.Success, Width = 280 };
        _cardPayable = new StatCard { Title = "TEDARİKÇİ BORÇLARI", ValueText = "0,00 ₺", SubText = "Ödenecek Tutar", AccentColor = UITheme.Danger, Width = 260 };
        _cardTodayCash = new StatCard { Title = "BUGÜNKÜ NET KASA GİRİŞİ", ValueText = "0,00 ₺", SubText = "Günlük Net Nakit Akışı", AccentColor = UITheme.Warning, Width = 260 };
        _cardOverdue = new StatCard { Title = "VADESİ GEÇEN ALACAKLAR", ValueText = "0,00 ₺", SubText = "0 Kişi (Detay İçin Tıkla)", AccentColor = Color.FromArgb(220, 38, 38), Width = 270 };
        _cardOverdue.Cursor = Cursors.Hand;
        _cardOverdue.Click += (s, e) => OpenNotificationCenter(1);

        cardsFlow.Controls.Add(_cardStockVal);
        cardsFlow.Controls.Add(_cardReceivable);
        cardsFlow.Controls.Add(_cardPayable);
        cardsFlow.Controls.Add(_cardTodayCash);
        cardsFlow.Controls.Add(_cardOverdue);

        // İki Kolonlu Alt Tablolar Paneli (Kritik Stok & Borçlu Müşteriler)
        var splitTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 360,
            ColumnCount = 2,
            Padding = new Padding(0, 10, 0, 10)
        };
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Sol: Kritik Stok Paneli
        var pnlCrit = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
        var lblCritTitle = new Label { Text = "⚠️ Kritik Stok Uyarıları (Detay / SMS İçin Tıkla)", Font = UITheme.TitleFont, ForeColor = UITheme.Danger, Dock = DockStyle.Top, Height = 30, Cursor = Cursors.Hand };
        lblCritTitle.Click += (s, e) => OpenNotificationCenter(0);
        UITheme.ApplyGridStyle(_gridCriticalStock);
        _gridCriticalStock.CellDoubleClick += (s, e) => OpenNotificationCenter(0);
        pnlCrit.Controls.Add(_gridCriticalStock);
        pnlCrit.Controls.Add(lblCritTitle);
        lblCritTitle.SendToBack();
        _gridCriticalStock.BringToFront();
        splitTable.Controls.Add(pnlCrit, 0, 0);

        // Sağ: En Çok Borcu Olan Müşteriler Paneli
        var pnlDebtors = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };
        var lblDebtorTitle = new Label { Text = "💰 En Yüksek Bakiyeli Müşteriler (Veresiye Alacaklar)", Font = UITheme.TitleFont, ForeColor = UITheme.Primary, Dock = DockStyle.Top, Height = 30 };
        UITheme.ApplyGridStyle(_gridTopDebtors);
        pnlDebtors.Controls.Add(_gridTopDebtors);
        pnlDebtors.Controls.Add(lblDebtorTitle);
        lblDebtorTitle.SendToBack();
        _gridTopDebtors.BringToFront();
        splitTable.Controls.Add(pnlDebtors, 1, 0);

        // Son Hareketler Paneli
        var pnlRecent = new CardPanel { Dock = DockStyle.Top, Height = 250, Margin = new Padding(0, 10, 0, 15) };
        var lblRecentTitle = new Label { Text = "🕒 Son Gerçekleşen Finansal & Cari İşlemler", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        UITheme.ApplyGridStyle(_gridRecentTransactions);
        pnlRecent.Controls.Add(_gridRecentTransactions);
        pnlRecent.Controls.Add(lblRecentTitle);
        lblRecentTitle.SendToBack();
        _gridRecentTransactions.BringToFront();

        // Dashboard Hızlı Ürün Arama & Satış Kartı
        var pnlDashSearch = new CardPanel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 0, 0, 15) };
        var pnlDashSearchTop = new Panel { Dock = DockStyle.Top, Height = 36 };
        var lblDashSearchTitle = new Label 
        { 
            Text = "🔍 Hızlı Ürün Arama & Anında Satış (Çift Tıklayarak Satış Yapın):", 
            Font = UITheme.TitleFont, 
            ForeColor = UITheme.Primary, 
            Dock = DockStyle.Left, 
            AutoSize = true, 
            TextAlign = ContentAlignment.MiddleLeft 
        };
        _txtDashProductSearch.Width = 320;
        _txtDashProductSearch.Dock = DockStyle.Right;
        _txtDashProductSearch.PlaceholderText = "🔍 Ürün Adı, Barkod veya Kod yazın...";

        var btnDashSale = UITheme.CreateButton("⚡ Hızlı Satış Yap", UITheme.Primary, Color.White, (s, e) =>
        {
            if (_gridDashProductSearch.CurrentRow != null && _gridDashProductSearch.Columns.Contains("Id"))
            {
                long pid = Convert.ToInt64(_gridDashProductSearch.CurrentRow.Cells["Id"].Value);
                OpenQuickSale("Satış", pid);
            }
            else
            {
                OpenQuickSale("Satış");
            }
        }, 130, 32);
        btnDashSale.Dock = DockStyle.Right;
        btnDashSale.Margin = new Padding(8, 0, 0, 0);

        pnlDashSearchTop.Controls.Add(lblDashSearchTitle);
        pnlDashSearchTop.Controls.Add(_txtDashProductSearch);
        pnlDashSearchTop.Controls.Add(btnDashSale);

        UITheme.ApplyGridStyle(_gridDashProductSearch);
        _gridDashProductSearch.Dock = DockStyle.Fill;
        _gridDashProductSearch.CellDoubleClick += (s, e) =>
        {
            if (_gridDashProductSearch.CurrentRow != null && _gridDashProductSearch.Columns.Contains("Id"))
            {
                long pid = Convert.ToInt64(_gridDashProductSearch.CurrentRow.Cells["Id"].Value);
                OpenQuickSale("Satış", pid);
            }
        };

        _txtDashProductSearch.TextChanged += (s, e) =>
        {
            string q = _txtDashProductSearch.Text.Trim();
            if (q.Length >= 1)
            {
                try
                {
                    var dt = ProductService.GetAllProducts(search: q);
                    _gridDashProductSearch.DataSource = dt;
                    if (_gridDashProductSearch.Columns["Id"] is { } colId) colId.Visible = false;
                    if (_gridDashProductSearch.Columns["Satış Fiyatı"] is { } colSale)
                    {
                        colSale.DefaultCellStyle.Format = "N2";
                        colSale.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    if (_gridDashProductSearch.Columns["Alış Fiyatı"] is { } colPur)
                    {
                        colPur.DefaultCellStyle.Format = "N2";
                        colPur.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                }
                catch { }
            }
            else
            {
                _gridDashProductSearch.DataSource = null;
            }
        };

        pnlDashSearch.Controls.Add(_gridDashProductSearch);
        pnlDashSearch.Controls.Add(pnlDashSearchTop);
        pnlDashSearchTop.SendToBack();
        _gridDashProductSearch.BringToFront();

        // Dashboard Bölüm Referanslarını Sınıf Seviyesine Ata
        _dashCardsFlow = cardsFlow;
        _dashPnlSearch = pnlDashSearch;
        _dashSplitTable = splitTable;
        _dashPnlCrit = pnlCrit;
        _dashPnlDebtors = pnlDebtors;
        _dashPnlRecent = pnlRecent;

        // Görünüm / Widget Ayarları Çubuğu
        var pnlCustomizer = new CardPanel 
        { 
            Dock = DockStyle.Top, 
            Height = 44, 
            Padding = new Padding(10, 6, 10, 6), 
            Margin = new Padding(0, 0, 0, 10) 
        };
        var flowCustomizer = new FlowLayoutPanel 
        { 
            Dock = DockStyle.Fill, 
            FlowDirection = FlowDirection.LeftToRight, 
            AutoScroll = true 
        };

        var lblCustTitle = new Label 
        { 
            Text = "👁️ Ekran Düzeni:", 
            Font = new Font(UITheme.RegularFont, FontStyle.Bold), 
            ForeColor = UITheme.Primary, 
            AutoSize = true, 
            Margin = new Padding(0, 5, 8, 0) 
        };

        _cmbDashLayoutMode.Width = 235;
        _cmbDashLayoutMode.Items.Clear();
        _cmbDashLayoutMode.Items.AddRange(new object[] 
        { 
            "🌐 Standart (Tüm Bölümler Açık)",
            "⚠️ Sadece Kritik Stoklar (Tam Odak)",
            "🕒 Sadece Son Finansallar (Tam Odak)",
            "💰 Sadece Borçlu Müşteriler (Tam Odak)",
            "🔍 Sadece Hızlı Ürün Satışı (Tam Odak)"
        });
        _cmbDashLayoutMode.SelectedIndex = 0;
        _cmbDashLayoutMode.SelectedIndexChanged += (s, e) => ApplyDashboardLayout();

        _chkDashCards.Margin = new Padding(12, 5, 6, 0);
        _chkDashSearch.Margin = new Padding(6, 5, 6, 0);
        _chkDashCrit.Margin = new Padding(6, 5, 6, 0);
        _chkDashDebtors.Margin = new Padding(6, 5, 6, 0);
        _chkDashRecent.Margin = new Padding(6, 5, 6, 0);

        _chkDashCards.CheckedChanged += (s, e) => ApplyDashboardCustomCheckboxes();
        _chkDashSearch.CheckedChanged += (s, e) => ApplyDashboardCustomCheckboxes();
        _chkDashCrit.CheckedChanged += (s, e) => ApplyDashboardCustomCheckboxes();
        _chkDashDebtors.CheckedChanged += (s, e) => ApplyDashboardCustomCheckboxes();
        _chkDashRecent.CheckedChanged += (s, e) => ApplyDashboardCustomCheckboxes();

        flowCustomizer.Controls.Add(lblCustTitle);
        flowCustomizer.Controls.Add(_cmbDashLayoutMode);
        flowCustomizer.Controls.Add(new Label { Text = "|", AutoSize = true, Margin = new Padding(8, 4, 8, 0), ForeColor = UITheme.TextMuted });
        flowCustomizer.Controls.Add(_chkDashCards);
        flowCustomizer.Controls.Add(_chkDashSearch);
        flowCustomizer.Controls.Add(_chkDashCrit);
        flowCustomizer.Controls.Add(_chkDashDebtors);
        flowCustomizer.Controls.Add(_chkDashRecent);
        pnlCustomizer.Controls.Add(flowCustomizer);

        // Kontrolleri doğrudan Dashboard Paneline Ekle
        _dashHeader = header;
        _dashPnlCustomizer = pnlCustomizer;
        _dashPnlCustomizer.Visible = false; // Ribbon menüye taşındığı için sayfa içinde yer kaplamaz

        _pnlDashboard.Controls.Clear();
        _pnlDashboard.Controls.Add(_dashHeader);
        _pnlDashboard.Controls.Add(_dashCardsFlow);
        _pnlDashboard.Controls.Add(_dashPnlSearch);
        _pnlDashboard.Controls.Add(_dashSplitTable);
        _pnlDashboard.Controls.Add(_dashPnlRecent);

        _pnlDashboard.Resize += (s, e) => LayoutDashboard();
        LayoutDashboard();

        LoadDashboardLayoutPreference();
    }

    private void SetDashboardLayout(int index)
    {
        if (index >= 0 && index < _cmbDashLayoutMode.Items.Count)
        {
            _cmbDashLayoutMode.SelectedIndex = index;
        }
        ShowPage(0);
        LayoutDashboard();
    }

    private void LayoutDashboard()
    {
        if (_pnlDashboard == null || _pnlDashboard.IsDisposed) return;
        _pnlDashboard.SuspendLayout();
        try
        {
            int targetW = Math.Max(760, _pnlDashboard.ClientSize.Width - 28);
            int curY = 12;

            // KPI Kartlarını ekran genişliğine göre orantılı paylaştır
            if (_dashCardsFlow != null && _dashCardsFlow.Visible)
            {
                int cardCount = 5;
                int cardW = Math.Clamp((targetW - 48) / cardCount, 220, 310);
                if (_cardStockVal != null) _cardStockVal.Width = cardW;
                if (_cardReceivable != null) _cardReceivable.Width = cardW;
                if (_cardPayable != null) _cardPayable.Width = cardW;
                if (_cardTodayCash != null) _cardTodayCash.Width = cardW;
                if (_cardOverdue != null) _cardOverdue.Width = cardW;
            }

            Control?[] items =
            [
                _dashHeader,
                _dashCardsFlow,
                _dashPnlSearch,
                _dashSplitTable,
                _dashPnlRecent
            ];

            foreach (var c in items)
            {
                if (c == null || !c.Visible) continue;
                c.Dock = DockStyle.None;
                c.Location = new Point(12, curY);
                c.Width = targetW;
                curY += c.Height + 12;
            }

            _pnlDashboard.AutoScrollMinSize = new Size(760, curY + 20);
        }
        finally
        {
            _pnlDashboard.ResumeLayout(true);
        }
    }

    private void ApplyDashboardLayout()
    {
        string mode = _cmbDashLayoutMode.SelectedItem?.ToString() ?? "🌐 Standart (Tüm Bölümler Açık)";
        if (mode.Contains("Kritik Stoklar"))
        {
            _dashCardsFlow.Visible = false;
            _dashPnlSearch.Visible = false;
            _dashPnlRecent.Visible = false;
            _dashSplitTable.Visible = true;
            _dashSplitTable.Height = 550;
            _dashPnlCrit.Visible = true;
            _dashPnlDebtors.Visible = false;
            _dashSplitTable.ColumnStyles[0].Width = 100;
            _dashSplitTable.ColumnStyles[1].Width = 0;
        }
        else if (mode.Contains("Son Finansallar"))
        {
            _dashCardsFlow.Visible = false;
            _dashPnlSearch.Visible = false;
            _dashSplitTable.Visible = false;
            _dashPnlRecent.Visible = true;
            _dashPnlRecent.Height = 550;
        }
        else if (mode.Contains("Borçlu Müşteriler"))
        {
            _dashCardsFlow.Visible = false;
            _dashPnlSearch.Visible = false;
            _dashPnlRecent.Visible = false;
            _dashSplitTable.Visible = true;
            _dashSplitTable.Height = 550;
            _dashPnlCrit.Visible = false;
            _dashPnlDebtors.Visible = true;
            _dashSplitTable.ColumnStyles[0].Width = 0;
            _dashSplitTable.ColumnStyles[1].Width = 100;
        }
        else if (mode.Contains("Hızlı Ürün Satışı"))
        {
            _dashCardsFlow.Visible = false;
            _dashSplitTable.Visible = false;
            _dashPnlRecent.Visible = false;
            _dashPnlSearch.Visible = true;
            _dashPnlSearch.Height = 550;
        }
        else
        {
            // Standart Mod: Checkbox'lardaki seçimlere dön
            _dashSplitTable.Height = 360;
            _dashPnlRecent.Height = 250;
            _dashPnlSearch.Height = 190;
            _dashSplitTable.ColumnStyles[0].Width = 50;
            _dashSplitTable.ColumnStyles[1].Width = 50;
            ApplyDashboardCustomCheckboxes();
        }
        LayoutDashboard();
        SaveDashboardLayoutPreference();
    }

    private void ApplyDashboardCustomCheckboxes()
    {
        if (_cmbDashLayoutMode.SelectedIndex != 0)
        {
            _cmbDashLayoutMode.SelectedIndex = 0;
            return;
        }

        _dashCardsFlow.Visible = _chkDashCards.Checked;
        _dashPnlSearch.Visible = _chkDashSearch.Checked;
        _dashPnlCrit.Visible = _chkDashCrit.Checked;
        _dashPnlDebtors.Visible = _chkDashDebtors.Checked;
        _dashSplitTable.Visible = _chkDashCrit.Checked || _chkDashDebtors.Checked;

        if (_chkDashCrit.Checked && !_chkDashDebtors.Checked)
        {
            _dashSplitTable.ColumnStyles[0].Width = 100;
            _dashSplitTable.ColumnStyles[1].Width = 0;
        }
        else if (!_chkDashCrit.Checked && _chkDashDebtors.Checked)
        {
            _dashSplitTable.ColumnStyles[0].Width = 0;
            _dashSplitTable.ColumnStyles[1].Width = 100;
        }
        else
        {
            _dashSplitTable.ColumnStyles[0].Width = 50;
            _dashSplitTable.ColumnStyles[1].Width = 50;
        }

        _dashPnlRecent.Visible = _chkDashRecent.Checked;

        LayoutDashboard();
        SaveDashboardLayoutPreference();
    }

    private void SaveDashboardLayoutPreference()
    {
        try
        {
            string cfgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp");
            Directory.CreateDirectory(cfgDir);
            string file = Path.Combine(cfgDir, "dashboard_layout.txt");
            string content = $"{_cmbDashLayoutMode.SelectedIndex};{_chkDashCards.Checked};{_chkDashSearch.Checked};{_chkDashCrit.Checked};{_chkDashDebtors.Checked};{_chkDashRecent.Checked}";
            File.WriteAllText(file, content);
        }
        catch { }
    }

    private void LoadDashboardLayoutPreference()
    {
        try
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp", "dashboard_layout.txt");
            if (File.Exists(file))
            {
                var parts = File.ReadAllText(file).Split(';');
                if (parts.Length >= 6)
                {
                    if (int.TryParse(parts[0], out int idx) && idx >= 0 && idx < _cmbDashLayoutMode.Items.Count)
                    {
                        _cmbDashLayoutMode.SelectedIndex = idx;
                    }
                    _chkDashCards.Checked = bool.Parse(parts[1]);
                    int offset = parts.Length >= 7 ? 1 : 0;
                    _chkDashSearch.Checked = bool.Parse(parts[2 + offset]);
                    _chkDashCrit.Checked = bool.Parse(parts[3 + offset]);
                    _chkDashDebtors.Checked = bool.Parse(parts[4 + offset]);
                    _chkDashRecent.Checked = bool.Parse(parts[5 + offset]);
                    ApplyDashboardLayout();
                }
            }
        }
        catch { }
    }
    #endregion

    #region 2. Products Page
    private static readonly Color CriticalBgColor = Color.FromArgb(254, 226, 226);
    private static readonly Color CriticalFgColor = Color.FromArgb(185, 28, 28);
    private static readonly Font CriticalFont = new(UITheme.RegularFont, FontStyle.Bold);

    private void BuildProductsPage()
    {
        _pnlProducts = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Ürün & Stok Yönetimi", "Tüm ürün tanımları, barkodlar, birimler, alış/satış fiyatları ve anlık depo miktarları");
        header.Dock = DockStyle.Top;
        header.Height = 65;

        // Filtre ve Buton Paneli (Krypton Ribbon Tarzı Kompakt, Kurumsal ve Tek Satırlı Araç Çubuğu)
        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 8, 12, 8) };

        // Sol: Filtreler
        var rowFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };

        _txtProductSearch.Width = 240;
        _txtProductSearch.Height = 32;
        _txtProductSearch.PlaceholderText = "🔍 Ürün Ara (Ad, Kod, Barkod)...";
        _txtProductSearch.Font = UITheme.RegularFont;
        _txtProductSearch.TextChanged += (s, e) => RefreshProducts();

        _cmbProductCategory.Width = 140;
        _cmbProductCategory.Height = 32;
        _cmbProductCategory.Font = UITheme.RegularFont;
        _cmbProductCategory.SelectedIndexChanged += ProductCategoryChanged;

        _cmbProductWarehouse.Width = 150;
        _cmbProductWarehouse.Height = 32;
        _cmbProductWarehouse.Font = UITheme.RegularFont;
        _cmbProductWarehouse.SelectedIndexChanged += (s, e) => RefreshProducts();

        _chkOnlyCritical.Text = "⚠️ Kritik Stok";
        _chkOnlyCritical.Font = UITheme.RegularFont;
        _chkOnlyCritical.AutoSize = true;
        _chkOnlyCritical.Margin = new Padding(8, 6, 8, 0);
        _chkOnlyCritical.CheckedChanged += (s, e) => RefreshProducts();

        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshProducts(), 40, 32);

        rowFilters.Controls.Add(_txtProductSearch);
        rowFilters.Controls.Add(_cmbProductCategory);
        rowFilters.Controls.Add(_cmbProductWarehouse);
        rowFilters.Controls.Add(_chkOnlyCritical);
        rowFilters.Controls.Add(btnRefresh);

        // Sağ: Kurumsal Aksiyon Butonları
        var rowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var btnAdd = UITheme.CreateButton("➕ Yeni Ürün", UITheme.Primary, Color.White, (s, e) => AddProduct(), 125, 32);
        btnAdd.Margin = new Padding(0, 0, 6, 0);

        var btnFastEntry = UITheme.CreateButton("⚡ Hızlı Giriş", Color.FromArgb(16, 185, 129), Color.White, (s, e) => OpenFastProductEntry(), 120, 32);
        btnFastEntry.Margin = new Padding(0, 0, 6, 0);

        // Diğer Tüm İşlemler İçin Kurumsal Dropdown Menü (Ribbon Tarzı Kompakt)
        var mnuTools = new ContextMenuStrip();
        mnuTools.Items.Add("📜 Stok & Fiyat Değişim Tarihçesi", null, (s, e) => OpenSelectedProductPriceHistory());
        mnuTools.Items.Add("📋 Stok Hareket Detayı", null, (s, e) => ViewProductHistory());
        mnuTools.Items.Add("🏷️ Barkod & Fiyat Etiketi Bas", null, (s, e) => PrintBarcodeLabelForSelected());
        mnuTools.Items.Add("🔄 Depolar Arası Transfer", null, (s, e) => OpenWarehouseTransfer());
        mnuTools.Items.Add(new ToolStripSeparator());
        mnuTools.Items.Add("📄 E-Fatura Girişi Yap", null, (s, e) => OpenInvoiceEntry());
        mnuTools.Items.Add("📄 Fatura Bilgileri & Diğer Kalemler", null, (s, e) => OpenSelectedProductInvoiceMetaDialog());
        mnuTools.Items.Add("👁️ Fatura PDF Belgesini Aç", null, (s, e) => OpenSelectedProductInvoicePdf());
        mnuTools.Items.Add("📱 Mobil Canlı Barkod Okuyucu", null, (s, e) => OpenMobileScanner());
        mnuTools.Items.Add(new ToolStripSeparator());
        mnuTools.Items.Add("📥 Excel'den Ürün Yükle", null, (s, e) => OpenExcelImport("Urun"));
        mnuTools.Items.Add("📊 Excel Listesi Olarak İndir", null, ExportProductsToExcel);
        mnuTools.Items.Add(new ToolStripSeparator());
        mnuTools.Items.Add("🗑️ Seçilen Ürünleri Toplu Sil", null, (s, e) => DeleteProductsBulkAction());
        mnuTools.Items.Add("🗑️ Ürünü Pasife Al / Sil", null, (s, e) => DeleteProduct());

        var btnTools = UITheme.CreateButton("⚙️ İşlemler ▾", UITheme.Secondary, Color.White, (s, e) =>
        {
            if (s is Control btn)
            {
                mnuTools.Show(btn, new Point(0, btn.Height));
            }
        }, 115, 32);

        rowActions.Controls.Add(btnAdd);
        rowActions.Controls.Add(btnFastEntry);
        rowActions.Controls.Add(btnTools);

        toolbar.Controls.Add(rowFilters);
        toolbar.Controls.Add(rowActions);

        // Grid
        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        UITheme.ApplyGridStyle(_gridProducts);
        _gridProducts.MultiSelect = true;
        _gridProducts.CellDoubleClick += (s, e) => EditProduct();
        _gridProducts.CellFormatting += GridProducts_CellFormatting;

        // Sağ Tık Menüsü (Fatura Meta Bilgileri, Diğer Kalemler & PDF Önizleme dahil)
        var mnuProduct = new ContextMenuStrip();
        var itemMeta = new ToolStripMenuItem("📄 Fatura Meta Bilgileri ve Faturadaki Diğer Ürünler", null, (s, e) => OpenSelectedProductInvoiceMetaDialog());
        itemMeta.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        itemMeta.ForeColor = Color.FromArgb(13, 148, 136);
        mnuProduct.Items.Add(itemMeta);

        var itemPdf = new ToolStripMenuItem("👁️ İlgili Fatura PDF Belgesini Aç", null, (s, e) => OpenSelectedProductInvoicePdf());
        mnuProduct.Items.Add(itemPdf);
        mnuProduct.Items.Add(new ToolStripSeparator());
        mnuProduct.Items.Add("⚡ Hızlı Manuel Seri Ürün Girişi", null, (s, e) => OpenFastProductEntry());
        mnuProduct.Items.Add("📜 Stok & Fiyat Değişim Tarihçesi", null, (s, e) => OpenSelectedProductPriceHistory());
        mnuProduct.Items.Add(new ToolStripSeparator());
        mnuProduct.Items.Add("📱 Mobil Canlı Barkod Okuyucu (QR Aç)", null, (s, e) => OpenMobileScanner());
        mnuProduct.Items.Add(new ToolStripSeparator());
        mnuProduct.Items.Add("✏️ Ürün Kartını Düzenle", null, (s, e) => EditProduct());
        mnuProduct.Items.Add("📋 Stok Hareket Geçmişi", null, (s, e) => ViewProductHistory());
        mnuProduct.Items.Add("🏷️ Barkod / Fiyat Etiketi Bas", null, (s, e) => PrintBarcodeLabelForSelected());
        mnuProduct.Items.Add(new ToolStripSeparator());
        mnuProduct.Items.Add("🗑️ Seçilen Ürünleri Toplu Sil (Çoklu)", null, (s, e) => DeleteProductsBulkAction());
        mnuProduct.Items.Add("🗑️ Ürünü Sil / Pasife Al", null, (s, e) => DeleteProduct());
        _gridProducts.ContextMenuStrip = mnuProduct;

        // Fatura No hücresine tıklandığında anında Fatura Meta Bilgisi & Faturadaki Ürünler popup'ı açılır
        _gridProducts.CellContentClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _gridProducts.Columns["Fatura No"] is { } colInv && e.ColumnIndex == colInv.Index)
            {
                OpenSelectedProductInvoiceMetaDialog();
            }
        };

        gridContainer.Controls.Add(_gridProducts);

        // WinForms Dock=Top kuralı: En son eklenen en üste çıkar:
        _pnlProducts.Controls.Add(gridContainer);
        _pnlProducts.Controls.Add(toolbar);
        _pnlProducts.Controls.Add(header);
    }

    private void GridProducts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridProducts.Rows.Count) return;
        if (_gridProducts.Rows[e.RowIndex].IsNewRow) return;

        if (_gridProducts.Columns.Contains("Kritik Seviye") && _gridProducts.Columns["Kalan Stok"] is { } colStock && e.ColumnIndex == colStock.Index)
        {
            var row = _gridProducts.Rows[e.RowIndex];
            double stock = Convert.ToDouble(row.Cells["Kalan Stok"].Value ?? 0);
            double min = Convert.ToDouble(row.Cells["Kritik Seviye"].Value ?? 0);
            if (stock <= min)
            {
                e.CellStyle.BackColor = CriticalBgColor;
                e.CellStyle.ForeColor = CriticalFgColor;
                e.CellStyle.Font = CriticalFont;
            }
        }

        if (_gridProducts.Columns["Fatura No"] is { } colPdf && e.ColumnIndex == colPdf.Index)
        {
            string? inv = e.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(inv))
            {
                e.Value = "📄 " + inv;
                e.CellStyle.ForeColor = Color.FromArgb(20, 176, 186);
                e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
            }
            else
            {
                e.Value = "-";
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            }
        }

        if (_gridProducts.Columns["SKT"] is { } colSktCol && e.ColumnIndex == colSktCol.Index)
        {
            string? sktStr = e.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(sktStr) && DateTime.TryParse(sktStr, out var sktDt))
            {
                int days = (int)(sktDt.Date - DateTime.Today).TotalDays;
                if (days < 0)
                {
                    e.Value = $"⛔ {sktDt:dd.MM.yy} ({Math.Abs(days)}g geçti)";
                    e.CellStyle.BackColor = Color.FromArgb(254, 226, 226);
                    e.CellStyle.ForeColor = Color.FromArgb(185, 28, 28);
                    e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
                }
                else if (days <= 30)
                {
                    e.Value = $"⚠️ {sktDt:dd.MM.yy} ({days}g kaldı)";
                    e.CellStyle.BackColor = Color.FromArgb(254, 243, 199);
                    e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9);
                    e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
                }
                else
                {
                    e.Value = $"✓ {sktDt:dd.MM.yyyy}";
                    e.CellStyle.ForeColor = Color.FromArgb(30, 41, 59);
                }
            }
            else
            {
                e.Value = "-";
                e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184);
            }
        }
    }
    #endregion

    #region 3. Accounts Page
    private void BuildAccountsPage()
    {
        _pnlAccounts = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Cari & Veresiye Takibi", "Müşteriler, tedarikçiler, açık hesap veresiye bakiyeleri, limit ve kara liste kontrolleri");
        _pnlAccounts.Controls.Add(header);

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(10, 12, 10, 12) };

        _txtAccountSearch.Width = 180;
        _txtAccountSearch.Height = 32;
        _txtAccountSearch.PlaceholderText = "🔍 Cari Ara (F2)...";
        _txtAccountSearch.TextChanged += (s, e) => RefreshAccounts();

        _cmbAccountType.Width = 100;
        _cmbAccountType.Height = 32;
        _cmbAccountType.Items.AddRange(new object[] { "Tümü", "Müşteri", "Tedarikçi" });
        _cmbAccountType.SelectedIndex = 0;
        _cmbAccountType.SelectedIndexChanged += (s, e) => RefreshAccounts();

        var btnAdd = UITheme.CreateButton("+ Yeni Cari", UITheme.Primary, Color.White, (s, e) => AddAccount(), 95, 34);
        var btnEdit = UITheme.CreateButton("✏️ Düzenle", UITheme.Secondary, Color.White, (s, e) => EditAccount(), 85, 34);
        var btnDebt = UITheme.CreateButton("🔴 Borç (F5)", Color.FromArgb(220, 38, 38), Color.White, (s, e) => AddAccountMovementForSelected("Satış"), 110, 34);
        var btnPayment = UITheme.CreateButton("🟢 Tahsilat (F6)", Color.FromArgb(16, 185, 129), Color.White, (s, e) => AddAccountMovementForSelected("Tahsilat"), 120, 34);
        var btnStatement = UITheme.CreateButton("📑 Ekstre (F10)", UITheme.Info, Color.White, (s, e) => ViewAccountStatement(), 115, 34);
        var btnWhatsApp = UITheme.CreateButton("📲 WhatsApp", Color.FromArgb(37, 211, 102), Color.White, (s, e) => SendWhatsAppForSelected(), 110, 34);
        var btnDue = UITheme.CreateButton("📅 Vade Takibi", Color.FromArgb(217, 119, 6), Color.White, (s, e) => { using var dlg = new DueReceivablesDialog(); dlg.ShowDialog(); RefreshAll(); }, 115, 34);
        var btnExcelImp = UITheme.CreateButton("📥 Excel'den Al", Color.FromArgb(14, 154, 167), Color.White, (s, e) => OpenExcelImport("Cari"), 120, 34);
        var btnExcelExp = UITheme.CreateButton("📊 Excel", UITheme.Primary, Color.White, ExportAccountsToExcel, 85, 34);
        var btnBulkDeleteAcc = UITheme.CreateButton("🗑️ Çoklu Sil", Color.FromArgb(220, 38, 38), Color.White, (s, e) => DeleteAccountsBulkAction(), 105, 34);
        var btnDelete = UITheme.CreateButton("🗑️ Sil", UITheme.BorderColor, UITheme.Danger, (s, e) => DeleteAccount(), 65, 34);
        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAccounts(), 40, 34);

        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true, WrapContents = false };
        filterFlow.Controls.Add(_txtAccountSearch);
        filterFlow.Controls.Add(_cmbAccountType);
        filterFlow.Controls.Add(btnAdd);
        filterFlow.Controls.Add(btnEdit);
        filterFlow.Controls.Add(btnDebt);
        filterFlow.Controls.Add(btnPayment);
        filterFlow.Controls.Add(btnStatement);
        filterFlow.Controls.Add(btnWhatsApp);
        filterFlow.Controls.Add(btnDue);
        filterFlow.Controls.Add(btnExcelImp);
        filterFlow.Controls.Add(btnExcelExp);
        filterFlow.Controls.Add(btnBulkDeleteAcc);
        filterFlow.Controls.Add(btnDelete);
        filterFlow.Controls.Add(btnRefresh);

        toolbar.Controls.Add(filterFlow);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
        UITheme.ApplyGridStyle(_gridAccounts);
        _gridAccounts.MultiSelect = true;
        _gridAccounts.CellDoubleClick += (s, e) => ViewAccountStatement();
        _gridAccounts.CellFormatting += GridAccounts_CellFormatting;

        var mnuAcc = new ContextMenuStrip();
        mnuAcc.Items.Add("📑 Hesap Ekstresi Aç (F10)", null, (s, e) => ViewAccountStatement());
        mnuAcc.Items.Add("📲 WhatsApp ile Bakiye / Borç Bildir", null, (s, e) => SendWhatsAppForSelected());
        mnuAcc.Items.Add(new ToolStripSeparator());
        mnuAcc.Items.Add("🔴 Cariye Borç Yaz (F5)", null, (s, e) => AddAccountMovementForSelected("Satış"));
        mnuAcc.Items.Add("🟢 Cariden Tahsilat Al (F6)", null, (s, e) => AddAccountMovementForSelected("Tahsilat"));
        mnuAcc.Items.Add(new ToolStripSeparator());
        mnuAcc.Items.Add("✏️ Cari Kartı Düzenle", null, (s, e) => EditAccount());
        mnuAcc.Items.Add(new ToolStripSeparator());
        mnuAcc.Items.Add("🗑️ Seçilen Carileri Toplu Sil (Çoklu)", null, (s, e) => DeleteAccountsBulkAction());
        _gridAccounts.ContextMenuStrip = mnuAcc;

        gridContainer.Controls.Add(_gridAccounts);

        _pnlAccounts.Controls.Add(gridContainer);
        _pnlAccounts.Controls.Add(toolbar);
        _pnlAccounts.Controls.Add(header);
    }
    #endregion

    #region 4. Stock Movements Page
    private void BuildStockMovementsPage()
    {
        _pnlStockMovements = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Stok Giriş & Çıkış Hareketleri", "Tarih bazlı ürün girişleri, satış çıkışları, iadeler ve zayiler");

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtStockSearch.Width = 180;
        _txtStockSearch.PlaceholderText = "🔍 Hareket Ara...";
        _txtStockSearch.TextChanged += (s, e) => RefreshStockMovements();

        _cmbStockType.Width = 120;
        _cmbStockType.Items.AddRange(new object[] { "Tümü", "Gelen", "Satılan", "İade Giriş", "Fire / Zayi", "Transfer Giriş", "Transfer Çıkış" });
        _cmbStockType.SelectedIndex = 0;
        _cmbStockType.SelectedIndexChanged += (s, e) => RefreshStockMovements();

        _cmbStockWarehouse.Width = 140;
        _cmbStockWarehouse.SelectedIndexChanged += (s, e) => RefreshStockMovements();

        _dtpStockStart.Width = 100;
        _dtpStockStart.ValueChanged += (s, e) => RefreshStockMovements();

        _dtpStockEnd.Width = 100;
        _dtpStockEnd.ValueChanged += (s, e) => RefreshStockMovements();

        var btnAdd = UITheme.CreateButton("+ Stok Hareketi", UITheme.Primary, Color.White, (s, e) => AddStock(), 130, 34);
        var btnTransfer = UITheme.CreateButton("🔄 Depo Transferi", Color.FromArgb(13, 148, 136), Color.White, (s, e) => OpenWarehouseTransfer(), 145, 34);
        var btnWarehouses = UITheme.CreateButton("🏢 Depolar / Şubeler", Color.FromArgb(79, 70, 229), Color.White, (s, e) => OpenWarehouseManage(), 155, 34);
        var btnStockCount = UITheme.CreateButton("📋 Stok Sayımı", Color.FromArgb(13, 148, 136), Color.White, (s, e) => OpenStockCount(), 130, 34);
        var btnBulkDelStock = UITheme.CreateButton("🗑️ Çoklu Sil", Color.FromArgb(220, 38, 38), Color.White, (s, e) => DeleteStockMovementsBulkAction(), 105, 34);
        var btnDelete = UITheme.CreateButton("🗑️ Sil", UITheme.Danger, Color.White, (s, e) => DeleteStock(), 80, 34);
        var btnExcelExp = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportStockToExcel, 130, 34);
        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshStockMovements(), 40, 34);

        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true, WrapContents = false };
        filterFlow.Controls.Add(_txtStockSearch);
        filterFlow.Controls.Add(_cmbStockType);
        filterFlow.Controls.Add(_cmbStockWarehouse);
        filterFlow.Controls.Add(_dtpStockStart);
        filterFlow.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 7, 0, 0) });
        filterFlow.Controls.Add(_dtpStockEnd);
        filterFlow.Controls.Add(btnAdd);
        filterFlow.Controls.Add(btnTransfer);
        filterFlow.Controls.Add(btnWarehouses);
        filterFlow.Controls.Add(btnStockCount);
        filterFlow.Controls.Add(btnBulkDelStock);
        filterFlow.Controls.Add(btnDelete);
        filterFlow.Controls.Add(btnExcelExp);
        filterFlow.Controls.Add(btnRefresh);

        toolbar.Controls.Add(filterFlow);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
        UITheme.ApplyGridStyle(_gridStockMov);
        _gridStockMov.MultiSelect = true;
        _gridStockMov.CellDoubleClick += (s, e) =>
        {
            if (_gridStockMov.CurrentRow != null && _gridStockMov.Columns.Contains("ProductId"))
            {
                long pid = Convert.ToInt64(_gridStockMov.CurrentRow.Cells["ProductId"].Value);
                OpenQuickSale("Satış", pid);
            }
        };

        var mnuStockMov = new ContextMenuStrip();
        var itemStockMovSale = new ToolStripMenuItem("⚡ Bu Ürünle Hızlı Satış Yap (Çift Tık)", null, (s, e) =>
        {
            if (_gridStockMov.CurrentRow != null && _gridStockMov.Columns.Contains("ProductId"))
            {
                long pid = Convert.ToInt64(_gridStockMov.CurrentRow.Cells["ProductId"].Value);
                OpenQuickSale("Satış", pid);
            }
        });
        itemStockMovSale.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        itemStockMovSale.ForeColor = UITheme.Primary;
        mnuStockMov.Items.Add(itemStockMovSale);

        var itemStockMovMeta = new ToolStripMenuItem("📄 Fatura Meta Bilgileri ve Faturadaki Diğer Ürünler", null, (s, e) =>
        {
            if (_gridStockMov.CurrentRow != null && _gridStockMov.Columns.Contains("ProductId"))
            {
                long pid = Convert.ToInt64(_gridStockMov.CurrentRow.Cells["ProductId"].Value);
                string pName = _gridStockMov.Columns.Contains("Ürün Adı") ? _gridStockMov.CurrentRow.Cells["Ürün Adı"].Value?.ToString() ?? "Ürün" : "Ürün";
                using var dlg = new InvoiceMetaDialog(pid, pName);
                dlg.ShowDialog(this);
            }
        });
        itemStockMovMeta.ForeColor = Color.FromArgb(13, 148, 136);
        mnuStockMov.Items.Add(itemStockMovMeta);
        _gridStockMov.ContextMenuStrip = mnuStockMov;

        gridContainer.Controls.Add(_gridStockMov);

        _pnlStockMovements.Controls.Add(gridContainer);
        _pnlStockMovements.Controls.Add(toolbar);
        _pnlStockMovements.Controls.Add(header);
    }
    #endregion

    #region 5. Account Movements Page
    private void BuildAccountMovementsPage()
    {
        _pnlAccountMovements = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Kasa & Finansal Yönetim", "Kasa/banka nakit akışı, fatura ödemeleri ve kısmi ödeme takibi");

        var tabsFinance = new TabControl { Dock = DockStyle.Fill, Font = UITheme.TitleFont };

        // ==================== SEKME 1: KASA & FİNANSAL HAREKETLER ====================
        var tabAccMov = new TabPage("💵 Kasa & Nakit Hareketleri") { BackColor = Color.FromArgb(248, 250, 252) };
        var toolbarAcc = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12) };

        _txtAccMovSearch.Width = 180;
        _txtAccMovSearch.PlaceholderText = "🔍 İşlem Ara...";
        _txtAccMovSearch.TextChanged += (s, e) => RefreshAccountMovements();

        _cmbAccMovType.Width = 120;
        _cmbAccMovType.Items.AddRange(new object[] { "Tümü", "Tahsilat", "Ödeme", "Satış", "Alış" });
        _cmbAccMovType.SelectedIndex = 0;
        _cmbAccMovType.SelectedIndexChanged += (s, e) => RefreshAccountMovements();

        _dtpAccStart.Width = 100;
        _dtpAccStart.ValueChanged += (s, e) => RefreshAccountMovements();

        _dtpAccEnd.Width = 100;
        _dtpAccEnd.ValueChanged += (s, e) => RefreshAccountMovements();

        var btnAdd = UITheme.CreateButton("+ Finansal İşlem", UITheme.Primary, Color.White, (s, e) => AddAccountMovement(), 140, 34);
        var btnDelete = UITheme.CreateButton("🗑️ Sil", UITheme.Danger, Color.White, (s, e) => DeleteAccountMovement(), 80, 34);
        var btnExcelExp = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportAccMovToExcel, 130, 34);
        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAccountMovements(), 40, 34);

        var filterFlowAcc = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        filterFlowAcc.Controls.Add(_txtAccMovSearch);
        filterFlowAcc.Controls.Add(_cmbAccMovType);
        filterFlowAcc.Controls.Add(_dtpAccStart);
        filterFlowAcc.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 7, 0, 0) });
        filterFlowAcc.Controls.Add(_dtpAccEnd);
        filterFlowAcc.Controls.Add(btnAdd);
        filterFlowAcc.Controls.Add(btnDelete);
        filterFlowAcc.Controls.Add(btnExcelExp);
        filterFlowAcc.Controls.Add(btnRefresh);

        toolbarAcc.Controls.Add(filterFlowAcc);

        var gridContainerAcc = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        UITheme.ApplyGridStyle(_gridAccMov);
        gridContainerAcc.Controls.Add(_gridAccMov);

        tabAccMov.Controls.Add(gridContainerAcc);
        tabAccMov.Controls.Add(toolbarAcc);

        // ==================== SEKME 2: FATURA & KISMİ ÖDEME TAKİBİ ====================
        var tabInvoices = new TabPage("🧾 Fatura & Kısmi Ödeme Takibi") { BackColor = Color.FromArgb(248, 250, 252) };
        var toolbarInv = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12) };

        _txtInvSearch.Width = 180;
        _txtInvSearch.PlaceholderText = "🔍 Fatura No / Cari...";
        _txtInvSearch.TextChanged += (s, e) => RefreshInvoices();

        _cmbInvType.Width = 140;
        _cmbInvType.Items.Clear();
        _cmbInvType.Items.AddRange(new object[] { "Tüm Faturalar", "Alış Faturası", "Satış Faturası" });
        _cmbInvType.SelectedIndex = 0;
        _cmbInvType.SelectedIndexChanged += (s, e) => RefreshInvoices();

        _cmbInvStatus.Width = 130;
        _cmbInvStatus.Items.Clear();
        _cmbInvStatus.Items.AddRange(new object[] { "Tüm Durumlar", "Ödenmedi", "Kısmi Ödendi", "Ödendi" });
        _cmbInvStatus.SelectedIndex = 0;
        _cmbInvStatus.SelectedIndexChanged += (s, e) => RefreshInvoices();

        _dtpInvStart.Width = 100;
        _dtpInvStart.ValueChanged += (s, e) => RefreshInvoices();

        _dtpInvEnd.Width = 100;
        _dtpInvEnd.ValueChanged += (s, e) => RefreshInvoices();

        var btnPayInv = UITheme.CreateButton("💳 Kısmi / Tam Ödeme Yap", UITheme.Success, Color.White, (s, e) => OpenInvoicePaymentDialogForSelected(), 185, 34);
        var btnViewInvMeta = UITheme.CreateButton("📄 Fatura Kalemleri", Color.FromArgb(13, 148, 136), Color.White, (s, e) => OpenInvoiceMetaForSelectedInvoice(), 145, 34);
        var btnExportInvExcel = UITheme.CreateButton("📊 Excel'e Aktar", Color.FromArgb(16, 185, 129), Color.White, ExportInvoicesToExcel, 130, 34);
        var btnRefreshInv = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshInvoices(), 40, 34);

        var filterFlowInv = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        filterFlowInv.Controls.Add(_txtInvSearch);
        filterFlowInv.Controls.Add(_cmbInvType);
        filterFlowInv.Controls.Add(_cmbInvStatus);
        filterFlowInv.Controls.Add(_dtpInvStart);
        filterFlowInv.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 7, 0, 0) });
        filterFlowInv.Controls.Add(_dtpInvEnd);
        filterFlowInv.Controls.Add(btnPayInv);
        filterFlowInv.Controls.Add(btnViewInvMeta);
        filterFlowInv.Controls.Add(btnExportInvExcel);
        filterFlowInv.Controls.Add(btnRefreshInv);

        toolbarInv.Controls.Add(filterFlowInv);

        // Alt Toplamlar Paneli (Kullanıcının İstediği Dinamik Fatura Toplamı Göstergesi)
        var pnlInvSummary = new CardPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(15, 8, 15, 8) };
        var flowInvSummary = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true };

        _lblInvSummaryCount.Margin = new Padding(0, 5, 25, 0);
        _lblInvSummaryTotal.Margin = new Padding(0, 5, 25, 0);
        _lblInvSummaryPaid.Margin = new Padding(0, 5, 25, 0);
        _lblInvSummaryRemaining.Margin = new Padding(0, 5, 0, 0);

        flowInvSummary.Controls.Add(_lblInvSummaryCount);
        flowInvSummary.Controls.Add(_lblInvSummaryTotal);
        flowInvSummary.Controls.Add(_lblInvSummaryPaid);
        flowInvSummary.Controls.Add(_lblInvSummaryRemaining);
        pnlInvSummary.Controls.Add(flowInvSummary);

        // Fatura Tablosu
        var gridContainerInv = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 10) };
        UITheme.ApplyGridStyle(_gridInvoices);
        _gridInvoices.CellDoubleClick += (s, e) => OpenInvoicePaymentDialogForSelected();

        var mnuInvoices = new ContextMenuStrip();
        var itemPay = new ToolStripMenuItem("💳 Faturaya Kısmi / Tam Ödeme Yap", null, (s, e) => OpenInvoicePaymentDialogForSelected())
        {
            Font = new Font(UITheme.RegularFont, FontStyle.Bold),
            ForeColor = UITheme.Success
        };
        var itemMeta = new ToolStripMenuItem("📄 Fatura Meta Bilgileri ve Ürün Kalemleri", null, (s, e) => OpenInvoiceMetaForSelectedInvoice())
        {
            ForeColor = Color.FromArgb(13, 148, 136)
        };
        mnuInvoices.Items.Add(itemPay);
        mnuInvoices.Items.Add(itemMeta);
        _gridInvoices.ContextMenuStrip = mnuInvoices;

        gridContainerInv.Controls.Add(_gridInvoices);

        tabInvoices.Controls.Add(gridContainerInv);
        tabInvoices.Controls.Add(pnlInvSummary);
        tabInvoices.Controls.Add(toolbarInv);

        _tabsFinance.TabPages.Clear();
        _tabsFinance.TabPages.Add(tabAccMov);
        _tabsFinance.TabPages.Add(tabInvoices);

        _pnlAccountMovements.Controls.Add(_tabsFinance);
        _pnlAccountMovements.Controls.Add(header);
    }
    #endregion

    #region 6. Settings Page
    private void BuildSettingsPage()
    {
        _pnlSettings = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var header = CreatePageHeader("Ayarlar, Veritabanı & Bulut Entegrasyonu", "Veritabanı yedekleme, Google Drive/Gmail bulut yedek, menü düzenleme ve sistem yönetimi");

        var flow = new FlowLayoutPanel 
        { 
            Dock = DockStyle.Fill, 
            FlowDirection = FlowDirection.TopDown, 
            AutoScroll = true, 
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 10) 
        };

        // 1. Veritabanı Yönetim Kartı
        var dbCard = new CardPanel { Width = 840, Height = 220, Margin = new Padding(0, 0, 0, 20) };
        var lblDbTitle = new Label { Text = "🗄️ SQL Server Veritabanı Yapılandırması & Yerel Yedekleme", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var lblDbDesc = new Label
        {
            Text = $"Aktif SQL Sunucusu: {Database.CurrentServer}\nVeritabanı Adı: {Database.CurrentDatabase}\nKimlik Doğrulama: {(Database.Config.IntegratedSecurity ? "Windows Kimlik Doğrulaması (Trusted)" : "SQL Server Kullanıcı Doğrulaması")}\n\nVeritabanı bağlantı parametrelerini test edebilir, SQL yedeği alabilir veya geri yükleyebilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 85
        };

        var dbButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnConfigSql = UITheme.CreateButton("⚙️ SQL Sunucu Ayarları", UITheme.Primary, Color.White, (s, e) =>
        {
            using var dlg = new DatabaseConfigDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RefreshAll();
            }
        }, 180, 36);
        var btnBackup = UITheme.CreateButton("💾 SQL Yedeği Al (.bak)", UITheme.Secondary, Color.White, BackupDatabaseClick, 180, 36);
        var btnRestore = UITheme.CreateButton("📂 Yedekten Geri Yükle", UITheme.Warning, Color.White, RestoreDatabaseClick, 180, 36);

        dbButtons.Controls.Add(btnConfigSql);
        dbButtons.Controls.Add(btnBackup);
        dbButtons.Controls.Add(btnRestore);

        dbCard.Controls.Add(dbButtons);
        dbCard.Controls.Add(lblDbDesc);
        dbCard.Controls.Add(lblDbTitle);
        flow.Controls.Add(dbCard);

        // 2. Google Drive & Gmail Bulut Yedekleme & Geri Yükleme Kartı
        var cloudCard = new CardPanel { Width = 840, Height = 210, Margin = new Padding(0, 0, 0, 20) };
        var lblCloudTitle = new Label { Text = "☁️ Google Drive & Gmail Bulut Yedekleme & Geri Yükleme", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(2, 132, 199), Dock = DockStyle.Top, Height = 30 };
        var lblCloudDesc = new Label
        {
            Text = "Gmail kullanıcı adı & şifrenizle otomatik bağlantı sağlayın. SQL veritabanı yedekleriniz şifreli .zip formatında Google Drive veya yerel klasörünüze kaydedilir, dilerseniz Gmail adresinize e-posta ekiyle sessizce iletilir. Mevcut yedekleri görüntüleyip dilediğiniz yedeği tek tıkla geri yükleyebilirsiniz (Restore). Program her kapandığında veya günlük otomatik yedek alma seçeneklerini buradan yönetin.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 80
        };

        var cloudButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenCloudManage = UITheme.CreateButton("☁️ Bulut Yedekleme & Geri Yükleme Yönetimi", Color.FromArgb(2, 132, 199), Color.White, (s, e) =>
        {
            using var dlg = new CloudBackupManageDialog();
            dlg.ShowDialog(this);
        }, 290, 36);
        var btnTriggerCloud = UITheme.CreateButton("⚡ Şimdi Buluta Yedek Al", UITheme.Success, Color.White, (s, e) =>
        {
            CloudBackupService.ExecuteBackup(silent: false);
        }, 190, 36);

        cloudButtons.Controls.Add(btnOpenCloudManage);
        cloudButtons.Controls.Add(btnTriggerCloud);

        cloudCard.Controls.Add(cloudButtons);
        cloudCard.Controls.Add(lblCloudDesc);
        cloudCard.Controls.Add(lblCloudTitle);
        flow.Controls.Add(cloudCard);

        // 3. Ribbon Menü Puzzle Düzenleyicisi & Kilitleme Kartı
        var puzzleCard = new CardPanel { Width = 840, Height = 190, Margin = new Padding(0, 0, 0, 20) };
        var lblPuzzleTitle = new Label { Text = "🧩 Menü Düzenleyici (Puzzle Modu & Kilitleme)", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(234, 88, 12), Dock = DockStyle.Top, Height = 30 };
        var lblPuzzleDesc = new Label
        {
            Text = "İşletmenizde kullanmadığınız menü sekmelerini (Ürünler, Cari, Stok, Kasa vb.) Ribbon çubuğundan kaldırabilir veya tekrar ekleyebilirsiniz. Düzenlemeniz bittiğinde 'Menü Düzenini Kilitle' seçeneğiyle ekranı dondurabilir, personelin veya diğer kullanıcıların yanlışlıkla menüleri kaldırmasını önleyebilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 65
        };

        var puzzleButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenPuzzle = UITheme.CreateButton("🧩 Menü Sekmelerini Düzenle & Kilitle", Color.FromArgb(234, 88, 12), Color.White, (s, e) =>
        {
            using var dlg = new RibbonCustomizerDialog(_ribbon, () => { });
            dlg.ShowDialog(this);
        }, 280, 36);
        puzzleButtons.Controls.Add(btnOpenPuzzle);

        puzzleCard.Controls.Add(puzzleButtons);
        puzzleCard.Controls.Add(lblPuzzleDesc);
        puzzleCard.Controls.Add(lblPuzzleTitle);
        flow.Controls.Add(puzzleCard);

        // 4. Excel İçe Aktarım Kartı
        var excelCard = new CardPanel { Width = 840, Height = 190, Margin = new Padding(0, 0, 0, 20) };
        var lblExTitle = new Label { Text = "📥 Excel'den Toplu Veri Yükleme", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var lblExDesc = new Label
        {
            Text = "Excel şablonunda (Ürünler, Stok Hareketi, Cari Listesi, Cari Hareket) yer alan tüm kayıtları tek seferde veritabanına aktarabilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 60
        };

        var exButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnImportExcel = UITheme.CreateButton("📥 Excel Dosyasından Yükle", UITheme.Success, Color.White, ImportExcelClick, 220, 36);
        exButtons.Controls.Add(btnImportExcel);

        excelCard.Controls.Add(exButtons);
        excelCard.Controls.Add(lblExDesc);
        excelCard.Controls.Add(lblExTitle);
        flow.Controls.Add(excelCard);

        // 5. Denetim & İşlem Logları Kartı
        var logCard = new CardPanel { Width = 840, Height = 170, Margin = new Padding(0, 0, 0, 20) };
        var lblLogTitle = new Label { Text = "📜 Sistem Değişiklik ve Silme Denetim Kayıtları (Audit Trail)", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var lblLogDesc = new Label
        {
            Text = "Silinen tüm cari kartlar, değiştirilen cari bilgileri, silinen satış/tahsilat/ödeme hareketleri otomatik olarak loglanır. Dilediğiniz zaman logları inceleyebilir ve Excel'e aktarabilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 50
        };

        var logButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenLogs = UITheme.CreateButton("📜 Log Kayıtlarını Görüntüle", UITheme.Primary, Color.White, (s, e) => ShowPage(5), 230, 36);
        logButtons.Controls.Add(btnOpenLogs);

        logCard.Controls.Add(logButtons);
        logCard.Controls.Add(lblLogDesc);
        logCard.Controls.Add(lblLogTitle);
        flow.Controls.Add(logCard);

        // 6. Telegram Botu & Cep Takip Kartı
        var tgCard = new CardPanel { Width = 840, Height = 170, Margin = new Padding(0, 0, 0, 20) };
        var lblTgTitle = new Label { Text = "🤖 Telegram Asistanı & Cep Telefonundan Canlı Takip", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(20, 176, 186), Dock = DockStyle.Top, Height = 30 };
        var lblTgDesc = new Label
        {
            Text = "Dükkanınızdaki güncel kasa durumunu, kritik stokları ve borçluları cep telefonunuzdan Telegram üzerinden anlık takip edin. Ayrıca telefon kameranızla ürünün barkod fotoğrafını göndererek anında stok ve fiyat sorgulayın.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 50
        };

        var tgButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenTg = UITheme.CreateButton("🤖 Telegram Ayarları & Botu Başlat", Color.FromArgb(20, 176, 186), Color.White, (s, e) => OpenTelegramConfig(), 280, 36);
        tgButtons.Controls.Add(btnOpenTg);

        tgCard.Controls.Add(tgButtons);
        tgCard.Controls.Add(lblTgDesc);
        tgCard.Controls.Add(lblTgTitle);
        flow.Controls.Add(tgCard);

        // 7. Görsel Tema & Palet Yöneticisi Kartı
        var themeCard = new CardPanel { Width = 840, Height = 175, Margin = new Padding(0, 0, 0, 20) };
        var lblThemeTitle = new Label { Text = "🎨 Görsel Tema & Gece Modu (Dark Mode) Yönetimi", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(124, 58, 237), Dock = DockStyle.Top, Height = 30 };
        var lblThemeDesc = new Label
        {
            Text = "Krypton Toolkit motoru ile uygulamanın renk temasını anında değiştirebilirsiniz. Gece çalışma modu (Dark Mode), Microsoft 365, Office kurumsal temaları veya canlı Sparkle temalarından dilediğinizi seçin. Seçiminiz anında uygulanır ve otomatik olarak kaydedilir.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 55
        };

        var themeButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.LeftToRight };
        var cmbThemePage = new KryptonComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Height = 34 };
        foreach (var t in AppThemeService.AvailableThemes) cmbThemePage.Items.Add(t);
        cmbThemePage.SelectedItem = AppThemeService.CurrentTheme;
        cmbThemePage.SelectedIndexChanged += (s, e) =>
        {
            if (cmbThemePage.SelectedItem is AppThemeOption opt)
            {
                AppThemeService.ApplyTheme(opt);
            }
        };

        var btnDarkMode = UITheme.CreateKryptonButton("🌙 Gece Modu", Color.FromArgb(30, 41, 59), Color.White, (s, e) =>
        {
            var darkTheme = AppThemeService.AvailableThemes.FirstOrDefault(t => t.IsDark);
            if (darkTheme != null)
            {
                cmbThemePage.SelectedItem = darkTheme;
                AppThemeService.ApplyTheme(darkTheme);
            }
        }, 130, 34);

        var btnLightMode = UITheme.CreateKryptonButton("☀️ Gündüz Modu", Color.FromArgb(2, 132, 199), Color.White, (s, e) =>
        {
            var lightTheme = AppThemeService.AvailableThemes[0];
            cmbThemePage.SelectedItem = lightTheme;
            AppThemeService.ApplyTheme(lightTheme);
        }, 130, 34);

        themeButtons.Controls.Add(cmbThemePage);
        themeButtons.Controls.Add(btnDarkMode);
        themeButtons.Controls.Add(btnLightMode);

        themeCard.Controls.Add(themeButtons);
        themeCard.Controls.Add(lblThemeDesc);
        themeCard.Controls.Add(lblThemeTitle);
        flow.Controls.Add(themeCard);

        _pnlSettings.Controls.Add(flow);
        _pnlSettings.Controls.Add(header);
        header.SendToBack();
    }
    #endregion

    #region 7. Audit Logs Page
    private void BuildAuditLogsPage()
    {
        _pnlAuditLogs = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("İşlem & Güvenlik Logları (Audit Trail)", "Silinen veya değiştirilen tüm cariler, hesap hareketleri ve ürün kayıtları");

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtAuditSearch.Width = 180;
        _txtAuditSearch.PlaceholderText = "🔍 Loglarda Ara...";
        _txtAuditSearch.TextChanged += (s, e) => RefreshAuditLogs();

        _cmbAuditEntity.Width = 120;
        _cmbAuditEntity.Items.AddRange(new object[] { "Tümü", "Kullanıcı", "Cari", "CariHareket", "Urun", "StokHareket", "Sistem" });
        _cmbAuditEntity.SelectedIndex = 0;
        _cmbAuditEntity.SelectedIndexChanged += (s, e) => RefreshAuditLogs();

        _cmbAuditAction.Width = 140;
        _cmbAuditAction.Items.AddRange(new object[] { "Tümü", "Giriş", "Güncelleme", "Silindi", "Silindi / Pasife Alındı", "Güncellendi", "Yeni Eklendi" });
        _cmbAuditAction.SelectedIndex = 0;
        _cmbAuditAction.SelectedIndexChanged += (s, e) => RefreshAuditLogs();

        _dtpAuditStart.Width = 100;
        _dtpAuditStart.ValueChanged += (s, e) => RefreshAuditLogs();

        _dtpAuditEnd.Width = 100;
        _dtpAuditEnd.ValueChanged += (s, e) => RefreshAuditLogs();

        var btnExportExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportAuditLogsToExcel, 130, 34);
        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAuditLogs(), 40, 34);

        var filterFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        filterFlow.Controls.Add(_txtAuditSearch);
        filterFlow.Controls.Add(_cmbAuditEntity);
        filterFlow.Controls.Add(_cmbAuditAction);
        filterFlow.Controls.Add(_dtpAuditStart);
        filterFlow.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 7, 0, 0) });
        filterFlow.Controls.Add(_dtpAuditEnd);
        filterFlow.Controls.Add(btnExportExcel);
        filterFlow.Controls.Add(btnRefresh);

        toolbar.Controls.Add(filterFlow);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
        UITheme.ApplyGridStyle(_gridAuditLogs);
        _gridAuditLogs.CellFormatting += GridAuditLogs_CellFormatting;
        gridContainer.Controls.Add(_gridAuditLogs);

        _pnlAuditLogs.Controls.Add(gridContainer);
        _pnlAuditLogs.Controls.Add(toolbar);
        _pnlAuditLogs.Controls.Add(header);
    }

    private void GridAuditLogs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridAuditLogs.Rows.Count) return;
        var row = _gridAuditLogs.Rows[e.RowIndex];
        string action = row.Cells["İşlem"]?.Value?.ToString() ?? "";

        if (action.Contains("Silin"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(254, 226, 226);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(185, 28, 28);
            row.Cells["İşlem"].Style.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        }
        else if (action.Contains("Güncellen"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(254, 243, 199);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(180, 83, 9);
            row.Cells["İşlem"].Style.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
        }
        else if (action.Contains("Yeni"))
        {
            row.Cells["İşlem"].Style.BackColor = Color.FromArgb(209, 250, 229);
            row.Cells["İşlem"].Style.ForeColor = Color.FromArgb(4, 120, 87);
        }
    }
    #endregion

    #region 8. Users Page
    private void BuildUsersPage()
    {
        _pnlUsers = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Kullanıcı & Yetkilendirme Yönetimi", "Sistem kullanıcılarını tanımlayın, rollerini belirleyin ve modül yetkilerini yönetin");

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 65, Padding = new Padding(12, 12, 12, 12) };

        _txtUserSearch.Width = 220;
        _txtUserSearch.PlaceholderText = "🔍 Kullanıcı Ara...";
        _txtUserSearch.TextChanged += (s, e) => RefreshUsers();

        var btnAddUser = UITheme.CreateButton("+ Yeni Kullanıcı Ekle", UITheme.Primary, Color.White, (s, e) =>
        {
            using var dlg = new UserEditDialog(0);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                RefreshUsers();
        }, 160, 34);

        var btnEditUser = UITheme.CreateButton("✏️ Düzenle", UITheme.Secondary, Color.White, (s, e) =>
        {
            if (_gridUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen düzenlemek istediğiniz kullanıcıyı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            long id = Convert.ToInt64(_gridUsers.SelectedRows[0].Cells["Id"].Value);
            using var dlg = new UserEditDialog(id);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                RefreshUsers();
        }, 110, 34);

        var btnDeleteUser = UITheme.CreateButton("🗑️ Sil", UITheme.Danger, Color.White, (s, e) =>
        {
            if (_gridUsers.SelectedRows.Count == 0)
            {
                MessageBox.Show("Lütfen silmek istediğiniz kullanıcıyı seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            long id = Convert.ToInt64(_gridUsers.SelectedRows[0].Cells["Id"].Value);
            string uname = _gridUsers.SelectedRows[0].Cells["Kullanıcı Adı"].Value?.ToString() ?? "";

            if (MessageBox.Show($"'{uname}' kullanıcısını silmek istediğinize emin misiniz?", "Kullanıcı Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var res = UserService.DeleteUser(id);
                if (res.Success)
                {
                    MessageBox.Show(res.Message, "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshUsers();
                }
                else
                {
                    MessageBox.Show(res.Message, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }, 90, 34);

        var btnRefreshUsers = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshUsers(), 40, 34);

        var toolFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        toolFlow.Controls.Add(_txtUserSearch);
        toolFlow.Controls.Add(btnAddUser);
        toolFlow.Controls.Add(btnEditUser);
        toolFlow.Controls.Add(btnDeleteUser);
        toolFlow.Controls.Add(btnRefreshUsers);
        toolbar.Controls.Add(toolFlow);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 15, 0, 0) };
        UITheme.ApplyGridStyle(_gridUsers);
        _gridUsers.DoubleClick += (s, e) =>
        {
            if (_gridUsers.SelectedRows.Count == 0) return;
            long id = Convert.ToInt64(_gridUsers.SelectedRows[0].Cells["Id"].Value);
            using var dlg = new UserEditDialog(id);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                RefreshUsers();
        };

        gridContainer.Controls.Add(_gridUsers);

        _pnlUsers.Controls.Add(gridContainer);
        _pnlUsers.Controls.Add(toolbar);
        _pnlUsers.Controls.Add(header);
    }

    private void RefreshUsers()
    {
        try
        {
            var users = UserService.GetAllUsers();
            string q = _txtUserSearch.Text.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(q))
            {
                users = users.Where(u => u.Username.ToLowerInvariant().Contains(q) || u.FullName.ToLowerInvariant().Contains(q) || u.Role.ToLowerInvariant().Contains(q)).ToList();
            }

            var dt = new DataTable();
            dt.Columns.Add("Id", typeof(long));
            dt.Columns.Add("Kullanıcı Adı");
            dt.Columns.Add("Adı Soyadı");
            dt.Columns.Add("Rol");
            dt.Columns.Add("Yetkiler");
            dt.Columns.Add("Durum");
            dt.Columns.Add("Kayıt Tarihi");

            foreach (var u in users)
            {
                string permDesc = u.Role == "Admin" || u.Permissions == "ALL" ? "⭐ Tam Yetkili (ALL)" : u.Permissions;
                dt.Rows.Add(
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Role,
                    permDesc,
                    u.IsActive ? "Aktif" : "Pasif",
                    u.CreatedAt.ToString("dd.MM.yyyy HH:mm")
                );
            }

            _gridUsers.DataSource = dt;
            if (_gridUsers.Columns["Id"] is { } colUserId)
                colUserId.Visible = false;

            foreach (DataGridViewRow row in _gridUsers.Rows)
            {
                string durum = row.Cells["Durum"].Value?.ToString() ?? "";
                if (durum == "Aktif")
                {
                    row.Cells["Durum"].Style.ForeColor = UITheme.Success;
                    row.Cells["Durum"].Style.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
                }
                else
                {
                    row.Cells["Durum"].Style.ForeColor = UITheme.Danger;
                }
            }
        }
        catch { }
    }
    #endregion

    #region 9. License & Keygen Page
    private Label _lblLicStatusVal = new();
    private Label _lblLicDaysVal = new();
    private Label _lblLicOwnerVal = new();
    private TextBox _txtLicMachineVal = new();

    private void BuildLicensePage()
    {
        _pnlLicense = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var header = CreatePageHeader("Lisans Durumu & Bilgileri", "1 Yıllık çevrimdışı lisans takibi, kalan süre ve sistem aktivasyon yönetimi");

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 10)
        };

        // KART 1: Aktif Lisans Durumu
        var cardStatus = new CardPanel { Width = 900, Height = 250, Padding = new Padding(20) };
        var lblStatusTitle = new Label { Text = "🛡️ Aktif Sistem Lisansı Bilgileri", Font = UITheme.SubHeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        
        var licInfoTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 130,
            ColumnCount = 2,
            RowCount = 4,
            ColumnStyles = { new ColumnStyle(SizeType.Absolute, 220), new ColumnStyle(SizeType.Percent, 100) }
        };

        licInfoTable.Controls.Add(new Label { Text = "Makine Donanım Kodu (ID):", Font = UITheme.TitleFont, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        var mcPanel = new Panel { Dock = DockStyle.Fill };
        _txtLicMachineVal.ReadOnly = true;
        _txtLicMachineVal.Width = 280;
        _txtLicMachineVal.Font = new Font("Consolas", 10f, FontStyle.Bold);
        _txtLicMachineVal.Text = LicenseService.GetMachineCode();
        var btnCopyMc = UITheme.CreateButton("📋 Kopyala", UITheme.Info, Color.White, (s, e) =>
        {
            Clipboard.SetText(_txtLicMachineVal.Text);
            MessageBox.Show("Makine donanım kodunuz kopyalandı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, 90, 26);
        btnCopyMc.Left = 290;
        mcPanel.Controls.Add(_txtLicMachineVal);
        mcPanel.Controls.Add(btnCopyMc);
        licInfoTable.Controls.Add(mcPanel, 1, 0);

        licInfoTable.Controls.Add(new Label { Text = "Lisans Durumu:", Font = UITheme.TitleFont, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _lblLicStatusVal.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        licInfoTable.Controls.Add(_lblLicStatusVal, 1, 1);

        licInfoTable.Controls.Add(new Label { Text = "Kalan Süre / Bitiş:", Font = UITheme.TitleFont, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _lblLicDaysVal.Font = UITheme.RegularFont;
        licInfoTable.Controls.Add(_lblLicDaysVal, 1, 2);

        licInfoTable.Controls.Add(new Label { Text = "Lisans Sahibi:", Font = UITheme.TitleFont, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        _lblLicOwnerVal.Font = UITheme.RegularFont;
        licInfoTable.Controls.Add(_lblLicOwnerVal, 1, 3);

        var actionFlow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };

        var btnMailBox = UITheme.CreateButton("🚀 Lisans Talebini Doğrudan İlet (Mail Box)", UITheme.Primary, Color.White, (s, e) =>
        {
            using var mb = new LicenseMailBoxDialog();
            mb.ShowDialog(this);
        }, 320, 36);

        var btnActivateNew = UITheme.CreateButton("🔑 Yeni Lisans Aktifleştir", UITheme.Success, Color.White, (s, e) =>
        {
            using var act = new LicenseActivationForm();
            if (act.ShowDialog(this) == DialogResult.OK)
            {
                RefreshLicensePage();
                RefreshDashboard();
            }
        }, 200, 36);

        var btnDeactivate = UITheme.CreateButton("🚫 Lisansı Pasife Al", UITheme.Danger, Color.White, (s, e) =>
        {
            var ask = MessageBox.Show(
                "Mevcut aktif sistem lisansını pasife almak (devre dışı bırakmak) istediğinize emin misiniz?\n\n" +
                "Sistem lisanssız duruma geçecek ve yeni geçerli bir lisans girilene kadar kullanılamayacaktır.",
                "Lisansı Pasife Alma Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (ask == DialogResult.Yes)
            {
                var res = LicenseService.DeactivateLicense("Kullanıcı ana ekran lisans sayfasından pasife aldı.");
                if (res.Success)
                {
                    MessageBox.Show(res.Message, "Lisans Pasife Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Ana formu kapatıp çık, Program.cs otomatik olarak LicenseActivationForm'u açar!
                    IsLoggedOut = false;
                    Close();
                }
                else
                {
                    MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }, 160, 36);

        actionFlow.Controls.Add(btnMailBox);
        actionFlow.Controls.Add(btnActivateNew);
        actionFlow.Controls.Add(btnDeactivate);

        cardStatus.Controls.Add(actionFlow);
        cardStatus.Controls.Add(licInfoTable);
        cardStatus.Controls.Add(lblStatusTitle);
        flow.Controls.Add(cardStatus);

        // KART 2: SADECE Süper Kullanıcı (super) için Lisans Üretim Merkezi Kartı (Admin göremez!)
        if (UserService.CurrentUser?.IsSuperUser == true)
        {
            var cardGen = new CardPanel { Width = 900, Height = 145, Padding = new Padding(20), Margin = new Padding(0, 15, 0, 0) };
            var lblGenTitle = new Label 
            { 
                Text = "👑 SÜPER KULLANICI LİSANS ÜRETİM MERKEZİ (KEYGEN)", 
                Font = UITheme.SubHeaderFont, 
                ForeColor = Color.FromArgb(124, 58, 237), 
                Dock = DockStyle.Top, 
                Height = 30 
            };
            var lblGenDesc = new Label 
            { 
                Text = "Bu kontrol paneli yalnızca Süper Kullanıcı (super / 367244) oturumunda görüntülenir. Normal yönetici veya personel ekranlarında gizlidir. Buradan müşterilerden gelen makine donanım kodlarına göre 1 yıllık benzersiz çevrimdışı aktivasyon anahtarları üretebilirsiniz.", 
                Font = UITheme.RegularFont, 
                ForeColor = UITheme.TextSecondary, 
                Dock = DockStyle.Top, 
                Height = 36 
            };

            var btnOpenGen = UITheme.CreateButton("🔑 Lisans Anahtarı Üreticisini Aç (Keygen)", Color.FromArgb(124, 58, 237), Color.White, (s, e) =>
            {
                using var gen = new LicenseGeneratorForm();
                gen.ShowDialog(this);
                RefreshLicensePage();
            }, 320, 36);
            btnOpenGen.Dock = DockStyle.Bottom;

            cardGen.Controls.Add(btnOpenGen);
            cardGen.Controls.Add(lblGenDesc);
            cardGen.Controls.Add(lblGenTitle);
            flow.Controls.Add(cardGen);
        }

        _pnlLicense.Controls.Add(flow);
        _pnlLicense.Controls.Add(header);

        RefreshLicensePage();
    }

    private void OpenSuperUserKeygen()
    {
        using var auth = new SuperUserAuthDialog();
        if (auth.ShowDialog(this) == DialogResult.OK)
        {
            using var gen = new LicenseGeneratorForm();
            gen.ShowDialog(this);
            RefreshLicensePage();
        }
    }

    private void RefreshLicensePage()
    {
        var lic = LicenseService.CheckLicenseStatus();
        if (lic.IsValid)
        {
            _lblLicStatusVal.Text = "● LİSANS AKTİF & ÇEVRİMİÇİ DOĞRULANDI";
            _lblLicStatusVal.ForeColor = UITheme.Success;
            _lblLicDaysVal.Text = $"{lic.DaysRemaining} Gün Kaldı (Son Geçerlilik: {lic.ExpireDate:dd.MM.yyyy})";
            _lblLicOwnerVal.Text = lic.LicensedTo;
        }
        else
        {
            _lblLicStatusVal.Text = "● LİSANS BULUNAMADI VEYA SÜRESİ DOLMUŞ";
            _lblLicStatusVal.ForeColor = UITheme.Danger;
            _lblLicDaysVal.Text = "0 Gün (Pasif)";
            _lblLicOwnerVal.Text = "-";
        }
    }
    #endregion

    private Panel CreatePageHeader(string title, string subtitle)
    {
        var p = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(0, 0, 0, 15) };
        var lblT = new Label { Text = title, Font = UITheme.HeaderFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var lblS = new Label { Text = subtitle, Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 20 };
        p.Controls.Add(lblS);
        p.Controls.Add(lblT);
        return p;
    }

    #region Veri Yenileme (Refresh)
    public void RefreshAll()
    {
        SyncProductCategories();
        RefreshDashboard();
        RefreshProducts();
        RefreshAccounts();
        RefreshStockMovements();
        RefreshAccountMovements();
        RefreshInvoices();
        RefreshAuditLogs();
        RefreshUsers();
        RefreshLicensePage();
    }

    private void RefreshDashboard()
    {
        try
        {
            var s = DashboardService.GetSummary();
            _cardStockVal.ValueText = $"{s.TotalStockValue:N2} ₺";
            _cardStockVal.SubText = $"{s.TotalStockQuantity:N2} Adet / Birim Stok";

            _cardReceivable.ValueText = $"{s.TotalCustomerReceivable:N2} ₺";
            _cardReceivable.SubText = $"{s.ActiveCustomerCount} Kayıtlı Müşteri";

            _cardPayable.ValueText = $"{s.TotalSupplierPayable:N2} ₺";
            _cardPayable.SubText = $"{s.ActiveSupplierCount} Kayıtlı Tedarikçi";

            _cardTodayCash.ValueText = $"{s.NetCashToday:N2} ₺";
            _cardTodayCash.SubText = s.NetCashToday >= 0 ? "Bugün Pozitif Kasa Girişi" : "Bugün Net Nakit Çıkışı";

            _cardOverdue.ValueText = $"{s.OverdueReceivableTotal:N2} ₺";
            _cardOverdue.SubText = s.OverdueReceivableCount > 0 ? $"⚠️ {s.OverdueReceivableCount} Kişi Gecikmede (Tıkla)" : "Tüm vadeler güncel";

            _gridCriticalStock.DataSource = DashboardService.GetCriticalStockProducts();
            _gridTopDebtors.DataSource = DashboardService.GetTopDebtorCustomers();
            _gridRecentTransactions.DataSource = DashboardService.GetRecentTransactions();

            if (_gridTopDebtors.Columns["Borç Bakiyesi (₺)"] is { } colDebtors)
                colDebtors.DefaultCellStyle.Format = "N2";

            if (_gridRecentTransactions.Columns["Tutar (₺)"] is { } colRecent)
                colRecent.DefaultCellStyle.Format = "N2";

            UpdateNotificationBadge();
            LayoutDashboard();
        }
        catch { }
    }

    private void ProductCategoryChanged(object? sender, EventArgs e)
    {
        RefreshProducts();
    }

    private void SyncProductCategories()
    {
        try
        {
            _cmbProductCategory.SelectedIndexChanged -= ProductCategoryChanged;
            string selectedCat = _cmbProductCategory.SelectedItem?.ToString() ?? "Tümü";
            var cats = ProductService.GetCategories();

            _cmbProductCategory.Items.Clear();
            _cmbProductCategory.Items.AddRange(cats.ToArray());
            _cmbProductCategory.SelectedItem = cats.Contains(selectedCat) ? selectedCat : "Tümü";
            _cmbProductCategory.SelectedIndexChanged += ProductCategoryChanged;
        }
        catch { }
    }

    private void SyncWarehouses()
    {
        try
        {
            var warehouses = WarehouseService.GetActiveWarehouses();
            
            // Ürünler sayfası depo filtresi
            var listProd = new List<dynamic> { new { Id = 0L, Name = "🏢 Tüm Depolar" } };
            foreach (var w in warehouses) listProd.Add(new { Id = w.Id, Name = $"🏢 {w.Name}" });
            _cmbProductWarehouse.DataSource = listProd;
            _cmbProductWarehouse.DisplayMember = "Name";
            _cmbProductWarehouse.ValueMember = "Id";

            // Stok hareketleri sayfası depo filtresi
            var listStock = new List<dynamic> { new { Id = 0L, Name = "🏢 Tüm Depolar" } };
            foreach (var w in warehouses) listStock.Add(new { Id = w.Id, Name = $"🏢 {w.Name}" });
            _cmbStockWarehouse.DataSource = listStock;
            _cmbStockWarehouse.DisplayMember = "Name";
            _cmbStockWarehouse.ValueMember = "Id";
        }
        catch { }
    }

    private void RefreshProducts()
    {
        if (_isRefreshing) return;
        try
        {
            _isRefreshing = true;

            string search = _txtProductSearch.Text?.Trim() ?? "";
            string? cat = _cmbProductCategory.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(cat) || cat.Equals("Tümü", StringComparison.OrdinalIgnoreCase))
            {
                cat = null;
            }

            long? selectedWarehouseId = null;
            if (_cmbProductWarehouse.SelectedValue != null && Convert.ToInt64(_cmbProductWarehouse.SelectedValue) > 0)
            {
                selectedWarehouseId = Convert.ToInt64(_cmbProductWarehouse.SelectedValue);
            }

            var dt = ProductService.GetAllProducts(search, cat, _chkOnlyCritical.Checked, selectedWarehouseId);

            if (_gridProducts.BindingContext == null && BindingContext != null)
            {
                _gridProducts.BindingContext = BindingContext;
            }

            _gridProducts.DataSource = null;
            _gridProducts.DataSource = dt;

            // Formatlar
            string[] moneyCols = { "Alış Fiyatı", "Satış Fiyatı", "Toplam Tutar" };
            foreach (var col in moneyCols)
            {
                if (_gridProducts.Columns[col] is { } c) c.DefaultCellStyle.Format = "N2";
            }
            string[] qtyCols = { "Açılış", "Gelen", "Satılan", "Kalan Stok", "Kritik Seviye" };
            foreach (var col in qtyCols)
            {
                if (_gridProducts.Columns[col] is { } c) c.DefaultCellStyle.Format = "N2";
            }

            if (_gridProducts.Columns["Fatura No"] is { } colInv)
            {
                colInv.HeaderText = "📄 Fatura PDF";
                colInv.Width = 145;
            }

            if (_gridProducts.Columns["SKT"] is { } colSkt)
            {
                colSkt.HeaderText = "⏳ SKT";
                colSkt.Width = 135;
            }

            if (_gridProducts.Columns["Parti / Lot"] is { } colBatch)
            {
                colBatch.HeaderText = "🏷️ Parti / Lot";
                colBatch.Width = 110;
            }

            _gridProducts.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshProducts Error: {ex}");
            MessageBox.Show($"Ürün listesi yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RefreshAccounts()
    {
        try
        {
            var dt = AccountService.GetAllAccounts(_txtAccountSearch.Text, _cmbAccountType.SelectedItem?.ToString());
            _gridAccounts.DataSource = dt;

            if (_gridAccounts.Columns["Id"] is { } cId) cId.Visible = false;
            if (_gridAccounts.Columns["Kara Liste"] is { } cBlacklist)
            {
                cBlacklist.Width = 115;
                cBlacklist.DisplayIndex = 1;
            }
            if (_gridAccounts.Columns["Bakiye (₺)"] is { } cBal) cBal.DefaultCellStyle.Format = "N2";
            if (_gridAccounts.Columns["Kredi Limiti"] is { } cLim) cLim.DefaultCellStyle.Format = "N2";
        }
        catch { }
    }

    private void GridAccounts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridAccounts.Rows.Count) return;
        if (_gridAccounts.Rows[e.RowIndex].IsNewRow) return;

        var row = _gridAccounts.Rows[e.RowIndex];
        bool isBlacklisted = false;
        if (_gridAccounts.Columns.Contains("Kara Liste"))
        {
            isBlacklisted = Convert.ToInt32(row.Cells["Kara Liste"].Value ?? 0) == 1;
        }

        if (isBlacklisted)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(254, 202, 202);
            row.DefaultCellStyle.SelectionForeColor = Color.FromArgb(153, 27, 27);
        }

        if (_gridAccounts.Columns["Kara Liste"] is { } colBlacklist && e.ColumnIndex == colBlacklist.Index)
        {
            int val = Convert.ToInt32(e.Value ?? 0);
            if (val == 1)
            {
                e.Value = "⛔ KARA LİSTE";
                e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);
                e.CellStyle.Font = new Font(UITheme.RegularFont, FontStyle.Bold);
            }
            else
            {
                e.Value = "✓ Normal";
                e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
            }
            e.FormattingApplied = true;
        }
    }

    private void RefreshStockMovements()
    {
        try
        {
            long? selectedWarehouseId = null;
            if (_cmbStockWarehouse.SelectedValue != null && Convert.ToInt64(_cmbStockWarehouse.SelectedValue) > 0)
            {
                selectedWarehouseId = Convert.ToInt64(_cmbStockWarehouse.SelectedValue);
            }

            var dt = StockService.GetAllStockMovements(
                _txtStockSearch.Text,
                _cmbStockType.SelectedItem?.ToString(),
                _dtpStockStart.Value.Date,
                _dtpStockEnd.Value.Date,
                selectedWarehouseId
            );
            _gridStockMov.DataSource = dt;

            if (_gridStockMov.Columns["Miktar"] is { } cQty) cQty.DefaultCellStyle.Format = "N2";
            if (_gridStockMov.Columns["Birim Fiyat"] is { } cPrice) cPrice.DefaultCellStyle.Format = "N2";
            if (_gridStockMov.Columns["Toplam Tutar"] is { } cTot) cTot.DefaultCellStyle.Format = "N2";
        }
        catch { }
    }

    private void RefreshAccountMovements()
    {
        try
        {
            var dt = TransactionService.GetAllAccountMovements(
                _txtAccMovSearch.Text,
                _cmbAccMovType.SelectedItem?.ToString(),
                _dtpAccStart.Value.Date,
                _dtpAccEnd.Value.Date
            );
            _gridAccMov.DataSource = dt;

            if (_gridAccMov.Columns["Tutar (₺)"] is { } cAccAmount) cAccAmount.DefaultCellStyle.Format = "N2";
        }
        catch { }
    }

    private void RefreshInvoices()
    {
        try
        {
            var dt = InvoiceService.GetAllInvoicesWithPaymentStatus(
                _cmbInvType.SelectedItem?.ToString(),
                _txtInvSearch.Text,
                _cmbInvStatus.SelectedItem?.ToString(),
                _dtpInvStart.Value.Date,
                _dtpInvEnd.Value.Date
            );
            _gridInvoices.DataSource = dt;

            if (_gridInvoices.Columns["Id"] is { } colInvId) colInvId.Visible = false;
            if (_gridInvoices.Columns["AccountId"] is { } colAccId) colAccId.Visible = false;
            if (_gridInvoices.Columns["WarehouseId"] is { } colWId) colWId.Visible = false;

            if (_gridInvoices.Columns["Fatura Tutarı"] is { } cGrand)
            {
                cGrand.DefaultCellStyle.Format = "N2";
                cGrand.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_gridInvoices.Columns["Ödenen Tutar"] is { } cPaid)
            {
                cPaid.DefaultCellStyle.Format = "N2";
                cPaid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_gridInvoices.Columns["Kalan Tutar"] is { } cRem)
            {
                cRem.DefaultCellStyle.Format = "N2";
                cRem.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            // Alt Toplamları Hesapla (Fatura tipine göre dinamik toplam göstergesi)
            int count = dt.Rows.Count;
            double totalGrand = 0;
            double totalPaid = 0;
            double totalRemaining = 0;

            foreach (DataRow r in dt.Rows)
            {
                totalGrand += Convert.ToDouble(r["Fatura Tutarı"]);
                totalPaid += Convert.ToDouble(r["Ödenen Tutar"]);
                totalRemaining += Convert.ToDouble(r["Kalan Tutar"]);
            }

            string typeLabel = _cmbInvType.SelectedItem?.ToString() ?? "Tüm Faturalar";
            _lblInvSummaryCount.Text = $"📋 {typeLabel}: {count} Adet";
            _lblInvSummaryTotal.Text = $"💰 Toplam Fatura: {totalGrand:N2} ₺";
            _lblInvSummaryPaid.Text = $"✅ Toplam Ödenen: {totalPaid:N2} ₺";
            _lblInvSummaryRemaining.Text = $"⚠️ Kalan Borç / Bakiye: {totalRemaining:N2} ₺";
        }
        catch { }
    }

    private void OpenInvoicePaymentDialogForSelected()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.InvoicePayment) && !curUser.HasPermission(UserPermissions.AccountMovements))
        {
            MessageBox.Show("Fatura ödeme ve tahsilat yönetimi modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_gridInvoices.CurrentRow == null || !_gridInvoices.Columns.Contains("Id"))
        {
            MessageBox.Show("Lütfen işlem yapmak istediğiniz faturayı tablodan seçiniz.", "Fatura Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var row = _gridInvoices.CurrentRow;
        long invId = Convert.ToInt64(row.Cells["Id"].Value);
        string invNo = row.Cells["Fatura No"].Value?.ToString() ?? "";
        string invType = row.Cells["Fatura Türü"].Value?.ToString() ?? "Alış Faturası";
        string accName = row.Cells["Cari Ünvanı"].Value?.ToString() ?? "";
        double grandTotal = Convert.ToDouble(row.Cells["Fatura Tutarı"].Value);
        double paidAmount = Convert.ToDouble(row.Cells["Ödenen Tutar"].Value);

        using var dlg = new InvoicePaymentDialog(invId, invNo, invType, accName, grandTotal, paidAmount);
        if (dlg.ShowDialog(this) == DialogResult.OK || dlg.PaymentMade)
        {
            RefreshAll();
        }
    }

    private void OpenInvoiceMetaForSelectedInvoice()
    {
        if (_gridInvoices.CurrentRow == null || !_gridInvoices.Columns.Contains("Id"))
        {
            MessageBox.Show("Lütfen detayını görmek istediğiniz faturayı tablodan seçiniz.", "Fatura Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long invId = Convert.ToInt64(_gridInvoices.CurrentRow.Cells["Id"].Value);
        string invNo = _gridInvoices.CurrentRow.Cells["Fatura No"].Value?.ToString() ?? "";

        // Faturadaki ilk ürünün ID'sini bularak InvoiceMetaDialog aç
        var dtItems = InvoiceService.GetInvoiceItems(invId);
        long pid = 0;
        string pName = $"Fatura: {invNo}";
        if (dtItems.Rows.Count > 0 && dtItems.Columns.Contains("ProductId") && dtItems.Rows[0]["ProductId"] != DBNull.Value)
        {
            pid = Convert.ToInt64(dtItems.Rows[0]["ProductId"]);
            pName = dtItems.Rows[0]["ItemName"]?.ToString() ?? pName;
        }

        using var dlg = new InvoiceMetaDialog(pid, pName);
        dlg.ShowDialog(this);
    }

    private void RefreshAuditLogs()
    {
        try
        {
            var dt = AuditLogService.GetLogs(
                _cmbAuditEntity.SelectedItem?.ToString(),
                _cmbAuditAction.SelectedItem?.ToString(),
                _dtpAuditStart.Value.Date,
                _dtpAuditEnd.Value.Date,
                _txtAuditSearch.Text
            );
            _gridAuditLogs.DataSource = dt;

            if (_gridAuditLogs.Columns["Id"] is { } colAuditId) colAuditId.Width = 60;
            if (_gridAuditLogs.Columns["İşlem Tarihi"] is { } colAuditDate) colAuditDate.Width = 140;
            if (_gridAuditLogs.Columns["Kayıt Türü"] is { } colAuditType) colAuditType.Width = 100;
            if (_gridAuditLogs.Columns["İşlem"] is { } colAuditAction) colAuditAction.Width = 140;
            if (_gridAuditLogs.Columns["İlgili Kayıt / Başlık"] is { } colAuditTitle) colAuditTitle.Width = 220;
        }
        catch { }
    }

    private void ExportAuditLogsToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridAuditLogs, "Sistem Değişiklik ve Silme Logları", "Denetim_Loglari");
    }
    #endregion

    #region CRUD & Aksiyon Metotları
    private long GetSelectedId(DataGridView grid)
    {
        if (grid.SelectedRows.Count == 0) return -1;
        return Convert.ToInt64(grid.SelectedRows[0].Cells[0].Value);
    }

    private void OpenQuickSale(string operation = "Satış", long? productId = null)
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.QuickSale) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Hızlı satış ve fiş modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var f = new QuickSaleDialog(operation, productId);
        if (f.ShowDialog() == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void AddProduct()
    {
        using var f = new ProductForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void EditProduct()
    {
        long id = GetSelectedId(_gridProducts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen düzenlemek istediğiniz ürünü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new ProductForm(id);
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void DeleteProduct()
    {
        long id = GetSelectedId(_gridProducts);
        if (id < 0) return;
        var prod = ProductService.GetById(id);
        string name = prod != null ? $"{prod.Code} - {prod.Name}" : $"Ürün #{id}";

        if (MessageBox.Show($"Seçilen '{name}' ürünü pasif hale getirilsin mi?", "Ürün Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string details = prod != null 
                ? $"Kod: {prod.Code}, Ad: {prod.Name}, Kategori: {prod.Category}, Alış: {prod.PurchasePrice:N2} ₺, Kalan Stok: {prod.CurrentStock:N2}" 
                : $"Id: {id}";

            Database.Execute("UPDATE Products SET IsActive=0 WHERE Id=$id", ("$id", id));
            AuditLogService.Log("Urun", "Silindi / Pasife Alındı", id, name, details, null, "Ürün kullanıcı tarafından pasife alındı");
            RefreshAll();
        }
    }

    private void OpenFastProductEntry()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Ürün tanımlama yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new FastProductEntryDialog();
        dlg.ShowDialog(this);
        RefreshProducts();
        RefreshDashboard();
    }

    private void OpenSelectedProductPriceHistory()
    {
        long id = GetSelectedId(_gridProducts);
        if (id <= 0)
        {
            MessageBox.Show("Lütfen fiyat & stok geçmişini görmek istediğiniz ürünü seçiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dlg = new ProductPriceHistoryDialog(id);
        dlg.ShowDialog(this);
    }

    private void DeleteProductsBulkAction()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Ürün silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedIds = new List<long>();
        foreach (DataGridViewRow row in _gridProducts.SelectedRows)
        {
            if (row.Cells["Id"]?.Value is { } val && long.TryParse(val.ToString(), out long id))
            {
                selectedIds.Add(id);
            }
        }

        if (selectedIds.Count == 0)
        {
            long singleId = GetSelectedId(_gridProducts);
            if (singleId > 0) selectedIds.Add(singleId);
        }

        if (selectedIds.Count == 0)
        {
            MessageBox.Show("Lütfen silmek istediğiniz ürünü veya ürünleri tablodan seçiniz (Ctrl veya Shift tuşu ile birden fazla satır seçebilirsiniz).", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"⚠️ DİKKAT: Seçilen {selectedIds.Count} adet ürünü silmek (arşive almak) istediğinize emin misiniz?\n\nBu işlem ürünleri listeden kaldıracak ve arşive taşıyacaktır.",
            "Toplu Ürün Silme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirm != DialogResult.Yes) return;

        var res = ProductService.DeleteProductsBulk(selectedIds);
        if (res.Success)
        {
            MessageBox.Show(res.Message, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshProducts();
            RefreshDashboard();
        }
        else
        {
            MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteStockMovementsBulkAction()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.StockMovements))
        {
            MessageBox.Show("Stok hareketi silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedIds = new List<long>();
        foreach (DataGridViewRow row in _gridStockMov.SelectedRows)
        {
            if (row.Cells["Id"]?.Value is { } val && long.TryParse(val.ToString(), out long id))
            {
                selectedIds.Add(id);
            }
        }

        if (selectedIds.Count == 0)
        {
            long singleId = GetSelectedId(_gridStockMov);
            if (singleId > 0) selectedIds.Add(singleId);
        }

        if (selectedIds.Count == 0)
        {
            MessageBox.Show("Lütfen silmek istediğiniz stok hareketlerini tablodan seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"⚠️ DİKKAT: Seçilen {selectedIds.Count} adet stok hareketini KALICI OLARAK silmek istediğinize emin misiniz?\n\nBu işlem geri alınamaz ve mevcut ürün stok miktarlarını doğrudan etkiler.",
            "Toplu Stok Hareketi Silme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirm != DialogResult.Yes) return;

        var res = ProductService.DeleteStockMovementsBulk(selectedIds);
        if (res.Success)
        {
            MessageBox.Show(res.Message, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshStockMovements();
            RefreshProducts();
            RefreshDashboard();
        }
        else
        {
            MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteAccountsBulkAction()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("Cari silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedIds = new List<long>();
        foreach (DataGridViewRow row in _gridAccounts.SelectedRows)
        {
            if (row.Cells["Id"]?.Value is { } val && long.TryParse(val.ToString(), out long id))
            {
                selectedIds.Add(id);
            }
        }

        if (selectedIds.Count == 0)
        {
            long singleId = GetSelectedId(_gridAccounts);
            if (singleId > 0) selectedIds.Add(singleId);
        }

        if (selectedIds.Count == 0)
        {
            MessageBox.Show("Lütfen silmek istediğiniz cari hesapları tablodan seçiniz.", "Seçim Yapılmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"⚠️ DİKKAT: Seçilen {selectedIds.Count} adet cari hesabı silmek (pasife almak) istediğinize emin misiniz?",
            "Toplu Cari Silme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirm != DialogResult.Yes) return;

        var res = AccountService.DeleteAccountsBulk(selectedIds);
        if (res.Success)
        {
            MessageBox.Show(res.Message, "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAccounts();
            RefreshDashboard();
        }
        else
        {
            MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ViewProductHistory()
    {
        long id = GetSelectedId(_gridProducts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen hareketlerini görmek istediğiniz ürünü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new ProductHistoryDialog(id);
        f.ShowDialog();
    }

    private void PrintBarcodeLabelForSelected()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.BarcodeLabels))
        {
            MessageBox.Show("Barkod ve fiyat etiketi basma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long id = GetSelectedId(_gridProducts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen etiket basmak istediğiniz ürünü tablodan seçiniz.", "Ürün Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prod = ProductService.GetById(id);
        if (prod == null)
        {
            MessageBox.Show("Ürün bilgisi bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var dlg = new BarcodeLabelDialog(prod);
        dlg.ShowDialog();
    }

    private void OpenMobileScanner()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.MobileScanner) && !curUser.IsSuperUser)
        {
            MessageBox.Show("Mobil canlı barkod okuyucu modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new MobileScannerDialog();
        dlg.ShowDialog(this);
    }



    private void OnBarcodeScannedFromMobile(string barcode, Product? prod)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnBarcodeScannedFromMobile(barcode, prod)));
            return;
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            _txtProductSearch.Text = barcode;
            RefreshProducts();
        }
    }

    private void OpenSelectedProductInvoiceMetaDialog()
    {
        long id = GetSelectedId(_gridProducts);
        if (id <= 0)
        {
            MessageBox.Show("Lütfen faturasına bakmak istediğiniz ürünü tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prod = ProductService.GetById(id);
        string prodName = prod != null ? $"{prod.Code} - {prod.Name}" : $"Ürün #{id}";

        using var dlg = new InvoiceMetaDialog(id, prodName);
        dlg.ShowDialog(this);
    }

    private void OpenSelectedProductInvoicePdf()
    {
        long id = GetSelectedId(_gridProducts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen faturasına bakmak istediğiniz ürünü tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var prod = ProductService.GetById(id);
        string prodName = prod != null ? $"{prod.Code} - {prod.Name}" : $"Ürün #{id}";

        if (!InvoiceService.OpenLatestInvoicePdfForProduct(id))
        {
            MessageBox.Show($"'{prodName}' ürününe ait sistemde kayıtlı bir e-fatura PDF belgesi bulunamadı.\n\nBu ürün sisteme henüz e-fatura ile yüklenmemiş veya manuel açılmış olabilir.", "Fatura Belgesi Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void AddAccount()
    {
        using var f = new AccountForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void EditAccount()
    {
        long id = GetSelectedId(_gridAccounts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen düzenlemek istediğiniz cariyi seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new AccountForm(id);
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void DeleteAccount()
    {
        long id = GetSelectedId(_gridAccounts);
        if (id < 0) return;
        var acc = AccountService.GetById(id);
        string name = acc?.Name ?? $"Cari #{id}";

        if (MessageBox.Show($"Seçilen '{name}' carisi pasif hale getirilsin (silinsin) mi?", "Cari Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string oldDetails = acc != null 
                ? $"Ad: {acc.Name}, Tür: {acc.Type}, Tel: {acc.Phone}, Vergi No: {acc.TaxNumber}, Limit: {acc.BalanceLimit:N2} ₺, Bakiye: {acc.Balance:N2} ₺" 
                : $"Id: {id}";

            Database.Execute("UPDATE Accounts SET IsActive=0 WHERE Id=$id", ("$id", id));
            AuditLogService.Log("Cari", "Silindi / Pasife Alındı", id, name, oldDetails, null, "Cari kullanıcı tarafından silindi/pasife alındı");
            RefreshAll();
        }
    }

    private void ViewAccountStatement()
    {
        long id = GetSelectedId(_gridAccounts);
        if (id < 0)
        {
            MessageBox.Show("Lütfen hesap ekstresini görmek istediğiniz cariyi seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new AccountStatementDialog(id);
        f.ShowDialog();
        RefreshAll();
    }

    private void AddAccountMovementForSelected(string? explicitType = null)
    {
        long id = GetSelectedId(_gridAccounts);
        if (id < 0)
        {
            using var fAll = new AccountMovementForm(null, explicitType);
            if (fAll.ShowDialog() == DialogResult.OK) RefreshAll();
            return;
        }
        var acc = AccountService.GetById(id);
        string defaultType = !string.IsNullOrEmpty(explicitType)
            ? explicitType
            : (acc?.Type == "Müşteri" ? "Tahsilat" : "Ödeme");
        using var f = new AccountMovementForm(id, defaultType);
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void SendWhatsAppForSelected()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.DigitalReceipt) && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("WhatsApp ile borç ve bakiye bildirimi yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long id = GetSelectedId(_gridAccounts);
        if (id <= 0)
        {
            MessageBox.Show("Lütfen WhatsApp bildirimi göndermek istediğiniz cariyi tablodan seçiniz.", "Cari Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var acc = AccountService.GetById(id);
        if (acc == null) return;

        string digitsOnly = new string((acc.Phone ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digitsOnly))
        {
            string inPhone = Microsoft.VisualBasic.Interaction.InputBox($"'{acc.Name}' carisi için kayıtlı telefon bulunamadı. Lütfen cep telefonu giriniz (Örn: 0532 123 45 67):", "WhatsApp Numarası", "");
            digitsOnly = new string(inPhone.Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digitsOnly)) return;
        }

        if (digitsOnly.Length == 10 && digitsOnly.StartsWith("5")) digitsOnly = "90" + digitsOnly;
        else if (digitsOnly.Length == 11 && digitsOnly.StartsWith("05")) digitsOnly = "90" + digitsOnly.Substring(1);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Sayın *{acc.Name}*,");
        sb.AppendLine();
        if (acc.Balance > 0)
        {
            sb.AppendLine($"Bilensis sistemimizde kayıtlı güncel veresiye/açık hesap borç bakiyeniz: *{acc.Balance:N2} ₺*'dir.");
            sb.AppendLine("Ödemenizi en kısa sürede gerçekleştirmenizi rica eder, hayırlı ve bereketli işler dileriz.");
        }
        else if (acc.Balance < 0)
        {
            sb.AppendLine($"Bilensis sistemimizde kayıtlı lehinize alacak bakiyeniz: *{Math.Abs(acc.Balance):N2} ₺*'dir.");
        }
        else
        {
            sb.AppendLine("Bilensis sistemimizde kayıtlı herhangi bir borç veya alacak bakiyeniz bulunmamaktadır. Hesap bakiyeniz sıfırdır.");
        }

        string url = $"https://wa.me/{digitsOnly}?text={Uri.EscapeDataString(sb.ToString())}";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WhatsApp açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddStock()
    {
        using var f = new StockForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void OpenStockCount()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.StockCount))
        {
            MessageBox.Show("Stok sayımı ve depo envanter düzeltme modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new StockCountDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void OpenWarehouseTransfer()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Warehouses))
        {
            MessageBox.Show("Depolar arası ürün transferi modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new WarehouseTransferDialog();
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void OpenWarehouseManage()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Warehouses))
        {
            MessageBox.Show("Depo tanımlama ve yönetim modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new WarehouseManageDialog();
        dlg.ShowDialog();
        SyncWarehouses();
        RefreshAll();
    }

    private void OpenExcelImport(string type = "Urun")
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ExcelOperations))
        {
            MessageBox.Show("Excel ile veri içe aktarma modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new ExcelImportDialog(type);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void OpenInvoiceEntry(string? filePath = null)
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.InvoiceEntry) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("E-Fatura / Alış Faturası girişine erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new InvoiceEntryDialog(filePath);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void OpenTelegramConfig()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.Telegram) && !curUser.IsSuperUser)
        {
            MessageBox.Show("Telegram asistanı yapılandırma modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new TelegramConfigDialog();
        dlg.ShowDialog(this);
    }

    private void DeleteStock()
    {
        long id = GetSelectedId(_gridStockMov);
        if (id < 0) return;
        var dt = Database.Query(@"
SELECT sm.Id, sm.MovementDate, sm.MovementType, sm.Quantity, sm.UnitPrice, sm.DocumentNo, p.Code, p.Name AS ProductName 
FROM StockMovements sm 
JOIN Products p ON p.Id = sm.ProductId 
WHERE sm.Id = $id", ("$id", id));

        if (dt.Rows.Count == 0) return;
        var r = dt.Rows[0];
        string prodName = $"{r["Code"]} - {r["ProductName"]}";
        string type = r["MovementType"]?.ToString() ?? "";
        double qty = Convert.ToDouble(r["Quantity"]);

        if (MessageBox.Show($"Seçilen '{prodName}' ürününe ait {qty:N2} adetlik {type} hareketi silinsin mi?", "Stok Hareketi Silme", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string details = $"Tarih: {r["MovementDate"]}, Ürün: {prodName}, Hareket: {type}, Miktar: {qty:N2}, Belge No: {r["DocumentNo"]}";

            Database.Execute("DELETE FROM StockMovements WHERE Id=$id", ("$id", id));
            AuditLogService.Log("StokHareket", "Silindi", id, $"{prodName} - {type} ({qty:N2})", details, null, "Stok hareketi kaydı silindi");
            RefreshAll();
        }
    }

    private void AddAccountMovement()
    {
        using var f = new AccountMovementForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void DeleteAccountMovement()
    {
        long id = GetSelectedId(_gridAccMov);
        if (id < 0) return;
        var dt = Database.Query(@"
SELECT m.Id, m.MovementDate, m.TransactionType, m.Amount, m.DocumentNo, m.Method, m.CashBank, m.Note, a.Name AS AccountName 
FROM AccountMovements m 
JOIN Accounts a ON a.Id = m.AccountId 
WHERE m.Id = $id", ("$id", id));

        if (dt.Rows.Count == 0) return;
        var r = dt.Rows[0];
        string accName = r["AccountName"]?.ToString() ?? "";
        string type = r["TransactionType"]?.ToString() ?? "";
        double amount = Convert.ToDouble(r["Amount"]);
        string docNo = r["DocumentNo"]?.ToString() ?? "";
        string method = r["Method"]?.ToString() ?? "";

        if (MessageBox.Show($"Seçilen '{accName}' carisine ait {amount:N2} ₺ tutarındaki {type} işlemi silinsin mi?", "İşlem Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            string details = $"Tarih: {r["MovementDate"]}, Cari: {accName}, İşlem: {type}, Tutar: {amount:N2} ₺, Belge No: {docNo}, Ödeme: {method}, Not: {r["Note"]}";

            Database.Execute("DELETE FROM AccountMovements WHERE Id=$id", ("$id", id));
            AuditLogService.Log("CariHareket", "Silindi", id, $"{accName} - {type} ({amount:N2} ₺)", details, null, "Finansal hareket kaydı kalıcı olarak silindi");
            RefreshAll();
        }
    }
    #endregion

    #region Excel Dışa Aktarma & Yedekleme
    private void ExportProductsToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridProducts, "Ürünler ve Stok Listesi", "Urun_Listesi");
    }

    private void ExportAccountsToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridAccounts, "Cari ve Veresiye Bakiyeleri", "Cari_Listesi");
    }

    private void ExportStockToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridStockMov, "Stok Hareketleri Raporu", "Stok_Hareketleri");
    }

    private void ExportAccMovToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridAccMov, "Finansal Hareketler Raporu", "Finansal_Hareketler");
    }

    private void ExportInvoicesToExcel(object? sender, EventArgs e)
    {
        ExportGridToExcel(_gridInvoices, "Alış ve Satış Faturaları Raporu", "Faturalar_Raporu");
    }

    private void OpenInvoicesPage()
    {
        ShowPage(4);
        if (_tabsFinance.TabPages.Count > 1)
        {
            _tabsFinance.SelectedIndex = 1;
        }
        RefreshInvoices();
    }

    private void ExportGridToExcel(DataGridView grid, string title, string defaultFileName)
    {
        if (grid.DataSource is not DataTable dt || dt.Rows.Count == 0)
        {
            MessageBox.Show("Dışa aktarılacak kayıt bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Excel Dosyası (*.xlsx)|*.xlsx",
            FileName = $"{defaultFileName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                ExcelService.ExportDataTableToExcel(dt, title, sfd.FileName);
                MessageBox.Show("Veriler başarıyla Excel'e aktarıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BackupDatabaseClick(object? sender, EventArgs e)
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "SQL Server Veritabanı Yedeği (*.bak)|*.bak|Tüm Dosyalar (*.*)|*.*",
            FileName = $"BilgeStok_Yedek_{DateTime.Now:yyyyMMdd_HHmm}.bak"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                Database.BackupDatabase(sfd.FileName);
                MessageBox.Show("SQL Server veritabanı yedeği (.bak) başarıyla alındı!", "Yedekleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedekleme sırasında hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RestoreDatabaseClick(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "SQL Server Veritabanı Yedeği (*.bak)|*.bak|Tüm Dosyalar (*.*)|*.*",
            Title = "Geri Yüklenecek SQL Server Yedek Dosyasını (.bak) Seçin"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            if (MessageBox.Show("Mevcut veriler silinerek seçilen yedek dosyasındaki veriler yüklenecektir. Devam etmek istiyor musunuz?", "Yedekten Geri Yükleme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    Database.RestoreDatabase(ofd.FileName);
                    MessageBox.Show("Veritabanı başarıyla geri yüklendi!", "Geri Yükleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Geri yükleme sırasında hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    private void ImportExcelClick(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog { Filter = "Excel Dosyası (*.xlsx)|*.xlsx" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        var ask = MessageBox.Show(
            "Mevcut veritabanı temizlenerek mi Excel aktarımı yapılsın?\n\n[Evet] = Tüm mevcut verileri sil ve Excel'i yükle\n[Hayır] = Mevcut kayıtların üzerine ekle/güncelle",
            "Excel İçe Aktarımı",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question
        );

        if (ask == DialogResult.Cancel) return;

        try
        {
            var res = ExcelService.ImportFromExcel(ofd.FileName, ask == DialogResult.Yes);
            MessageBox.Show(
                $"Excel Aktarımı Başarıyla Tamamlandı!\n\n" +
                $"📦 Ürün Sayısı: {res.products}\n" +
                $"🔄 Stok Hareketi: {res.stock}\n" +
                $"👥 Cari Kart Sayısı: {res.accounts}\n" +
                $"💳 Cari Finansal Hareket: {res.accountMovements}",
                "Aktarım Başarılı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Excel aktarımı hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    #endregion
}
