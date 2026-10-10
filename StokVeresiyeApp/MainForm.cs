using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp;

public class MainForm : Form
{
    // Ribbon & Navigasyon
    private readonly CmdRegistry _ribbon = new();
    private readonly Panel _contentArea = new();
    private readonly TableLayoutPanel _shellLayout = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusUser = new();
    private readonly ToolStripStatusLabel _statusLicense = new();
    private readonly ToolStripStatusLabel _statusDb = new();
    private readonly ToolStripStatusLabel _statusClock = new();
    private CmdButton? _ribbonBtnNotify;
    private int _currentNavIndex = 0;
    public bool IsLoggedOut { get; private set; } = false;

    // Modern SaaS Sol Sidebar & Üst Header Kontrolleri
    private readonly Panel _pnlSidebar = new();
    private readonly FlowLayoutPanel _pnlNavButtons = new();
    private readonly Panel _pnlTopHeader = new();
    private readonly Label _lblPageTitle = new();
    private readonly Label _lblPageSubTitle = new();
    private readonly TextBox _txtGlobalSearch = new();
    private readonly Panel _pnlGlobalResults = new();
    private readonly ListBox _lstGlobalResults = new();
    private readonly Button _btnHeaderNotify = new();
    private readonly Button _btnSidebarToggle = new();
    private readonly Label _lblSidebarBrand = new();
    private readonly Label _lblSidebarBadge = new();
    private bool _isSidebarCollapsed = false;
    private TableLayoutPanel? _sidebarLogoLayout;
    private TableLayoutPanel? _sidebarUserLayout;
    private readonly List<Control> _sidebarExpandedOnly = new();

    // Sidebar Navigasyon Butonları
    private SidebarNavButton _btnNavDash = new();
    private SidebarNavButton _btnNavQuickSale = new();
    private SidebarNavButton _btnNavProducts = new();
    private SidebarNavButton _btnNavAccounts = new();
    private SidebarNavButton _btnNavInvoices = new();
    private SidebarNavButton _btnNavStockMov = new();
    private SidebarNavButton _btnNavFinance = new();
    private SidebarNavButton _btnNavBatchInv = new();
    private SidebarNavButton _btnNavWhatsApp = new();
    private SidebarNavButton _btnNavAudit = new();
    private SidebarNavButton _btnNavUsers = new();
    private SidebarNavButton _btnNavSettings = new();
    private SidebarNavButton _btnNavLicense = new();
    private readonly List<SidebarNavButton> _allNavButtons = new();

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
    private DataGridView _gridUsers = new();
    private TextBox _txtUserSearch = new();

    // Audit Log Sayfası Kontrolleri
    private DataGridView _gridAuditLogs = new();
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
    private DataGridView _gridCriticalStock = new();
    private DataGridView _gridTopDebtors = new();
    private DataGridView _gridRecentTransactions = new();
    private TextBox _txtDashProductSearch = new();
    private DataGridView _gridDashProductSearch = new();

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
    private DataGridView _gridInvoices = new();
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
    private DataGridView _gridProducts = new();
    private TextBox _txtProductSearch = new();
    private ComboBox _cmbProductCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbProductWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private CheckBox _chkOnlyCritical = new() { Text = "Sadece Kritik Stoklar", AutoSize = true, Font = UITheme.RegularFont };

    // Cari Sayfası Kontrolleri
    private DataGridView _gridAccounts = new();
    private TextBox _txtAccountSearch = new();
    private ComboBox _cmbAccountType = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    // Stok Hareketleri Sayfası Kontrolleri
    private DataGridView _gridStockMov = new();
    private TextBox _txtStockSearch = new();
    private ComboBox _cmbStockType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private ComboBox _cmbStockWarehouse = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpStockStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private DateTimePicker _dtpStockEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

    // Kasa & Finansal Hareketler Sayfası Kontrolleri
    private DataGridView _gridAccMov = new();
    private TextBox _txtAccMovSearch = new();
    private ComboBox _cmbAccMovType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private DateTimePicker _dtpAccStart = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    private DateTimePicker _dtpAccEnd = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    private TabControl _tabsFinance = new();
    private bool _isRefreshing = false;

    public MainForm()
    {
        DoubleBuffered = true;
        Text = "Bilensis - Stok & Cari Yönetim Sistemi";
        Width = 1380;
        Height = 850;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
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
            else if (e.Control && e.KeyCode == Keys.K)
            {
                OpenCommandPalette();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _pnlGlobalResults.Visible)
            {
                _pnlGlobalResults.Visible = false;
                e.Handled = true;
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
            else if (e.KeyCode == Keys.F8)
            {
                OpenSaleReturn(); // F8: Satış İadesi & Değişim
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

        // Günlük Otomatik Bulut Yedekleme Kontrolü (veritabanı + yalnızca PDF faturalar ayrı ayrı)
        Task.Run(() =>
        {
            CloudBackupService.AutoCheckDailyBackup();
            PdfArchiveService.AutoCheckDailyPdfBackup();
        });

        // Program gece boyunca açık kalırsa gün değiştiğinde de günlük yedek alınsın (yarım saatte bir kontrol)
        var dailyBackupTimer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
        dailyBackupTimer.Tick += (s, e) => Task.Run(() =>
        {
            CloudBackupService.AutoCheckDailyBackup();
            PdfArchiveService.AutoCheckDailyPdfBackup();
        });
        dailyBackupTimer.Start();
        FormClosed += (s, e) => dailyBackupTimer.Dispose();

        // Program Kapanışında Otomatik Bulut Yedekleme (Aktifse ve oturum kapatma değilse çalışır)
        FormClosing += (s, e) =>
        {
            try
            {
                if (!IsLoggedOut && CloudBackupService.Config.BackupOnExit)
                {
                    CloudBackupService.ExecuteBackup(silent: true);
                    // PDF'ler artık veritabanı yedeğinde değil: ayrı ve artımlı (yalnızca yeni dosyalar) kopyalanır
                    if (CloudBackupService.Config.DailyPdfBackupEnabled) PdfArchiveService.BackupAll();
                }
            }
            catch { }
        };
    }

    private void BuildLayout()
    {
        // 1. Ribbon Menüsü (Arka planda hazır tutulur, dikey yer kaplamaması için formdan gizlenir)
        BuildRibbon();

        // 2. Ana Shell Düzeni: Sidebar / Header / Content tek kök çerçeve içinde çakışmaz
        _shellLayout.Dock = DockStyle.Fill;
        _shellLayout.ColumnCount = 2;
        _shellLayout.RowCount = 2;
        _shellLayout.Padding = new Padding(0);
        _shellLayout.Margin = new Padding(0);
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236F));
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _shellLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        _shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // 3. Sol Modern SaaS Sidebar (Koyu Slate #0F172A)
        BuildSidebar();
        _pnlSidebar.Dock = DockStyle.Fill;
        _shellLayout.Controls.Add(_pnlSidebar, 0, 1);

        // 4. Üst Modern SaaS Header (Beyaz #FFFFFF)
        BuildTopHeader();
        _pnlTopHeader.Dock = DockStyle.Fill;
        _shellLayout.Controls.Add(_pnlTopHeader, 0, 0);
        _shellLayout.SetColumnSpan(_pnlTopHeader, 2);

        // 5. İçerik Alanı (Kalan tüm alan %100 Fill, sol sidebar ve üst header ile çakışmaz)
        _contentArea.Dock = DockStyle.Fill;
        _contentArea.BackColor = UITheme.Background;
        _contentArea.Padding = new Padding(12);
        _shellLayout.Controls.Add(_contentArea, 1, 1);

        Controls.Add(_shellLayout);
        _shellLayout.BringToFront();

        _contentArea.AutoScroll = true;
        _pnlDashboard.AutoScroll = true;

        // 6. Alt Durum Çubuğu (StatusStrip)
        BuildStatusStrip();
        Controls.Add(_statusStrip);
        _statusStrip.BringToFront();

        // 7. Global Spotlight Arama Sonuç Paneli (Overlay)
        BuildGlobalSearchPopup();
        Controls.Add(_pnlGlobalResults);
        _pnlGlobalResults.BringToFront();

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

    private SidebarNavButton CreateNavBtn(string icon, string text, Action onClick)
    {
        var btn = new SidebarNavButton
        {
            Icon = icon,
            Text = text,
            Width = 206,
            Height = 44,
            Margin = new Padding(5, 2, 5, 2)
        };
        btn.Click += (s, e) => onClick();
        _allNavButtons.Add(btn);
        return btn;
    }

    private void BuildSidebar()
    {
        _pnlSidebar.Dock = DockStyle.Fill;
        _pnlSidebar.Width = 236;
        _pnlSidebar.BackColor = UITheme.SidebarBg;
        _pnlSidebar.Padding = new Padding(0);

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        var pnlLogo = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(12, 10, 12, 10)
        };

        var logoLayout = _sidebarLogoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        logoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        logoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
        logoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));

        _lblSidebarBrand.Text = "BİLENSİS";
        _lblSidebarBrand.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblSidebarBrand.ForeColor = Color.White;
        _lblSidebarBrand.AutoSize = false;
        _lblSidebarBrand.Dock = DockStyle.Fill;
        _lblSidebarBrand.TextAlign = ContentAlignment.MiddleLeft;
        _lblSidebarBrand.Cursor = Cursors.Hand;
        _lblSidebarBrand.Margin = new Padding(0, 0, 6, 0);
        _lblSidebarBrand.Click += (s, e) => ShowPage(0);

        _lblSidebarBadge.Text = "v2.9";
        _lblSidebarBadge.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        _lblSidebarBadge.ForeColor = Color.White;
        _lblSidebarBadge.BackColor = UITheme.Primary;
        _lblSidebarBadge.AutoSize = false;
        _lblSidebarBadge.Dock = DockStyle.Fill;
        _lblSidebarBadge.TextAlign = ContentAlignment.MiddleCenter;
        _lblSidebarBadge.Margin = new Padding(0);
        _lblSidebarBadge.MaximumSize = new Size(42, 18);
        _lblSidebarBadge.MinimumSize = new Size(38, 18);

        _btnSidebarToggle.Text = "☰";
        _btnSidebarToggle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _btnSidebarToggle.ForeColor = Color.FromArgb(148, 163, 184);
        _btnSidebarToggle.BackColor = Color.Transparent;
        _btnSidebarToggle.FlatStyle = FlatStyle.Flat;
        _btnSidebarToggle.FlatAppearance.BorderSize = 0;
        _btnSidebarToggle.Size = new Size(30, 30);
        _btnSidebarToggle.Dock = DockStyle.Fill;
        _btnSidebarToggle.Cursor = Cursors.Hand;
        _btnSidebarToggle.Click += (s, e) => ToggleSidebar();

        logoLayout.Controls.Add(_lblSidebarBrand, 0, 0);
        logoLayout.Controls.Add(_lblSidebarBadge, 1, 0);
        logoLayout.Controls.Add(_btnSidebarToggle, 2, 0);
        pnlLogo.Controls.Add(logoLayout);

        var pnlUserCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(11, 17, 32),
            Padding = new Padding(10, 3, 10, 3)
        };

        var userLayout = _sidebarUserLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        userLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        userLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        userLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        userLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

        var curUser = UserService.CurrentUser;
        var lblUserName = new Label
        {
            Text = curUser?.FullName ?? "Admin",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 2, 4, 0),
            Margin = new Padding(0, 0, 4, 0)
        };
        var lblUserRole = new Label
        {
            Text = curUser?.Role ?? "Yönetici",
            Font = new Font("Segoe UI", 8f, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 0, 4, 0),
            Margin = new Padding(0, 0, 4, 0)
        };

        var btnLogout = new Button
        {
            Text = "Çıkış",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(248, 113, 113),
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.Click += (s, e) => LogoutAction();

        _sidebarExpandedOnly.Add(lblUserName);
        _sidebarExpandedOnly.Add(lblUserRole);
        userLayout.Controls.Add(lblUserName, 0, 0);
        userLayout.Controls.Add(lblUserRole, 0, 1);
        userLayout.Controls.Add(btnLogout, 1, 0);
        userLayout.SetRowSpan(btnLogout, 2);
        pnlUserCard.Controls.Add(userLayout);

        _pnlNavButtons.Dock = DockStyle.Fill;
        _pnlNavButtons.BackColor = UITheme.SidebarBg;
        _pnlNavButtons.FlowDirection = FlowDirection.TopDown;
        _pnlNavButtons.WrapContents = false;
        _pnlNavButtons.AutoScroll = true;
        _pnlNavButtons.Padding = new Padding(4, 4, 4, 4);

        _btnNavDash = CreateNavBtn("📊", "Genel Bakış", () => ShowPage(0));
        _btnNavQuickSale = CreateNavBtn("⚡", "Hızlı Satış (POS)", () => OpenQuickSale("Satış"));
        _btnNavProducts = CreateNavBtn("📦", "Ürünler & Stok", () => ShowPage(1));
        _btnNavAccounts = CreateNavBtn("👥", "Cari Hesaplar", () => ShowPage(2));
        _btnNavInvoices = CreateNavBtn("📄", "Faturalar", () => { ShowPage(4); _tabsFinance.SelectedIndex = 1; });
        _btnNavStockMov = CreateNavBtn("🔄", "Stok Hareketleri", () => ShowPage(3));
        _btnNavFinance = CreateNavBtn("💳", "Kasa & Finans", () => { ShowPage(4); _tabsFinance.SelectedIndex = 0; });
        _btnNavBatchInv = CreateNavBtn("📂", "Toplu Fatura Aktar", () => OpenBatchInvoiceImportDialog());
        _btnNavWhatsApp = CreateNavBtn("💬", "WhatsApp & Mobil", () => OpenWhatsAppAssistantDialog());
        _btnNavAudit = CreateNavBtn("🕒", "İşlem Logları", () => ShowPage(5));
        _btnNavUsers = CreateNavBtn("👤", "Kullanıcı & Yetki", () => ShowPage(6));
        _btnNavSettings = CreateNavBtn("⚙️", "Ayarlar & Yedek", () => OpenSettings(0));
        _btnNavLicense = CreateNavBtn("🔑", "Lisans", () => ShowPage(7));

        _pnlNavButtons.Controls.Add(new SidebarSectionTitle { Text = "Genel" });
        _pnlNavButtons.Controls.Add(_btnNavDash);
        _pnlNavButtons.Controls.Add(_btnNavQuickSale);

        _pnlNavButtons.Controls.Add(new SidebarSectionTitle { Text = "Ticari İşlemler" });
        _pnlNavButtons.Controls.Add(_btnNavProducts);
        _pnlNavButtons.Controls.Add(_btnNavAccounts);
        _pnlNavButtons.Controls.Add(_btnNavInvoices);
        _pnlNavButtons.Controls.Add(_btnNavStockMov);
        _pnlNavButtons.Controls.Add(_btnNavFinance);

        _pnlNavButtons.Controls.Add(new SidebarSectionTitle { Text = "Operasyon" });
        _pnlNavButtons.Controls.Add(_btnNavBatchInv);
        _pnlNavButtons.Controls.Add(_btnNavWhatsApp);

        _pnlNavButtons.Controls.Add(new SidebarSectionTitle { Text = "Yönetim" });
        _pnlNavButtons.Controls.Add(_btnNavAudit);
        _pnlNavButtons.Controls.Add(_btnNavUsers);
        _pnlNavButtons.Controls.Add(_btnNavSettings);
        _pnlNavButtons.Controls.Add(_btnNavLicense);

        sidebarLayout.Controls.Add(pnlLogo, 0, 0);
        sidebarLayout.Controls.Add(_pnlNavButtons, 0, 1);
        sidebarLayout.Controls.Add(pnlUserCard, 0, 2);
        _pnlSidebar.Controls.Add(sidebarLayout);
    }

    private void ToggleSidebar()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        var collapsedWidth = 62F;
        var expandedWidth = 236F;

        _shellLayout.ColumnStyles[0].SizeType = SizeType.Absolute;
        _shellLayout.ColumnStyles[0].Width = _isSidebarCollapsed ? collapsedWidth : expandedWidth;
        _pnlSidebar.Width = _isSidebarCollapsed ? (int)collapsedWidth : (int)expandedWidth;
        _pnlSidebar.MinimumSize = new Size((int)collapsedWidth, 0);
        _pnlSidebar.MaximumSize = new Size((int)expandedWidth, 0);

        _lblSidebarBrand.Visible = !_isSidebarCollapsed;
        _lblSidebarBadge.Visible = !_isSidebarCollapsed;

        // Daraltılmış modda tek görünen buton açma/kapama düğmesi olmalı; sütunlar 62 px içinde ona yer bırakır
        if (_sidebarLogoLayout != null)
        {
            _sidebarLogoLayout.ColumnStyles[0] = _isSidebarCollapsed ? new ColumnStyle(SizeType.Absolute, 0F) : new ColumnStyle(SizeType.Percent, 100F);
            _sidebarLogoLayout.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, _isSidebarCollapsed ? 0F : 42F);
            _sidebarLogoLayout.ColumnStyles[2] = _isSidebarCollapsed ? new ColumnStyle(SizeType.Percent, 100F) : new ColumnStyle(SizeType.Absolute, 30F);
        }
        if (_sidebarUserLayout != null)
        {
            _sidebarUserLayout.ColumnStyles[0] = _isSidebarCollapsed ? new ColumnStyle(SizeType.Absolute, 0F) : new ColumnStyle(SizeType.Percent, 100F);
            _sidebarUserLayout.ColumnStyles[1] = _isSidebarCollapsed ? new ColumnStyle(SizeType.Percent, 100F) : new ColumnStyle(SizeType.Absolute, 58F);
        }
        foreach (var c in _sidebarExpandedOnly) c.Visible = !_isSidebarCollapsed;

        foreach (var btn in _allNavButtons)
        {
            btn.IsCollapsed = _isSidebarCollapsed;
            btn.Width = _isSidebarCollapsed ? 44 : 206;
            btn.Visible = true;
            btn.Invalidate();
        }

        foreach (Control c in _pnlNavButtons.Controls)
        {
            if (c is SidebarSectionTitle title)
            {
                title.IsCollapsed = _isSidebarCollapsed;
                title.Visible = !_isSidebarCollapsed;
            }
            else
            {
                c.Visible = true;
            }
        }

        _pnlNavButtons.AutoScroll = true;
        _pnlNavButtons.PerformLayout();
        _pnlSidebar.PerformLayout();
        _shellLayout.PerformLayout();
        _pnlTopHeader.Invalidate();
        _contentArea.Invalidate();
    }

    private void BuildTopHeader()
    {
        _pnlTopHeader.Dock = DockStyle.Fill;
        _pnlTopHeader.Height = 54;
        _pnlTopHeader.BackColor = Color.White;
        _pnlTopHeader.Padding = new Padding(12, 0, 12, 0);

        _pnlTopHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(UITheme.BorderColor, 1);
            e.Graphics.DrawLine(pen, 0, _pnlTopHeader.Height - 1, _pnlTopHeader.Width, _pnlTopHeader.Height - 1);
        };

        // Sol Panel: Sayfa Başlığı ve Açıklaması (Dock Left, asla sağdaki butonlarla çakışmaz)
        var pnlTitle = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 6, 8, 0)
        };

        _lblPageTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        _lblPageTitle.ForeColor = UITheme.TextPrimary;
        _lblPageTitle.Text = "Genel Bakış (Dashboard)";
        _lblPageTitle.AutoSize = true;
        _lblPageTitle.Margin = new Padding(0);

        _lblPageSubTitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        _lblPageSubTitle.ForeColor = UITheme.TextSecondary;
        _lblPageSubTitle.Text = "İşletmenizin anlık finansal ve operasyonel göstergeleri";
        _lblPageSubTitle.AutoSize = true;
        _lblPageSubTitle.Margin = new Padding(1, 0, 0, 0);

        pnlTitle.Controls.Add(_lblPageTitle);
        pnlTitle.Controls.Add(_lblPageSubTitle);

        // Sağ Panel: Hızlı Aksiyon Butonları & Arama (Dock Right)
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 0, 0)
        };

        // Arama Kutusu
        var pnlSearchContainer = new Panel
        {
            Width = 220,
            Height = 32,
            BackColor = Color.FromArgb(241, 245, 249),
            Margin = new Padding(4, 1, 6, 0),
            Padding = new Padding(6, 4, 6, 4)
        };
        pnlSearchContainer.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlSearchContainer.Width - 1, pnlSearchContainer.Height - 1);
        };

        _txtGlobalSearch.Dock = DockStyle.Fill;
        _txtGlobalSearch.BorderStyle = BorderStyle.None;
        _txtGlobalSearch.BackColor = Color.FromArgb(241, 245, 249);
        _txtGlobalSearch.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        _txtGlobalSearch.PlaceholderText = "🔍 Hızlı Ara (Ctrl+K)";
        _txtGlobalSearch.TextChanged += (s, e) => OnGlobalSearchTextChanged();
        _txtGlobalSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                if (_lstGlobalResults.Items.Count > 0)
                {
                    _lstGlobalResults.Focus();
                    _lstGlobalResults.SelectedIndex = 0;
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _pnlGlobalResults.Visible = false;
            }
        };
        pnlSearchContainer.Controls.Add(_txtGlobalSearch);

        // Butonlar (Standart font ve boyutlar)
        var btnSale = new Button
        {
            Text = "⚡ Satış (F3)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = UITheme.Primary,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            Width = 85,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 0)
        };
        btnSale.FlatAppearance.BorderSize = 0;
        btnSale.Click += (s, e) => OpenQuickSale("Satış");

        var btnBuy = new Button
        {
            Text = "📥 Alış (F4)",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = UITheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            Width = 80,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 0)
        };
        btnBuy.FlatAppearance.BorderSize = 0;
        btnBuy.Click += (s, e) => OpenQuickSale("Alış");

        _btnHeaderNotify.Text = "🔔 Bildirim";
        _btnHeaderNotify.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        _btnHeaderNotify.BackColor = Color.FromArgb(241, 245, 249);
        _btnHeaderNotify.ForeColor = UITheme.TextPrimary;
        _btnHeaderNotify.FlatStyle = FlatStyle.Flat;
        _btnHeaderNotify.Height = 32;
        _btnHeaderNotify.AutoSize = true;
        _btnHeaderNotify.Padding = new Padding(6, 0, 6, 0);
        _btnHeaderNotify.Cursor = Cursors.Hand;
        _btnHeaderNotify.Margin = new Padding(3, 1, 3, 0);
        _btnHeaderNotify.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        _btnHeaderNotify.Click += (s, e) => OpenNotificationCenter();

        var btnRefresh = new Button
        {
            Text = "🔄",
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = UITheme.TextSecondary,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            Width = 34,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 0, 0)
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (s, e) => RefreshCurrentPage();

        var btnAllActions = new Button
        {
            Text = "☰ Tüm İşlemler",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = UITheme.PrimaryLight,
            ForeColor = UITheme.Primary,
            FlatStyle = FlatStyle.Flat,
            Height = 32,
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 0)
        };
        btnAllActions.FlatAppearance.BorderSize = 0;
        btnAllActions.Click += (s, e) =>
        {
            var menu = _ribbon.BuildMenu();
            menu.Show(btnAllActions, new Point(0, btnAllActions.Height));
        };

        pnlActions.Controls.Add(pnlSearchContainer);
        pnlActions.Controls.Add(btnAllActions);
        pnlActions.Controls.Add(btnSale);
        pnlActions.Controls.Add(btnBuy);
        pnlActions.Controls.Add(_btnHeaderNotify);
        pnlActions.Controls.Add(btnRefresh);

        _pnlTopHeader.Controls.Add(pnlTitle);
        _pnlTopHeader.Controls.Add(pnlActions);
    }

    private void BuildGlobalSearchPopup()
    {
        _pnlGlobalResults.Size = new Size(440, 260);
        _pnlGlobalResults.Location = new Point(590, 60);
        _pnlGlobalResults.BackColor = Color.White;
        _pnlGlobalResults.BorderStyle = BorderStyle.FixedSingle;
        _pnlGlobalResults.Visible = false;

        _lstGlobalResults.Dock = DockStyle.Fill;
        _lstGlobalResults.BorderStyle = BorderStyle.None;
        _lstGlobalResults.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        _lstGlobalResults.ItemHeight = 28;
        _lstGlobalResults.DoubleClick += (s, e) => ExecuteGlobalSearchResult();
        _lstGlobalResults.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteGlobalSearchResult();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _pnlGlobalResults.Visible = false;
            }
        };

        _pnlGlobalResults.Controls.Add(_lstGlobalResults);
    }

    private void OnGlobalSearchTextChanged()
    {
        string q = _txtGlobalSearch.Text.Trim();
        if (q.Length < 2)
        {
            _pnlGlobalResults.Visible = false;
            return;
        }

        _lstGlobalResults.Items.Clear();

        try
        {
            // 1. Ürünlerde ara
            var dtProds = ProductService.GetAllProducts(q);
            int prodCount = 0;
            foreach (DataRow r in dtProds.Rows)
            {
                if (prodCount++ >= 4) break;
                string name = r["Ürün Adı"]?.ToString() ?? "";
                decimal stock = Convert.ToDecimal(r["Kalan Stok"] ?? 0);
                decimal price = Convert.ToDecimal(r["Satış Fiyatı"] ?? 0);
                _lstGlobalResults.Items.Add($"📦 ÜRÜN: {name} | Stok: {stock} | Fiyat: {price:N2} ₺");
            }

            // 2. Carilerde ara
            var dtAccs = AccountService.GetAllAccounts(q);
            int accCount = 0;
            foreach (DataRow r in dtAccs.Rows)
            {
                if (accCount++ >= 4) break;
                string title = r["Cari Adı"]?.ToString() ?? "";
                decimal balance = Convert.ToDecimal(r["Bakiye (₺)"] ?? 0);
                _lstGlobalResults.Items.Add($"👥 CARİ: {title} | Bakiye: {balance:N2} ₺");
            }

            if (_lstGlobalResults.Items.Count > 0)
            {
                _pnlGlobalResults.Visible = true;
                _pnlGlobalResults.BringToFront();
            }
            else
            {
                _pnlGlobalResults.Visible = false;
            }
        }
        catch
        {
            _pnlGlobalResults.Visible = false;
        }
    }

    private void ExecuteGlobalSearchResult()
    {
        if (_lstGlobalResults.SelectedItem == null) return;
        string selected = _lstGlobalResults.SelectedItem.ToString() ?? "";
        _pnlGlobalResults.Visible = false;
        _txtGlobalSearch.Clear();

        if (selected.StartsWith("📦 ÜRÜN:"))
        {
            string name = selected.Substring(8);
            int pipeIdx = name.IndexOf('|');
            if (pipeIdx > 0) name = name.Substring(0, pipeIdx).Trim();

            ShowPage(1); // Ürünler Sayfası
            _txtProductSearch.Text = name;
            _txtProductSearch.Focus();
            _txtProductSearch.SelectAll();
        }
        else if (selected.StartsWith("👥 CARİ:"))
        {
            string name = selected.Substring(8);
            int pipeIdx = name.IndexOf('|');
            if (pipeIdx > 0) name = name.Substring(0, pipeIdx).Trim();

            ShowPage(2); // Cari Sayfası
            _txtAccountSearch.Text = name;
            _txtAccountSearch.Focus();
            _txtAccountSearch.SelectAll();
        }
    }

    private void OpenCommandPalette()
    {
        // Menüdeki tüm komutlar (aynı komut birden çok sekmede olsa da tek sefer)
        var seen = new HashSet<string>();
        var commands = new List<PaletteItem>();
        foreach (var tab in _ribbon.RibbonTabs)
        foreach (var group in tab.Groups)
        foreach (var btn in group.Items.SelectMany(t => t.Items).Where(b => b.Visible && b.Enabled))
        {
            if (!seen.Add(btn.TextLine1)) continue;
            string text = btn.TextLine1.Trim();
            string icon = "▸";
            int sp = text.IndexOf(' ');
            if (sp > 0 && !char.IsLetterOrDigit(text[0])) { icon = text[..sp]; text = text[(sp + 1)..].Trim(); }
            var captured = btn;
            commands.Add(new PaletteItem
            {
                Icon = icon,
                Title = text,
                Subtitle = string.IsNullOrWhiteSpace(btn.TextLine2) ? tab.Text : $"{btn.TextLine2}  ·  {tab.Text}",
                Run = () => captured.Invoke()
            });
        }

        IEnumerable<PaletteItem> DataSearch(string q)
        {
            foreach (DataRow r in ProductService.GetAllProducts(q).Rows.Cast<DataRow>().Take(5))
            {
                string name = r["Ürün Adı"]?.ToString() ?? "";
                decimal stock = Convert.ToDecimal(r["Kalan Stok"] ?? 0);
                decimal price = Convert.ToDecimal(r["Satış Fiyatı"] ?? 0);
                yield return new PaletteItem
                {
                    Icon = "📦",
                    Title = name,
                    Subtitle = $"Ürün  ·  Stok: {stock}  ·  Fiyat: {price:N2} ₺",
                    Run = () => { ShowPage(1); _txtProductSearch.Text = name; _txtProductSearch.Focus(); _txtProductSearch.SelectAll(); }
                };
            }
            foreach (DataRow r in AccountService.GetAllAccounts(q).Rows.Cast<DataRow>().Take(5))
            {
                string title = r["Cari Adı"]?.ToString() ?? "";
                decimal balance = Convert.ToDecimal(r["Bakiye (₺)"] ?? 0);
                yield return new PaletteItem
                {
                    Icon = "👥",
                    Title = title,
                    Subtitle = $"Cari  ·  Bakiye: {balance:N2} ₺",
                    Run = () => { ShowPage(2); _txtAccountSearch.Text = title; _txtAccountSearch.Focus(); _txtAccountSearch.SelectAll(); }
                };
            }
        }

        using var dlg = new CommandPaletteDialog(commands, DataSearch);
        dlg.PositionOver(this);
        // Komut, palet kapandıktan sonra çalışır (iç içe modal pencere oluşmasın)
        if (dlg.ShowDialog(this) == DialogResult.OK) dlg.Selected?.Run?.Invoke();
    }

    private void RefreshCurrentPage()
    {
        switch (_currentNavIndex)
        {
            case 0: RefreshDashboard(); break;
            case 1: RefreshProducts(); break;
            case 2: RefreshAccounts(); break;
            case 3: RefreshStockMovements(); break;
            case 4: RefreshAccountMovements(); RefreshInvoices(); break;
            case 5: RefreshAuditLogs(); break;
            case 6: RefreshUsers(); break;
            case 7: RefreshLicensePage(); break;
        }
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
        _statusUser.Text = $"Kullanıcı: {curUser?.FullName ?? "Admin"} ({curUser?.Role ?? "Yönetici"})";
        _statusUser.ForeColor = Color.FromArgb(226, 232, 240);
        _statusUser.Margin = new Padding(8, 0, 16, 0);

        var lic = LicenseService.CurrentLicense;
        _statusLicense.Text = $"Lisans: {lic.DaysRemaining} Gün Kaldı ({lic.LicensedTo})";
        _statusLicense.ForeColor = lic.DaysRemaining < 30 ? Color.FromArgb(248, 113, 113) : Color.FromArgb(52, 211, 153);
        _statusLicense.Margin = new Padding(0, 0, 16, 0);

        _statusDb.Text = "SQL Server: Bağlı";
        _statusDb.ForeColor = Color.FromArgb(56, 189, 248);
        _statusDb.Margin = new Padding(0, 0, 16, 0);

        _statusClock.Text = $"Saat: {DateTime.Now:HH:mm:ss}";
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

        _ribbon.RibbonTabs.Clear();

        var curUser = UserService.CurrentUser;

        // ==========================================
        // SEKME 1: 📊 GENEL BAKIŞ (DASHBOARD)
        // ==========================================
        var tabDash = new CmdTab { Text = "📊 Genel Bakış" };

        var grpDashMain = new CmdGroup { TextLine1 = "Ana Kontrol" };
        var tripDashMain = new CmdTriple();

        var btnDash = new CmdButton 
        { 
            TextLine1 = "📊 Kontrol Paneli", 
            TextLine2 = "Özet Pano",
            ImageLarge = RibbonIconFactory.CreateIcon("dashboard", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("dashboard", 16)
        };
        btnDash.Click += (s, e) => ShowPage(0);

        var btnZReportDash = new CmdButton 
        { 
            TextLine1 = "🔒 Gün Sonu (Z)", 
            TextLine2 = "Kasa Raporu",
            ImageLarge = RibbonIconFactory.CreateIcon("zreport", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("zreport", 16)
        };
        btnZReportDash.Click += (s, e) => OpenDailyCashClosing();

        var btnNotifDash = new CmdButton 
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
        var grpDashLayout = new CmdGroup { TextLine1 = "👁️ Ekran Düzeni" };
        var tripLayout1 = new CmdTriple();

        var btnLayoutStd = new CmdButton 
        { 
            TextLine1 = "🌐 Standart Pano", 
            TextLine2 = "Tüm Bölümler",
            ImageLarge = RibbonIconFactory.CreateIcon("layout", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("layout", 16)
        };
        btnLayoutStd.Click += (s, e) => SetDashboardLayout(0);

        var btnLayoutDebtors = new CmdButton 
        { 
            TextLine1 = "💰 Borçlular", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("accounts", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("accounts", 16)
        };
        btnLayoutDebtors.Click += (s, e) => SetDashboardLayout(2);

        tripLayout1.Items.Add(btnLayoutStd);
        tripLayout1.Items.Add(btnLayoutDebtors);
        grpDashLayout.Items.Add(tripLayout1);

        var tripLayout2 = new CmdTriple();

        var btnLayoutRecent = new CmdButton 
        { 
            TextLine1 = "🕒 Son İşlemler", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnLayoutRecent.Click += (s, e) => SetDashboardLayout(1);

        var btnLayoutSearch = new CmdButton 
        { 
            TextLine1 = "🔍 Hızlı Arama", 
            TextLine2 = "Tam Odak",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnLayoutSearch.Click += (s, e) => SetDashboardLayout(3);

        var btnRefreshDash = new CmdButton 
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
        var grpDashDue = new CmdGroup { TextLine1 = "Risk & Entegrasyon" };
        var tripDashDue = new CmdTriple();

        var btnOverdueDash = new CmdButton 
        { 
            TextLine1 = "⚠️ Vadesi Dolanlar", 
            TextLine2 = "Geciken Borçlar",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnOverdueDash.Click += (s, e) => OpenNotificationCenter(1);

        var btnMobDash = new CmdButton 
        { 
            TextLine1 = "📱 Canlı Barkod", 
            TextLine2 = "Kamera Okuyucu",
            ImageLarge = RibbonIconFactory.CreateIcon("mobilescanner", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("mobilescanner", 16)
        };
        btnMobDash.Click += (s, e) => OpenMobileScanner();

        tripDashDue.Items.Add(btnOverdueDash);
        tripDashDue.Items.Add(btnMobDash);
        grpDashDue.Items.Add(tripDashDue);

        var tripDashDue2 = new CmdTriple();

        var btnWhatsAppDash = new CmdButton 
        { 
            TextLine1 = "💬 WhatsApp Cep", 
            TextLine2 = "Mobil Portal & Bot",
            ImageLarge = RibbonIconFactory.CreateIcon("whatsapp", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("whatsapp", 16)
        };
        btnWhatsAppDash.Click += (s, e) => OpenWhatsAppAssistantDialog();

        var btnBatchInvDash = new CmdButton 
        { 
            TextLine1 = "📂 Toplu Fatura", 
            TextLine2 = "Çoklu PDF Aktar",
            ImageLarge = RibbonIconFactory.CreateIcon("batchinvoices", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("batchinvoices", 16)
        };
        btnBatchInvDash.Click += (s, e) => OpenBatchInvoiceImportDialog();

        tripDashDue2.Items.Add(btnWhatsAppDash);
        tripDashDue2.Items.Add(btnBatchInvDash);
        grpDashDue.Items.Add(tripDashDue2);

        tabDash.Groups.Add(grpDashDue);

        _ribbon.RibbonTabs.Add(tabDash);

        // ==========================================
        // SEKME 2: ⚡ HIZLI SATIŞ & POS
        // ==========================================
        var tabSales = new CmdTab { Text = "⚡ Hızlı Satış & POS" };

        var grpSale = new CmdGroup { TextLine1 = "Satış & Fiş" };
        var tripSale = new CmdTriple();

        var btnSale = new CmdButton 
        { 
            TextLine1 = "⚡ Hızlı Satış", 
            TextLine2 = "(F3 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnSale.Click += (s, e) => OpenQuickSale("Satış");

        var btnBuy = new CmdButton 
        { 
            TextLine1 = "📥 Hızlı Alış", 
            TextLine2 = "(F4 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("quickbuy", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quickbuy", 16)
        };
        btnBuy.Click += (s, e) => OpenQuickSale("Alış");

        var btnParked = new CmdButton 
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

        var btnReturn = new CmdButton
        {
            TextLine1 = "↩️ Satış İadesi & Değişim",
            TextLine2 = "(F8 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("quickbuy", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quickbuy", 16)
        };
        btnReturn.Click += (s, e) => OpenSaleReturn();

        tripSale.Items.Add(btnSale);
        tripSale.Items.Add(btnBuy);
        tripSale.Items.Add(btnParked);
        grpSale.Items.Add(tripSale);
        var tripSale2 = new CmdTriple();
        tripSale2.Items.Add(btnReturn);
        grpSale.Items.Add(tripSale2);
        tabSales.Groups.Add(grpSale);

        var grpCash = new CmdGroup { TextLine1 = "Tahsilat & Kasa" };
        var tripCash = new CmdTriple();

        var btnDebt = new CmdButton 
        { 
            TextLine1 = "➕ Borç Ekle", 
            TextLine2 = "(F5 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("debt", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("debt", 16)
        };
        btnDebt.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Satış"); };

        var btnCollect = new CmdButton 
        { 
            TextLine1 = "💵 Tahsilat Yap", 
            TextLine2 = "(F6 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("collect", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("collect", 16)
        };
        btnCollect.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Tahsilat"); };

        var btnCloseDay = new CmdButton 
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

        var grpHardware = new CmdGroup { TextLine1 = "POS Donanım" };
        var tripHardware = new CmdTriple();

        var btnCustomerDisplay = new CmdButton 
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

        var btnFastIbanQr = new CmdButton 
        { 
            TextLine1 = "⚡ FAST / Karekod", 
            TextLine2 = "IBAN Tanımla",
            ImageLarge = RibbonIconFactory.CreateIcon("quicksale", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("quicksale", 16)
        };
        btnFastIbanQr.Click += (s, e) =>
        {
            using var dlg = new FastQrPaymentDialog(100, "TEST-KAREKOD");
            dlg.ShowDialog(this);
        };

        tripHardware.Items.Add(btnCustomerDisplay);
        tripHardware.Items.Add(btnFastIbanQr);
        grpHardware.Items.Add(tripHardware);
        tabSales.Groups.Add(grpHardware);

        _ribbon.RibbonTabs.Add(tabSales);

        // ==========================================
        // SEKME 3: 📦 ÜRÜNLER & STOK
        // ==========================================
        var tabStock = new CmdTab { Text = "📦 Ürünler & Stok" };

        var grpProduct = new CmdGroup { TextLine1 = "Ürün Kartları" };
        var tripProd = new CmdTriple();

        var btnProdList = new CmdButton 
        { 
            TextLine1 = "📦 Ürün Listesi", 
            TextLine2 = "Tüm Stok",
            ImageLarge = RibbonIconFactory.CreateIcon("products", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("products", 16)
        };
        btnProdList.Click += (s, e) => ShowPage(1);

        var btnNewProd = new CmdButton 
        { 
            TextLine1 = "➕ Yeni Ürün", 
            TextLine2 = "Kart Tanımla",
            ImageLarge = RibbonIconFactory.CreateIcon("newproduct", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("newproduct", 16)
        };
        btnNewProd.Click += (s, e) => AddProduct();

        var btnBarcode = new CmdButton 
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

        var grpFast = new CmdGroup { TextLine1 = "Seri Giriş & Geçmiş" };
        var tripFast = new CmdTriple();

        var btnFastEntry = new CmdButton 
        { 
            TextLine1 = "⚡ Seri Ürün Girişi", 
            TextLine2 = "Manuel Ekleme",
            ImageLarge = RibbonIconFactory.CreateIcon("fastentry", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("fastentry", 16)
        };
        btnFastEntry.Click += (s, e) => OpenFastProductEntry();

        var btnHistoryRib = new CmdButton 
        { 
            TextLine1 = "📜 Fiyat & Stok", 
            TextLine2 = "Tarihçesi",
            ImageLarge = RibbonIconFactory.CreateIcon("pricehistory", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("pricehistory", 16)
        };
        btnHistoryRib.Click += (s, e) => { ShowPage(1); OpenSelectedProductPriceHistory(); };

        var btnBulkDel = new CmdButton 
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

        var grpWh = new CmdGroup { TextLine1 = "Depo & Envanter" };
        var tripWh = new CmdTriple();

        var btnWhManage = new CmdButton 
        { 
            TextLine1 = "🏢 Depo & Şube", 
            TextLine2 = "Yönetim",
            ImageLarge = RibbonIconFactory.CreateIcon("warehouse", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("warehouse", 16)
        };
        btnWhManage.Click += (s, e) => OpenWarehouseManage();

        var btnStockMov = new CmdButton 
        { 
            TextLine1 = "🔄 Stok Hareketleri", 
            TextLine2 = "Giriş/Çıkış",
            ImageLarge = RibbonIconFactory.CreateIcon("stockmovement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("stockmovement", 16)
        };
        btnStockMov.Click += (s, e) => ShowPage(3);

        var btnCount = new CmdButton 
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

        // --- 4. AKILLI FİYATLANDIRMA & STOK ANALİZİ GRUBU ---
        var grpSmartPricing = new CmdGroup { TextLine1 = "Akıllı Fiyat & Analiz" };
        var tripSmartPrice = new CmdTriple();

        var btnBulkPrice = new CmdButton 
        { 
            TextLine1 = "🏷️ Toplu Fiyat", 
            TextLine2 = "% Zam & İndirim",
            ImageLarge = RibbonIconFactory.CreateIcon("barcode", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("barcode", 16)
        };
        btnBulkPrice.Click += (s, e) =>
        {
            if (!Require(UserPermissions.ProductsBulkPrice, "Toplu fiyat güncelleme")) return;
            using var dlg = new BulkPriceUpdateDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RefreshProducts();
            }
        };

        var btnExpiryAlerts = new CmdButton 
        { 
            TextLine1 = "⚠️ SKT & Kritik", 
            TextLine2 = "Stok Alarmları",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnExpiryAlerts.Click += (s, e) =>
        {
            using var dlg = new ExpiryAndStockAlertsDialog();
            dlg.ShowDialog(this);
        };

        var btnDeadStock = new CmdButton 
        { 
            TextLine1 = "📦 Ölü Stok", 
            TextLine2 = "Durgun Sermaye",
            ImageLarge = RibbonIconFactory.CreateIcon("stockcount", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("stockcount", 16)
        };
        btnDeadStock.Click += (s, e) =>
        {
            using var dlg = new DeadStockReportDialog();
            dlg.ShowDialog(this);
        };

        var btnSoldProducts = new CmdButton
        {
            TextLine1 = "📈 Satılan Ürünler",
            TextLine2 = "Adet, Tutar, Stok",
            ImageLarge = RibbonIconFactory.CreateIcon("stockcount", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("stockcount", 16)
        };
        btnSoldProducts.Click += (s, e) =>
        {
            using var dlg = new SoldProductsReportDialog();
            dlg.ShowDialog(this);
        };

        tripSmartPrice.Items.Add(btnBulkPrice);
        tripSmartPrice.Items.Add(btnExpiryAlerts);
        tripSmartPrice.Items.Add(btnDeadStock);
        grpSmartPricing.Items.Add(tripSmartPrice);
        var tripSalesReport = new CmdTriple();
        tripSalesReport.Items!.Add(btnSoldProducts);
        grpSmartPricing.Items.Add(tripSalesReport);
        tabStock.Groups.Add(grpSmartPricing);

        _ribbon.RibbonTabs.Add(tabStock);

        // ==========================================
        // SEKME 4: 👥 CARİ & VERESİYE
        // ==========================================
        var tabAccounts = new CmdTab { Text = "👥 Cari & Veresiye" };

        var grpAcc = new CmdGroup { TextLine1 = "Cari Hesaplar" };
        var tripAcc = new CmdTriple();

        var btnAccList = new CmdButton 
        { 
            TextLine1 = "👥 Cari Listesi", 
            TextLine2 = "Müşteri & Bayi",
            ImageLarge = RibbonIconFactory.CreateIcon("accounts", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("accounts", 16)
        };
        btnAccList.Click += (s, e) => ShowPage(2);

        var btnNewAcc = new CmdButton 
        { 
            TextLine1 = "➕ Yeni Cari", 
            TextLine2 = "Müşteri Ekle",
            ImageLarge = RibbonIconFactory.CreateIcon("newaccount", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("newaccount", 16)
        };
        btnNewAcc.Click += (s, e) => AddAccount();

        var btnStmt = new CmdButton 
        { 
            TextLine1 = "📄 Hesap Ekstresi", 
            TextLine2 = "(F10 Kısayol)",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnStmt.Click += (s, e) => { ShowPage(2); ViewAccountStatement(); };

        var btnBulkStmt = new CmdButton
        {
            TextLine1 = "📨 Toplu Ekstre Gönder",
            TextLine2 = "WhatsApp / E-posta PDF",
            ImageLarge = RibbonIconFactory.CreateIcon("statement", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("statement", 16)
        };
        btnBulkStmt.Click += (s, e) => { using var dlg = new BulkStatementDialog(); dlg.ShowDialog(this); };

        tripAcc.Items.Add(btnAccList);
        tripAcc.Items.Add(btnNewAcc);
        tripAcc.Items.Add(btnStmt);
        grpAcc.Items.Add(tripAcc);
        var tripAcc2 = new CmdTriple();
        tripAcc2.Items.Add(btnBulkStmt);
        grpAcc.Items.Add(tripAcc2);
        tabAccounts.Groups.Add(grpAcc);

        var grpAccActions = new CmdGroup { TextLine1 = "Borç & Tahsilat" };
        var tripAccActions = new CmdTriple();

        var btnAccCollect = new CmdButton 
        { 
            TextLine1 = "💵 Tahsilat Al", 
            TextLine2 = "Ödeme Kaydet",
            ImageLarge = RibbonIconFactory.CreateIcon("collect", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("collect", 16)
        };
        btnAccCollect.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Tahsilat"); };

        var btnAccDebt = new CmdButton 
        { 
            TextLine1 = "➕ Borç Yaz", 
            TextLine2 = "Veresiye Ekle",
            ImageLarge = RibbonIconFactory.CreateIcon("debt", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("debt", 16)
        };
        btnAccDebt.Click += (s, e) => { ShowPage(2); AddAccountMovementForSelected("Satış"); };

        var btnAccBulkDel = new CmdButton 
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

        var grpRisk = new CmdGroup { TextLine1 = "Risk & Takip" };
        var tripRisk = new CmdTriple();

        var btnDue = new CmdButton 
        { 
            TextLine1 = "⚠️ Vadesi Dolanlar", 
            TextLine2 = "Geciken Borçlar",
            ImageLarge = RibbonIconFactory.CreateIcon("due", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("due", 16)
        };
        btnDue.Click += (s, e) => OpenNotificationCenter(1);

        var btnNotifyAcc = new CmdButton 
        { 
            TextLine1 = "🔔 SMS & Bildirim", 
            TextLine2 = "Otomatik Hatırlat",
            ImageLarge = RibbonIconFactory.CreateIcon("notification", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("notification", 16)
        };
        btnNotifyAcc.Click += (s, e) => OpenNotificationCenter();

        var btnAccStmtRisk = new CmdButton 
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
        var tabFin = new CmdTab { Text = "💳 Faturalar & Finans" };

        var grpInv = new CmdGroup { TextLine1 = "Faturalar" };
        var tripInv = new CmdTriple();

        var btnInvEntry = new CmdButton 
        { 
            TextLine1 = "📄 E-Fatura Girişi", 
            TextLine2 = "XML/PDF Aktar (F7)",
            ImageLarge = RibbonIconFactory.CreateIcon("invoiceentry", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("invoiceentry", 16)
        };
        btnInvEntry.Click += (s, e) => OpenInvoiceEntry();

        var btnInvList = new CmdButton 
        { 
            TextLine1 = "📋 Fatura Listesi", 
            TextLine2 = "Alış & Satış",
            ImageLarge = RibbonIconFactory.CreateIcon("invoices", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("invoices", 16)
        };
        btnInvList.Click += (s, e) => OpenInvoicesPage();

        var btnExcelInv = new CmdButton 
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

        var tripInv2 = new CmdTriple();
        var btnBatchInvRib = new CmdButton 
        { 
            TextLine1 = "📂 Toplu PDF Fatura", 
            TextLine2 = "Çoklu Yükle & Al",
            ImageLarge = RibbonIconFactory.CreateIcon("batchinvoices", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("batchinvoices", 16)
        };
        btnBatchInvRib.Click += (s, e) => OpenBatchInvoiceImportDialog();
        tripInv2.Items.Add(btnBatchInvRib);
        grpInv.Items.Add(tripInv2);

        tabFin.Groups.Add(grpInv);

        var grpCashFlow = new CmdGroup { TextLine1 = "Kasa & Nakit Akışı" };
        var tripCashFlow = new CmdTriple();

        var btnCashMov = new CmdButton 
        { 
            TextLine1 = "💳 Kasa Hareketleri", 
            TextLine2 = "Nakit Akışı & Kasa",
            ImageLarge = RibbonIconFactory.CreateIcon("cash", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("cash", 16)
        };
        btnCashMov.Click += (s, e) => ShowPage(4);

        var btnFinZ = new CmdButton 
        { 
            TextLine1 = "🔒 Gün Sonu (Z)", 
            TextLine2 = "Kasa Kapat & Rapor",
            ImageLarge = RibbonIconFactory.CreateIcon("zreport", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("zreport", 16)
        };
        btnFinZ.Click += (s, e) => OpenDailyCashClosing();

        var btnCashCollect = new CmdButton 
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

        // --- 3. MASRAFLAR, NET KÂR & DÖVİZ KURLARI GRUBU ---
        var grpExpensesAndRates = new CmdGroup { TextLine1 = "Giderler & Kurlar" };
        var tripExpensesAndRates = new CmdTriple();

        var btnExpenses = new CmdButton 
        { 
            TextLine1 = "📉 Dükkan Giderleri", 
            TextLine2 = "Net Kâr / Zarar",
            ImageLarge = RibbonIconFactory.CreateIcon("cash", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("cash", 16)
        };
        btnExpenses.Click += (s, e) =>
        {
            using var dlg = new ExpenseManageDialog();
            dlg.ShowDialog(this);
        };

        var btnCurrency = new CmdButton 
        { 
            TextLine1 = "💱 Canlı TCMB", 
            TextLine2 = "Döviz Kurları",
            ImageLarge = RibbonIconFactory.CreateIcon("refresh", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("refresh", 16)
        };
        btnCurrency.Click += (s, e) =>
        {
            using var dlg = new CurrencyRatesDialog();
            dlg.ShowDialog(this);
        };

        tripExpensesAndRates.Items.Add(btnExpenses);
        tripExpensesAndRates.Items.Add(btnCurrency);
        grpExpensesAndRates.Items.Add(tripExpensesAndRates);
        tabFin.Groups.Add(grpExpensesAndRates);

        _ribbon.RibbonTabs.Add(tabFin);

        // ==========================================
        // SEKME 6: 📱 ARAÇLAR & MOBİL
        // ==========================================
        var tabMob = new CmdTab { Text = "📱 Araçlar & Mobil" };

        var grpSmart = new CmdGroup { TextLine1 = "Akıllı Cihaz & Bot" };
        var tripSmart = new CmdTriple();

        var btnMobScan = new CmdButton 
        { 
            TextLine1 = "📱 Mobil Barkod", 
            TextLine2 = "Kamera Okuyucu",
            ImageLarge = RibbonIconFactory.CreateIcon("mobilescanner", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("mobilescanner", 16)
        };
        btnMobScan.Click += (s, e) => OpenMobileScanner();

        var btnNotifCenter = new CmdButton 
        { 
            TextLine1 = "🔔 SMS & Bildirim", 
            TextLine2 = "Mesaj Merkezi",
            ImageLarge = RibbonIconFactory.CreateIcon("notification", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("notification", 16)
        };
        btnNotifCenter.Click += (s, e) => OpenNotificationCenter();

        tripSmart.Items.Add(btnMobScan);
        tripSmart.Items.Add(btnNotifCenter);
        grpSmart.Items.Add(tripSmart);

        var tripSmart2 = new CmdTriple();

        var btnWhatsAppBot = new CmdButton 
        { 
            TextLine1 = "💬 WhatsApp Bot", 
            TextLine2 = "Online Mobil Portal",
            ImageLarge = RibbonIconFactory.CreateIcon("whatsapp", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("whatsapp", 16)
        };
        btnWhatsAppBot.Click += (s, e) => OpenWhatsAppAssistantDialog();

        var btnBatchInvMob = new CmdButton 
        { 
            TextLine1 = "📂 Toplu Fatura", 
            TextLine2 = "Çoklu PDF Aktar",
            ImageLarge = RibbonIconFactory.CreateIcon("batchinvoices", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("batchinvoices", 16)
        };
        btnBatchInvMob.Click += (s, e) => OpenBatchInvoiceImportDialog();

        tripSmart2.Items.Add(btnWhatsAppBot);
        tripSmart2.Items.Add(btnBatchInvMob);
        grpSmart.Items.Add(tripSmart2);

        tabMob.Groups.Add(grpSmart);

        _ribbon.RibbonTabs.Add(tabMob);

        // ==========================================
        // SEKME 7: ⚙️ SİSTEM & YÖNETİM
        // ==========================================
        var tabSys = new CmdTab { Text = "⚙️ Sistem & Yönetim" };

        var grpSys = new CmdGroup { TextLine1 = "Sistem & Güvenlik" };
        var tripSys = new CmdTriple();

        var btnUsers = new CmdButton 
        { 
            TextLine1 = "👤 Kullanıcılar", 
            TextLine2 = "Rol & Depo Yetki",
            ImageLarge = RibbonIconFactory.CreateIcon("users", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("users", 16)
        };
        btnUsers.Click += (s, e) => ShowPage(6);

        var btnLogs = new CmdButton 
        { 
            TextLine1 = "📜 İşlem Logları", 
            TextLine2 = "Denetim İzi & Audit",
            ImageLarge = RibbonIconFactory.CreateIcon("auditlogs", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("auditlogs", 16)
        };
        btnLogs.Click += (s, e) => ShowPage(5);

        var btnBackup = new CmdButton 
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

        var tripSys2 = new CmdTriple();
        var btnResetDbRibbon = new CmdButton 
        { 
            TextLine1 = "⚠️ DB Sıfırla", 
            TextLine2 = "Fabrika Ayarlarına Dön",
            ImageLarge = RibbonIconFactory.CreateIcon("danger", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("danger", 16)
        };
        btnResetDbRibbon.Click += (s, e) => ResetDatabaseAction();
        tripSys2.Items.Add(btnResetDbRibbon);
        grpSys.Items.Add(tripSys2);

        tabSys.Groups.Add(grpSys);

        var grpCloud = new CmdGroup { TextLine1 = "Bulut & Harici" };
        var tripCloud = new CmdTriple();

        var btnCloudBackup = new CmdButton 
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

        var grpConfig = new CmdGroup { TextLine1 = "Yapılandırma & Menü" };
        var tripConfig = new CmdTriple();

        var btnSettings = new CmdButton 
        { 
            TextLine1 = "⚙️ Genel Ayarlar", 
            TextLine2 = "Sistem & Donanım",
            ImageLarge = RibbonIconFactory.CreateIcon("settings", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("settings", 16)
        };
        btnSettings.Click += (s, e) => OpenSettings(0);

        var btnLicense = new CmdButton 
        { 
            TextLine1 = "🔑 Lisans Durumu", 
            TextLine2 = "Aktivasyon & Süre",
            ImageLarge = RibbonIconFactory.CreateIcon("license", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("license", 16)
        };
        btnLicense.Click += (s, e) => ShowPage(7);

        var btnTheme = new CmdButton 
        { 
            TextLine1 = "🎨 Tema Değiştir", 
            TextLine2 = "Görünüm Ayarları",
            ImageLarge = RibbonIconFactory.CreateIcon("theme", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("theme", 16)
        };
        btnTheme.Click += (s, e) => OpenSettings(4);

        tripConfig.Items.Add(btnSettings);
        tripConfig.Items.Add(btnLicense);
        tripConfig.Items.Add(btnTheme);
        grpConfig.Items.Add(tripConfig);

        var tripPuzzle = new CmdTriple();
        var btnDbMaint = new CmdButton
        {
            TextLine1 = "⚡ DB Bakımı",
            TextLine2 = "Shrink & Hızlandır",
            ImageLarge = RibbonIconFactory.CreateIcon("database", 32),
            ImageSmall = RibbonIconFactory.CreateIcon("database", 16)
        };
        btnDbMaint.Click += async (s, e) =>
        {
            var confirm = MessageBox.Show(
                "SQL Server veritabanı bakım ve optimizasyon işlemi başlatılacak:\n\n" +
                "1. Log (.ldf) dosyası küçültülecektir (Log Shrink)\n" +
                "2. Boş alanlar diske iade edilecektir (DB Shrink)\n" +
                "3. Tüm parçalanmış indeksler baştan inşa edilecektir (Rebuild Indexes)\n" +
                "4. Sorgu istatistikleri güncellenecektir (sp_updatestats)\n\n" +
                "Devam etmek istiyor musunuz?",
                "Veritabanı Bakım Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (confirm != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            try
            {
                var res = await DatabaseMaintenanceService.ExecuteMaintenanceAsync();
                MessageBox.Show(res.Message, res.Success ? "Optimizasyon Başarılı" : "Hata", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        };

        tripPuzzle.Items.Add(btnDbMaint);
        grpConfig.Items.Add(tripPuzzle);

        tabSys.Groups.Add(grpConfig);

        var grpLogout = new CmdGroup { TextLine1 = "Oturum" };
        var tripLogout = new CmdTriple();

        var btnLogout = new CmdButton 
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
            var btnKeygen = new CmdButton 
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

            // Modern SaaS Header ve Sidebar Rozetlerini Canlı Güncelle
            if (counts.TotalAlertCount > 0)
            {
                _btnHeaderNotify.Text = $"🔔 Bildirim ({counts.TotalAlertCount})";
                _btnHeaderNotify.BackColor = Color.FromArgb(254, 226, 226);
                _btnNavDash.BadgeText = counts.TotalAlertCount.ToString();
            }
            else
            {
                _btnHeaderNotify.Text = "🔔 Bildirim";
                _btnHeaderNotify.BackColor = Color.FromArgb(241, 245, 249);
                _btnNavDash.BadgeText = "";
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

        // Modern Sidebar Navigasyon Butonlarını ve Başlığı Güncelle
        foreach (var btn in _allNavButtons) btn.IsActive = false;
        switch (index)
        {
            case 0:
                _btnNavDash.IsActive = true;
                _lblPageTitle.Text = "📊 Genel Bakış (Dashboard)";
                _lblPageSubTitle.Text = "İşletmenizin anlık finansal ve operasyonel göstergeleri";
                break;
            case 1:
                _btnNavProducts.IsActive = true;
                _lblPageTitle.Text = "📦 Ürün & Stok Yönetimi";
                _lblPageSubTitle.Text = "Tüm ürün tanımları, barkodlar, birimler ve depo stokları";
                break;
            case 2:
                _btnNavAccounts.IsActive = true;
                _lblPageTitle.Text = "👥 Cari Hesaplar & Veresiye";
                _lblPageSubTitle.Text = "Müşteri ve tedarikçi bakiyeleri, veresiye limitleri ve risk durumu";
                break;
            case 3:
                _btnNavStockMov.IsActive = true;
                _lblPageTitle.Text = "🔄 Stok Hareketleri";
                _lblPageSubTitle.Text = "Giriş, çıkış, sayım ve transfer işlem geçmişi";
                break;
            case 4:
                _btnNavFinance.IsActive = true;
                _lblPageTitle.Text = "💳 Kasa, Finans & Faturalar";
                _lblPageSubTitle.Text = "Kasa hareketleri, tahsilat/ödemeler ve fatura kayıtları";
                break;
            case 5:
                _btnNavAudit.IsActive = true;
                _lblPageTitle.Text = "🕒 İşlem Geçmişi & Loglar";
                _lblPageSubTitle.Text = "Sistemdeki tüm kayıt, silme ve düzenleme hareketleri";
                break;
            case 6:
                _btnNavUsers.IsActive = true;
                _lblPageTitle.Text = "👤 Kullanıcı & Yetki Yönetimi";
                _lblPageSubTitle.Text = "Personel hesapları ve modül bazlı işlem izinleri";
                break;
            case 7:
                _btnNavLicense.IsActive = true;
                _lblPageTitle.Text = "🔑 Lisans Bilgileri";
                _lblPageSubTitle.Text = "Lisans anahtarı ve sistem kullanım geçerliliği";
                break;
            case 8:
                _btnNavSettings.IsActive = true;
                _lblPageTitle.Text = "⚙️ Sistem Ayarları & Yedek";
                _lblPageSubTitle.Text = "SQL bağlantısı, şirket parametreleri ve bulut yedekleme";
                break;
        }

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
            AutoScroll = false,
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
            ColumnCount = 1,
            Padding = new Padding(0, 10, 0, 10)
        };
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

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

        // Sağ: En Çok Borcu Olan Müşteriler Paneli
        var pnlDebtors = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        var lblDebtorTitle = new Label { Text = "💰 En Yüksek Bakiyeli Müşteriler (Veresiye Alacaklar)", Font = UITheme.TitleFont, ForeColor = UITheme.Primary, Dock = DockStyle.Top, Height = 30 };
        UITheme.ApplyGridStyle(_gridTopDebtors);
        pnlDebtors.Controls.Add(_gridTopDebtors);
        pnlDebtors.Controls.Add(lblDebtorTitle);
        lblDebtorTitle.SendToBack();
        _gridTopDebtors.BringToFront();
        splitTable.Controls.Add(pnlDebtors, 0, 0);

        // Son Hareketler Paneli
        var pnlRecent = new CardPanel { Dock = DockStyle.Top, Height = 250, Margin = new Padding(0, 10, 0, 15) };
        var lblRecentTitle = new Label { Text = "🕒 Son Gerçekleşen Finansal & Cari İşlemler", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        UITheme.ApplyGridStyle(_gridRecentTransactions);
        pnlRecent.Controls.Add(_gridRecentTransactions);
        pnlRecent.Controls.Add(lblRecentTitle);
        lblRecentTitle.SendToBack();
        _gridRecentTransactions.BringToFront();

        // Dashboard Hızlı Ürün Arama & Satış Kartı
        var pnlDashSearch = new CardPanel { Dock = DockStyle.Top, Height = 330, Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 0, 0, 15) };
        var pnlDashSearchTop = new Panel { Dock = DockStyle.Top, Height = 36 };
        var lblDashSearchTitle = new Label 
        { 
            Text = "🔍 Hızlı Ürün Arama & Anında Satış (Listeden ürüne tıklayın, satış ekranı açılır):", 
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
        // Listeden bir ürüne tek tıklamak satış ekranını o ürünle açar
        _gridDashProductSearch.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && _gridDashProductSearch.CurrentRow != null && _gridDashProductSearch.Columns.Contains("Id"))
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
                if (targetW >= 1150)
                {
                    _dashCardsFlow.WrapContents = false;
                    _dashCardsFlow.Height = 105;
                    int cardW = (targetW - 50) / cardCount;
                    if (_cardStockVal != null) _cardStockVal.Width = cardW;
                    if (_cardReceivable != null) _cardReceivable.Width = cardW;
                    if (_cardPayable != null) _cardPayable.Width = cardW;
                    if (_cardTodayCash != null) _cardTodayCash.Width = cardW;
                    if (_cardOverdue != null) _cardOverdue.Width = cardW;
                }
                else
                {
                    _dashCardsFlow.WrapContents = true;
                    _dashCardsFlow.Height = 215;
                    int cardW = (targetW - 40) / 3;
                    if (_cardStockVal != null) _cardStockVal.Width = cardW;
                    if (_cardReceivable != null) _cardReceivable.Width = cardW;
                    if (_cardPayable != null) _cardPayable.Width = cardW;
                    if (_cardTodayCash != null) _cardTodayCash.Width = (targetW - 30) / 2;
                    if (_cardOverdue != null) _cardOverdue.Width = (targetW - 30) / 2;
                }
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
        _dashSplitTable.Visible = _chkDashDebtors.Checked;

        if (_chkDashCrit.Checked && !_chkDashDebtors.Checked)
        {
        }
        else if (!_chkDashCrit.Checked && _chkDashDebtors.Checked)
        {
        }
        else
        {
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

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Filtreler (Dock Left)
        var rowFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtProductSearch.Width = 190;
        _txtProductSearch.Height = 32;
        _txtProductSearch.PlaceholderText = "🔍 Ürün Ara (Ad, Kod, Barkod)...";
        _txtProductSearch.Font = UITheme.RegularFont;
        _txtProductSearch.TextChanged += (s, e) => RefreshProducts();

        _cmbProductCategory.Width = 115;
        _cmbProductCategory.Height = 32;
        _cmbProductCategory.Font = UITheme.RegularFont;
        _cmbProductCategory.SelectedIndexChanged += ProductCategoryChanged;

        _cmbProductWarehouse.Width = 115;
        _cmbProductWarehouse.Height = 32;
        _cmbProductWarehouse.Font = UITheme.RegularFont;
        _cmbProductWarehouse.SelectedIndexChanged += (s, e) => RefreshProducts();

        _chkOnlyCritical.Text = "⚠️ Kritik";
        _chkOnlyCritical.Font = UITheme.RegularFont;
        _chkOnlyCritical.AutoSize = true;
        _chkOnlyCritical.Margin = new Padding(6, 6, 6, 0);
        _chkOnlyCritical.CheckedChanged += (s, e) => RefreshProducts();

        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshProducts(), 36, 32);

        rowFilters.Controls.Add(_txtProductSearch);
        rowFilters.Controls.Add(_cmbProductCategory);
        rowFilters.Controls.Add(_cmbProductWarehouse);
        rowFilters.Controls.Add(_chkOnlyCritical);
        rowFilters.Controls.Add(btnRefresh);

        // Sağ: Kurumsal Aksiyon Butonları (Dock Right)
        var rowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnAdd = UITheme.CreateButton("➕ Yeni Ürün", UITheme.Primary, Color.White, (s, e) => AddProduct(), 110, 32);
        var btnFastEntry = UITheme.CreateButton("⚡ Hızlı Giriş", Color.FromArgb(16, 185, 129), Color.White, (s, e) => OpenFastProductEntry(), 105, 32);

        // Diğer Tüm İşlemler İçin Kurumsal Dropdown Menü
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

        var btnTools = UITheme.CreateButton("⚙️ İşlemler ▾", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) =>
        {
            if (s is Control btn)
            {
                mnuTools.Show(btn, new Point(0, btn.Height));
            }
        }, 110, 32);

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

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Filtreler (Dock Left)
        var pnlLeftFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtAccountSearch.Width = 190;
        _txtAccountSearch.Height = 32;
        _txtAccountSearch.PlaceholderText = "🔍 Cari Ara (F2)...";
        _txtAccountSearch.Font = UITheme.RegularFont;
        _txtAccountSearch.TextChanged += (s, e) => RefreshAccounts();

        _cmbAccountType.Width = 110;
        _cmbAccountType.Height = 32;
        _cmbAccountType.Font = UITheme.RegularFont;
        _cmbAccountType.Items.Clear();
        _cmbAccountType.Items.AddRange(new object[] { "Tüm Cariler", "Müşteri", "Tedarikçi" });
        _cmbAccountType.SelectedIndex = 0;
        _cmbAccountType.SelectedIndexChanged += (s, e) => RefreshAccounts();

        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAccounts(), 36, 32);

        pnlLeftFilters.Controls.Add(_txtAccountSearch);
        pnlLeftFilters.Controls.Add(_cmbAccountType);
        pnlLeftFilters.Controls.Add(btnRefresh);

        // Sağ: Ana Aksiyon Butonları & Dropdown (Dock Right)
        var pnlRightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnAdd = UITheme.CreateButton("➕ Yeni Cari", UITheme.Primary, Color.White, (s, e) => AddAccount(), 105, 32);
        var btnPayment = UITheme.CreateButton("🟢 Tahsilat (F6)", Color.FromArgb(16, 185, 129), Color.White, (s, e) => AddAccountMovementForSelected("Tahsilat"), 110, 32);
        var btnDebt = UITheme.CreateButton("🔴 Borç Yaz (F5)", Color.FromArgb(220, 38, 38), Color.White, (s, e) => AddAccountMovementForSelected("Satış"), 110, 32);
        var btnStatement = UITheme.CreateButton("📑 Ekstre (F10)", UITheme.Info, Color.White, (s, e) => ViewAccountStatement(), 105, 32);

        // Diğer İşlemler Dropdown Menüsü
        var mnuAccMore = new ContextMenuStrip();
        mnuAccMore.Items.Add("✏️ Cari Kartı Düzenle", null, (s, e) => EditAccount());
        mnuAccMore.Items.Add("📲 WhatsApp ile Bakiye Bildir", null, (s, e) => SendWhatsAppForSelected());
        mnuAccMore.Items.Add("📅 Vade & Borç Takibi", null, (s, e) => { using var dlg = new DueReceivablesDialog(); dlg.ShowDialog(); RefreshAll(); });
        mnuAccMore.Items.Add(new ToolStripSeparator());
        mnuAccMore.Items.Add("📥 Excel'den Cari Yükle", null, (s, e) => OpenExcelImport("Cari"));
        mnuAccMore.Items.Add("📊 Excel Listesi Olarak İndir", null, ExportAccountsToExcel);
        mnuAccMore.Items.Add(new ToolStripSeparator());
        mnuAccMore.Items.Add("🗑️ Seçili Cariyi Sil", null, (s, e) => DeleteAccount());
        mnuAccMore.Items.Add("🗑️ Çoklu Sil (Seçilenleri)", null, (s, e) => DeleteAccountsBulkAction());

        var btnMore = UITheme.CreateButton("⚡ İşlemler ▼", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) =>
        {
            if (s is Button b) mnuAccMore.Show(b, new Point(0, b.Height));
        }, 105, 32);

        pnlRightActions.Controls.Add(btnAdd);
        pnlRightActions.Controls.Add(btnPayment);
        pnlRightActions.Controls.Add(btnDebt);
        pnlRightActions.Controls.Add(btnStatement);
        pnlRightActions.Controls.Add(btnMore);

        toolbar.Controls.Add(pnlLeftFilters);
        toolbar.Controls.Add(pnlRightActions);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(0) };
        UITheme.ApplyGridStyle(_gridAccounts);
        _gridAccounts.MultiSelect = true;
        _gridAccounts.CellDoubleClick += (s, e) => ViewAccountStatement();
        _gridAccounts.CellFormatting += GridAccounts_CellFormatting;
        _gridAccounts.ContextMenuStrip = mnuAccMore;

        gridContainer.Controls.Add(_gridAccounts);

        _pnlAccounts.Controls.Add(gridContainer);
        _pnlAccounts.Controls.Add(toolbar);
    }
    #endregion

    #region 4. Stock Movements Page
    private void BuildStockMovementsPage()
    {
        _pnlStockMovements = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("Stok Giriş & Çıkış Hareketleri", "Tarih bazlı ürün girişleri, satış çıkışları, iadeler ve zayiler");

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Filtreler (Dock Left)
        var pnlLeftFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtStockSearch.Width = 175;
        _txtStockSearch.Height = 32;
        _txtStockSearch.PlaceholderText = "🔍 Hareket Ara...";
        _txtStockSearch.Font = UITheme.RegularFont;
        _txtStockSearch.TextChanged += (s, e) => RefreshStockMovements();

        _cmbStockType.Width = 110;
        _cmbStockType.Height = 32;
        _cmbStockType.Font = UITheme.RegularFont;
        _cmbStockType.Items.Clear();
        _cmbStockType.Items.AddRange(new object[] { "Tüm Türler", "Gelen", "Satılan", "İade Giriş", "Fire / Zayi", "Transfer Giriş", "Transfer Çıkış" });
        _cmbStockType.SelectedIndex = 0;
        _cmbStockType.SelectedIndexChanged += (s, e) => RefreshStockMovements();

        _cmbStockWarehouse.Width = 120;
        _cmbStockWarehouse.Height = 32;
        _cmbStockWarehouse.Font = UITheme.RegularFont;
        _cmbStockWarehouse.SelectedIndexChanged += (s, e) => RefreshStockMovements();

        _dtpStockStart.Width = 95;
        _dtpStockStart.Height = 32;
        _dtpStockStart.Font = UITheme.RegularFont;
        _dtpStockStart.ValueChanged += (s, e) => RefreshStockMovements();

        _dtpStockEnd.Width = 95;
        _dtpStockEnd.Height = 32;
        _dtpStockEnd.Font = UITheme.RegularFont;
        _dtpStockEnd.ValueChanged += (s, e) => RefreshStockMovements();

        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshStockMovements(), 36, 32);

        pnlLeftFilters.Controls.Add(_txtStockSearch);
        pnlLeftFilters.Controls.Add(_cmbStockType);
        pnlLeftFilters.Controls.Add(_cmbStockWarehouse);
        pnlLeftFilters.Controls.Add(_dtpStockStart);
        pnlLeftFilters.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 6, 0, 0) });
        pnlLeftFilters.Controls.Add(_dtpStockEnd);
        pnlLeftFilters.Controls.Add(btnRefresh);

        // Sağ: Aksiyon Butonları & Dropdown Menü (Dock Right)
        var pnlRightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnAdd = UITheme.CreateButton("➕ Stok Hareketi", UITheme.Primary, Color.White, (s, e) => AddStock(), 125, 32);
        var btnTransfer = UITheme.CreateButton("🔄 Depo Transferi", Color.FromArgb(13, 148, 136), Color.White, (s, e) => OpenWarehouseTransfer(), 125, 32);

        // İkincil İşlemler İçin Dropdown Menü
        var mnuStockTools = new ContextMenuStrip();
        mnuStockTools.Items.Add("🏢 Depolar & Şubeler Yönetimi", null, (s, e) => OpenWarehouseManage());
        mnuStockTools.Items.Add("📋 Hızlı Stok Sayımı & Eşitleme", null, (s, e) => OpenStockCount());
        mnuStockTools.Items.Add("📊 Excel'e Aktar", null, ExportStockToExcel);
        mnuStockTools.Items.Add(new ToolStripSeparator());
        mnuStockTools.Items.Add("🗑️ Seçili Hareketi Sil", null, (s, e) => DeleteStock());
        mnuStockTools.Items.Add("🗑️ Çoklu Hareket Sil (Seçilenler)", null, (s, e) => DeleteStockMovementsBulkAction());

        var btnTools = UITheme.CreateButton("⚙️ İşlemler ▾", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) =>
        {
            if (s is Control btn)
            {
                mnuStockTools.Show(btn, new Point(0, btn.Height));
            }
        }, 105, 32);

        pnlRightActions.Controls.Add(btnAdd);
        pnlRightActions.Controls.Add(btnTransfer);
        pnlRightActions.Controls.Add(btnTools);

        toolbar.Controls.Add(pnlLeftFilters);
        toolbar.Controls.Add(pnlRightActions);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(0) };
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

        _tabsFinance.Dock = DockStyle.Fill;
        _tabsFinance.Font = UITheme.TitleFont;

        // ==================== SEKME 1: KASA & FİNANSAL HAREKETLER ====================
        var tabAccMov = new TabPage("💵 Kasa & Nakit Hareketleri") { BackColor = Color.FromArgb(248, 250, 252) };
        var toolbarAcc = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Filtreler (Dock Left)
        var pnlLeftFiltersAcc = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtAccMovSearch.Width = 175;
        _txtAccMovSearch.Height = 32;
        _txtAccMovSearch.PlaceholderText = "🔍 İşlem Ara...";
        _txtAccMovSearch.Font = UITheme.RegularFont;
        _txtAccMovSearch.TextChanged += (s, e) => RefreshAccountMovements();

        _cmbAccMovType.Width = 110;
        _cmbAccMovType.Height = 32;
        _cmbAccMovType.Font = UITheme.RegularFont;
        _cmbAccMovType.Items.Clear();
        _cmbAccMovType.Items.AddRange(new object[] { "Tüm İşlemler", "Tahsilat", "Ödeme", "Satış", "Alış" });
        _cmbAccMovType.SelectedIndex = 0;
        _cmbAccMovType.SelectedIndexChanged += (s, e) => RefreshAccountMovements();

        _dtpAccStart.Width = 95;
        _dtpAccStart.Height = 32;
        _dtpAccStart.Font = UITheme.RegularFont;
        _dtpAccStart.ValueChanged += (s, e) => RefreshAccountMovements();

        _dtpAccEnd.Width = 95;
        _dtpAccEnd.Height = 32;
        _dtpAccEnd.Font = UITheme.RegularFont;
        _dtpAccEnd.ValueChanged += (s, e) => RefreshAccountMovements();

        var btnRefreshAcc = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAccountMovements(), 36, 32);

        pnlLeftFiltersAcc.Controls.Add(_txtAccMovSearch);
        pnlLeftFiltersAcc.Controls.Add(_cmbAccMovType);
        pnlLeftFiltersAcc.Controls.Add(_dtpAccStart);
        pnlLeftFiltersAcc.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 6, 0, 0) });
        pnlLeftFiltersAcc.Controls.Add(_dtpAccEnd);
        pnlLeftFiltersAcc.Controls.Add(btnRefreshAcc);

        // Sağ: Aksiyon Butonları (Dock Right)
        var pnlRightActionsAcc = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnAddAcc = UITheme.CreateButton("➕ Nakit / Kasa İşlemi", UITheme.Primary, Color.White, (s, e) => AddAccountMovement(), 150, 32);
        var btnExcelExpAcc = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportAccMovToExcel, 115, 32);
        var btnDeleteAcc = UITheme.CreateButton("🗑️ Sil", UITheme.Danger, Color.White, (s, e) => DeleteAccountMovement(), 75, 32);

        pnlRightActionsAcc.Controls.Add(btnAddAcc);
        pnlRightActionsAcc.Controls.Add(btnExcelExpAcc);
        pnlRightActionsAcc.Controls.Add(btnDeleteAcc);

        toolbarAcc.Controls.Add(pnlLeftFiltersAcc);
        toolbarAcc.Controls.Add(pnlRightActionsAcc);

        var gridContainerAcc = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(0) };
        UITheme.ApplyGridStyle(_gridAccMov);
        gridContainerAcc.Controls.Add(_gridAccMov);

        tabAccMov.Controls.Add(gridContainerAcc);
        tabAccMov.Controls.Add(toolbarAcc);

        // ==================== SEKME 2: FATURA & KISMİ ÖDEME TAKİBİ ====================
        var tabInvoices = new TabPage("🧾 Fatura & Kısmi Ödeme Takibi") { BackColor = Color.FromArgb(248, 250, 252) };
        var toolbarInv = new CardPanel { Dock = DockStyle.Top, Height = 84, Padding = new Padding(10, 6, 10, 6) };

        // 1. SATIR: Hızlı Arama & Tür / Durum / Tarih Filtreleri
        var rowFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };

        _txtInvSearch.Width = 160;
        _txtInvSearch.Height = 28;
        _txtInvSearch.PlaceholderText = "🔍 Fatura No / Cari...";
        _txtInvSearch.Font = UITheme.RegularFont;
        _txtInvSearch.TextChanged += (s, e) => RefreshInvoices();

        // Alış & Satış Ayrımı için Hızlı Segment Butonları
        var btnFilterAll = UITheme.CreateButton("🌐 Tümü", UITheme.Primary, Color.White, null!, 70, 28);
        btnFilterAll.Font = UITheme.RegularFont;
        var btnFilterPurchase = UITheme.CreateButton("📥 Alış", Color.FromArgb(226, 232, 240), UITheme.TextPrimary, null!, 90, 28);
        btnFilterPurchase.Font = UITheme.RegularFont;
        var btnFilterSale = UITheme.CreateButton("📤 Satış", Color.FromArgb(226, 232, 240), UITheme.TextPrimary, null!, 90, 28);
        btnFilterSale.Font = UITheme.RegularFont;

        void SetInvoiceTypeFilter(string typeName)
        {
            btnFilterAll.BackColor = typeName == "Tüm Faturalar" ? UITheme.Primary : Color.FromArgb(226, 232, 240);
            btnFilterAll.ForeColor = typeName == "Tüm Faturalar" ? Color.White : UITheme.TextPrimary;

            btnFilterPurchase.BackColor = typeName == "Alış Faturası" ? Color.FromArgb(79, 70, 229) : Color.FromArgb(226, 232, 240);
            btnFilterPurchase.ForeColor = typeName == "Alış Faturası" ? Color.White : UITheme.TextPrimary;

            btnFilterSale.BackColor = typeName == "Satış Faturası" ? Color.FromArgb(16, 185, 129) : Color.FromArgb(226, 232, 240);
            btnFilterSale.ForeColor = typeName == "Satış Faturası" ? Color.White : UITheme.TextPrimary;

            _cmbInvType.SelectedItem = typeName;
        }

        btnFilterAll.Click += (s, e) => { SetInvoiceTypeFilter("Tüm Faturalar"); RefreshInvoices(); };
        btnFilterPurchase.Click += (s, e) => { SetInvoiceTypeFilter("Alış Faturası"); RefreshInvoices(); };
        btnFilterSale.Click += (s, e) => { SetInvoiceTypeFilter("Satış Faturası"); RefreshInvoices(); };

        _cmbInvType.Visible = false;
        _cmbInvType.Items.Clear();
        _cmbInvType.Items.AddRange(new object[] { "Tüm Faturalar", "Alış Faturası", "Satış Faturası" });
        _cmbInvType.SelectedIndex = 0;

        _cmbInvStatus.Width = 115;
        _cmbInvStatus.Height = 28;
        _cmbInvStatus.Font = UITheme.RegularFont;
        _cmbInvStatus.Items.Clear();
        _cmbInvStatus.Items.AddRange(new object[] { "Tüm Durumlar", "Ödenmedi", "Kısmi Ödendi", "Ödendi" });
        _cmbInvStatus.SelectedIndex = 0;
        _cmbInvStatus.SelectedIndexChanged += (s, e) => RefreshInvoices();

        _dtpInvStart.Width = 90;
        _dtpInvStart.Height = 28;
        _dtpInvStart.Font = UITheme.RegularFont;
        _dtpInvStart.ValueChanged += (s, e) => RefreshInvoices();

        _dtpInvEnd.Width = 90;
        _dtpInvEnd.Height = 28;
        _dtpInvEnd.Font = UITheme.RegularFont;
        _dtpInvEnd.ValueChanged += (s, e) => RefreshInvoices();

        var btnRefreshInv = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshInvoices(), 34, 28);

        rowFilters.Controls.Add(_txtInvSearch);
        rowFilters.Controls.Add(btnFilterAll);
        rowFilters.Controls.Add(btnFilterPurchase);
        rowFilters.Controls.Add(btnFilterSale);
        rowFilters.Controls.Add(new Label { Text = "|", ForeColor = Color.FromArgb(203, 213, 225), AutoSize = true, Margin = new Padding(2, 5, 2, 0) });
        rowFilters.Controls.Add(_cmbInvStatus);
        rowFilters.Controls.Add(_dtpInvStart);
        rowFilters.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 5, 0, 0) });
        rowFilters.Controls.Add(_dtpInvEnd);
        rowFilters.Controls.Add(btnRefreshInv);

        // 2. SATIR: Aksiyon & İşlem Butonları (Sağa yaslı, açılır menülü)
        var rowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoScroll = false,
            Margin = new Padding(0)
        };

        var mnuInvTools = new ContextMenuStrip();
        mnuInvTools.Items.Add("📄 Fatura Kalemleri & Detay", null, (s, e) => OpenInvoiceMetaForSelectedInvoice());
        mnuInvTools.Items.Add("👁️ Orijinal PDF Belgesini Aç", null, (s, e) => OpenSelectedInvoicePdf());
        mnuInvTools.Items.Add("📊 Excel'e Aktar", null, ExportInvoicesToExcel);
        mnuInvTools.Items.Add(new ToolStripSeparator());
        mnuInvTools.Items.Add("🧹 Mükerrer Faturaları Temizle", null, (s, e) => DeduplicateInvoicesClick());
        mnuInvTools.Items.Add("🔙 Faturayı İptal Et / Geri Al", null, (s, e) => DeleteSelectedInvoiceClick());

        var btnInvTools = UITheme.CreateButton("⚙️ İşlemler ▾", Color.FromArgb(241, 245, 249), UITheme.TextPrimary, (s, e) =>
        {
            if (s is Control btn)
            {
                mnuInvTools.Show(btn, new Point(0, btn.Height));
            }
        }, 105, 30);

        var btnPayInv = UITheme.CreateButton("💳 Ödeme / Tahsilat", UITheme.Success, Color.White, (s, e) => OpenInvoicePaymentDialogForSelected(), 135, 30);
        var btnBatchInvoice = UITheme.CreateButton("📂 Toplu PDF Yükle", Color.FromArgb(14, 116, 144), Color.White, (s, e) => OpenBatchInvoiceImportDialog(), 140, 30);
        var btnNewInvoice = UITheme.CreateButton("➕ Yeni Alış Faturası", Color.FromArgb(79, 70, 229), Color.White, (s, e) => OpenInvoiceEntry(), 150, 30);

        rowActions.Controls.Add(btnInvTools);
        rowActions.Controls.Add(btnPayInv);
        rowActions.Controls.Add(btnBatchInvoice);
        rowActions.Controls.Add(btnNewInvoice);

        toolbarInv.Controls.Add(rowActions);
        toolbarInv.Controls.Add(rowFilters);

        // Alt Toplamlar Paneli (Alış, Satış ve Ödeme Dengesi)
        var pnlInvSummary = new CardPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(15, 6, 15, 6) };
        var flowInvSummary = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoScroll = false, WrapContents = false };

        _lblInvSummaryCount.Margin = new Padding(0, 6, 20, 0);
        _lblInvSummaryTotal.Margin = new Padding(0, 6, 20, 0);
        _lblInvSummaryPaid.Margin = new Padding(0, 6, 20, 0);
        _lblInvSummaryRemaining.Margin = new Padding(0, 6, 0, 0);

        flowInvSummary.Controls.Add(_lblInvSummaryCount);
        flowInvSummary.Controls.Add(_lblInvSummaryTotal);
        flowInvSummary.Controls.Add(_lblInvSummaryPaid);
        flowInvSummary.Controls.Add(_lblInvSummaryRemaining);
        pnlInvSummary.Controls.Add(flowInvSummary);

        // Fatura Tablosu (Ferah ve Geniş Grid)
        var gridContainerInv = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) };
        UITheme.ApplyGridStyle(_gridInvoices);
        _gridInvoices.CellDoubleClick += (s, e) => OpenInvoiceMetaForSelectedInvoice();

        // Alış ve Satış Faturalarını Görsel Olarak Ayırt Etme
        _gridInvoices.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _gridInvoices.Columns[e.ColumnIndex];

            if (col.Name == "Fatura Türü" && e.Value != null)
            {
                string val = e.Value.ToString() ?? "";
                if (val.Contains("Alış"))
                {
                    e.Value = "📥 Alış Faturası";
                    e.CellStyle.ForeColor = Color.FromArgb(67, 56, 202);
                    e.CellStyle.Font = new Font(_gridInvoices.Font, FontStyle.Bold);
                }
                else if (val.Contains("Satış"))
                {
                    e.Value = "📤 Satış Faturası";
                    e.CellStyle.ForeColor = Color.FromArgb(5, 150, 105);
                    e.CellStyle.Font = new Font(_gridInvoices.Font, FontStyle.Bold);
                }
            }

            if (col.Name == "Ödeme Durumu" && e.Value != null)
            {
                string val = e.Value.ToString() ?? "";
                if (val == "Ödendi")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(16, 185, 129);
                    e.CellStyle.Font = new Font(_gridInvoices.Font, FontStyle.Bold);
                }
                else if (val == "Kısmi Ödendi")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(217, 119, 6);
                    e.CellStyle.Font = new Font(_gridInvoices.Font, FontStyle.Bold);
                }
                else if (val == "Ödenmedi")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(220, 38, 38);
                    e.CellStyle.Font = new Font(_gridInvoices.Font, FontStyle.Bold);
                }
            }
        };

        var mnuInvoices = new ContextMenuStrip();
        var itemMeta = new ToolStripMenuItem("📄 Fatura Detayı ve Kalemleri Gör", null, (s, e) => OpenInvoiceMetaForSelectedInvoice())
        {
            Font = new Font(UITheme.RegularFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 148, 136)
        };
        var itemPdf = new ToolStripMenuItem("👁️ Orijinal PDF Belgesini Aç", null, (s, e) => OpenSelectedInvoicePdf())
        {
            ForeColor = UITheme.Secondary
        };
        var itemPay = new ToolStripMenuItem("💳 Faturaya Kısmi / Tam Ödeme Yap", null, (s, e) => OpenInvoicePaymentDialogForSelected())
        {
            ForeColor = UITheme.Success
        };
        var itemDel = new ToolStripMenuItem("🔙 Faturayı İptal Et / Geri Al (Stok ve Cariyi Sıfırla)", null, (s, e) => DeleteSelectedInvoiceClick())
        {
            Font = new Font(UITheme.RegularFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38)
        };

        mnuInvoices.Items.Add(itemMeta);
        mnuInvoices.Items.Add(itemPdf);
        mnuInvoices.Items.Add(new ToolStripSeparator());
        mnuInvoices.Items.Add(itemPay);
        mnuInvoices.Items.Add(new ToolStripSeparator());
        mnuInvoices.Items.Add(itemDel);
        _gridInvoices.ContextMenuStrip = mnuInvoices;

        gridContainerInv.Controls.Add(_gridInvoices);

        tabInvoices.Controls.Add(gridContainerInv);
        tabInvoices.Controls.Add(pnlInvSummary);
        tabInvoices.Controls.Add(toolbarInv);
        gridContainerInv.BringToFront();

        _tabsFinance.TabPages.Clear();
        _tabsFinance.TabPages.Add(tabAccMov);
        _tabsFinance.TabPages.Add(tabInvoices);

        _pnlAccountMovements.Controls.Add(_tabsFinance);
        _pnlAccountMovements.Controls.Add(header);
        _tabsFinance.BringToFront();
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
        var dbCard = new CardPanel { Width = 840, Height = 280, Margin = new Padding(0, 0, 0, 20) };
        var lblDbTitle = new Label { Text = "🗄️ SQL Server Veritabanı Yapılandırması & Yerel Yedekleme", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
        var lblDbDesc = new Label
        {
            Text = $"Aktif SQL Sunucusu: {Database.CurrentServer}\nVeritabanı Adı: {Database.CurrentDatabase}\nKimlik Doğrulama: {(Database.Config.IntegratedSecurity ? "Windows Kimlik Doğrulaması (Trusted)" : "SQL Server Kullanıcı Doğrulaması")}\n\nVeritabanı bağlantı parametrelerini test edebilir, SQL yedeği alabilir veya geri yükleyebilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 85
        };

        var dbButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, WrapContents = true };
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
        var btnMaint = UITheme.CreateButton("⚡ Optimize Et (Shrink & Reindex)", Color.FromArgb(16, 185, 129), Color.White, async (s, e) =>
        {
            var confirm = MessageBox.Show(
                "SQL Server veritabanı bakım ve optimizasyon işlemi başlatılacak:\n\n" +
                "1. Log (.ldf) dosyası küçültülecektir (Log Shrink)\n" +
                "2. Boş alanlar diske iade edilecektir (DB Shrink)\n" +
                "3. Tüm parçalanmış indeksler baştan inşa edilecektir (Rebuild Indexes)\n" +
                "4. Sorgu istatistikleri güncellenecektir (sp_updatestats)\n\n" +
                "Devam etmek istiyor musunuz?",
                "Veritabanı Bakım Onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );
            if (confirm != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            try
            {
                var res = await DatabaseMaintenanceService.ExecuteMaintenanceAsync();
                MessageBox.Show(res.Message, res.Success ? "Optimizasyon Başarılı" : "Hata", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }, 260, 36);

        var btnReset = UITheme.CreateButton("⚠️ Fabrika Ayarlarına Sıfırla", Color.FromArgb(220, 38, 38), Color.White, (s, e) => ResetDatabaseAction(), 220, 36);

        dbButtons.Controls.Add(btnConfigSql);
        dbButtons.Controls.Add(btnBackup);
        dbButtons.Controls.Add(btnRestore);
        dbButtons.Controls.Add(btnMaint);
        dbButtons.Controls.Add(btnReset);

        dbCard.Controls.Add(dbButtons);
        dbCard.Controls.Add(lblDbDesc);
        dbCard.Controls.Add(lblDbTitle);
        flow.Controls.Add(dbCard);

        // 2. Google Drive & Gmail Bulut Yedekleme & Geri Yükleme Kartı
        var cloudCard = new CardPanel { Width = 840, Height = 230, Margin = new Padding(0, 0, 0, 20) };
        var lblCloudTitle = new Label { Text = "☁️ Google Drive & Gmail Bulut Yedekleme & Geri Yükleme", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(2, 132, 199), Dock = DockStyle.Top, Height = 30 };
        var lblCloudDesc = new Label
        {
            Text = "Gmail kullanıcı adı & şifrenizle otomatik bağlantı sağlayın. SQL veritabanı yedekleriniz şifreli .zip formatında Google Drive veya yerel klasörünüze kaydedilir, dilerseniz Gmail adresinize e-posta ekiyle sessizce iletilir. Mevcut yedekleri görüntüleyip dilediğiniz yedeği tek tıkla geri yükleyebilirsiniz (Restore). Program her kapandığında veya günlük otomatik yedek alma seçeneklerini buradan yönetin.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 100
        };

        var cloudButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenCloudManage = UITheme.CreateButton("☁️ Bulut Yedekleme & Geri Yükleme Yönetimi", Color.FromArgb(2, 132, 199), Color.White, (s, e) =>
        {
            using var dlg = new CloudBackupManageDialog();
            dlg.ShowDialog(this);
        }, 290, 36);
        var btnTriggerCloud = UITheme.CreateButton("⚡ Şimdi Buluta Yedek Al", UITheme.Success, Color.White, (s, e) =>
        {
            var res = CloudBackupService.ExecuteBackup(silent: false);
            MessageBox.Show(res.Message, res.Success ? "Yedekleme Başarılı" : "Yedekleme Doğrulanamadı", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }, 190, 36);

        cloudButtons.Controls.Add(btnOpenCloudManage);
        cloudButtons.Controls.Add(btnTriggerCloud);

        cloudCard.Controls.Add(cloudButtons);
        cloudCard.Controls.Add(lblCloudDesc);
        cloudCard.Controls.Add(lblCloudTitle);
        flow.Controls.Add(cloudCard);

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

        // 6. WhatsApp Asistanı & Cep Takip Kartı
        var waCard = new CardPanel { Width = 840, Height = 170, Margin = new Padding(0, 0, 0, 20) };
        var lblWaTitle = new Label { Text = "💬 WhatsApp Asistanı & Cep Telefonundan Canlı Takip", Font = UITheme.TitleFont, ForeColor = Color.FromArgb(22, 101, 52), Dock = DockStyle.Top, Height = 30 };
        var lblWaDesc = new Label
        {
            Text = "Güncel kasa durumunu, kritik stokları ve borçluları WhatsApp üzerinden takip edin; mobil portal ile telefonunuzdan stok ve fiyat sorgulayın.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 50
        };

        var waButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
        var btnOpenWa = UITheme.CreateButton("💬 WhatsApp & Mobil Portal Ayarları", Color.FromArgb(22, 163, 74), Color.White, (s, e) => OpenWhatsAppAssistantDialog(), 300, 36);
        waButtons.Controls.Add(btnOpenWa);

        waCard.Controls.Add(waButtons);
        waCard.Controls.Add(lblWaDesc);
        waCard.Controls.Add(lblWaTitle);
        flow.Controls.Add(waCard);

        // 7. Görünüm (Açık / Koyu Tema)
        var themeCard = new CardPanel { Width = 840, Height = 150, Margin = new Padding(0, 0, 0, 20) };
        var lblThemeTitle = new Label { Text = "🎨 Görünüm", Font = UITheme.TitleFont, ForeColor = UITheme.Primary, Dock = DockStyle.Top, Height = 30 };
        var lblThemeDesc = new Label
        {
            Text = "Açık veya koyu temayı seçin. Seçiminiz kaydedilir ve program yeniden başlatıldığında tüm pencerelere uygulanır.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            Height = 40
        };

        var themeButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.LeftToRight };
        foreach (var opt in AppThemeService.AvailableThemes)
        {
            var themeOpt = opt;
            bool selected = themeOpt == AppThemeService.CurrentTheme;
            var btn = UITheme.CreateButton(themeOpt.DisplayName, selected ? UITheme.Primary : UITheme.BorderColor, selected ? Color.White : UITheme.TextPrimary, (s, e) =>
            {
                AppThemeService.ApplyTheme(themeOpt);
                MessageBox.Show("Tema kaydedildi. Programı yeniden başlattığınızda tüm pencerelere uygulanır.", "Görünüm", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, 170, 34);
            themeButtons.Controls.Add(btn);
        }

        themeCard.Controls.Add(themeButtons);
        themeCard.Controls.Add(lblThemeDesc);
        themeCard.Controls.Add(lblThemeTitle);
        flow.Controls.Add(themeCard);

        // 8. Kullanıcılar & Lisans kısayol kartları (tek ayar penceresinde toplanması için)
        CardPanel MakeShortcutCard(string title, string desc, string btnText, Color btnColor, Action action)
        {
            var card = new CardPanel { Width = 840, Height = 170, Margin = new Padding(0, 0, 0, 20) };
            var lt = new Label { Text = title, Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Dock = DockStyle.Top, Height = 30 };
            var ld = new Label { Text = desc, Font = UITheme.RegularFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 50 };
            var fb = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45 };
            fb.Controls.Add(UITheme.CreateButton(btnText, btnColor, Color.White, (s, e) => action(), 260, 36));
            card.Controls.Add(fb);
            card.Controls.Add(ld);
            card.Controls.Add(lt);
            return card;
        }
        var userCard = MakeShortcutCard("👤 Kullanıcılar, Roller & Yetkiler",
            "Personel hesaplarını, rollerini, depo yetkilerini ve şifre politikalarını yönetin.",
            "👤 Kullanıcı Yönetimini Aç", UITheme.Primary, () => ShowPage(6));
        var licCard = MakeShortcutCard("🔑 Lisans & Aktivasyon",
            "Lisans durumunu, kalan süreyi görüntüleyin; yeni lisans aktifleştirin.",
            "🔑 Lisans Sayfasını Aç", UITheme.Success, () => ShowPage(7));
        flow.Controls.Add(userCard);
        flow.Controls.Add(licCard);

        // Sol kategori menüsü + sağda yalnızca seçili kategorinin kartı
        _settingsCategories.Clear();
        _settingsCategories.Add(("🗄️  Veritabanı & Yerel Yedek", dbCard));
        _settingsCategories.Add(("☁️  Bulut Yedekleme", cloudCard));
        _settingsCategories.Add(("📥  Veri Aktarımı (Excel)", excelCard));
        _settingsCategories.Add(("💬  WhatsApp & Mobil", waCard));
        _settingsCategories.Add(("🎨  Görünüm", themeCard));
        _settingsCategories.Add(("👤  Kullanıcılar", userCard));
        _settingsCategories.Add(("📜  Denetim Kayıtları", logCard));
        _settingsCategories.Add(("🔑  Lisans", licCard));

        var nav = new Panel { Dock = DockStyle.Left, Width = 230, Padding = new Padding(0, 10, 10, 0), BackColor = Color.Transparent };
        var navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        _settingsNavButtons.Clear();
        for (int i = 0; i < _settingsCategories.Count; i++)
        {
            int idx = i;
            var b = new Button
            {
                Text = _settingsCategories[i].Title,
                Width = 208,
                Height = 42,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 4)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => ShowSettingsCategory(idx);
            _settingsNavButtons.Add(b);
            navFlow.Controls.Add(b);
        }
        nav.Controls.Add(navFlow);

        _pnlSettings.AutoScroll = false;
        _pnlSettings.Controls.Add(flow);
        _pnlSettings.Controls.Add(nav);
        _pnlSettings.Controls.Add(header);
        header.SendToBack();
        nav.BringToFront();
        flow.BringToFront();

        ShowSettingsCategory(0);
    }

    private readonly List<(string Title, Control Card)> _settingsCategories = new();
    private readonly List<Button> _settingsNavButtons = new();

    /// <summary>Ayarlar sayfasında verilen kategoriyi (0=Veritabanı ... 4=Görünüm) gösterir.</summary>
    private void ShowSettingsCategory(int index)
    {
        if (index < 0 || index >= _settingsCategories.Count) return;
        for (int i = 0; i < _settingsCategories.Count; i++)
        {
            _settingsCategories[i].Card.Visible = i == index;
            var b = _settingsNavButtons[i];
            b.BackColor = i == index ? UITheme.PrimaryLight : UITheme.CardBg;
            b.ForeColor = i == index ? UITheme.Primary : UITheme.TextPrimary;
            b.Font = new Font("Segoe UI", 10f, i == index ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private void OpenSettings(int category)
    {
        ShowPage(8);
        ShowSettingsCategory(category);
    }
    #endregion

    #region 7. Audit Logs Page
    private void BuildAuditLogsPage()
    {
        _pnlAuditLogs = new Panel { Dock = DockStyle.Fill };

        var header = CreatePageHeader("İşlem & Güvenlik Logları (Audit Trail)", "Silinen veya değiştirilen tüm cariler, hesap hareketleri ve ürün kayıtları");

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Filtreler
        var pnlLeftFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtAuditSearch.Width = 175;
        _txtAuditSearch.Height = 32;
        _txtAuditSearch.PlaceholderText = "🔍 Loglarda Ara...";
        _txtAuditSearch.Font = UITheme.RegularFont;
        _txtAuditSearch.TextChanged += (s, e) => RefreshAuditLogs();

        _cmbAuditEntity.Width = 110;
        _cmbAuditEntity.Height = 32;
        _cmbAuditEntity.Font = UITheme.RegularFont;
        _cmbAuditEntity.Items.Clear();
        _cmbAuditEntity.Items.AddRange(new object[] { "Tüm Varlıklar", "Kullanıcı", "Cari", "CariHareket", "Urun", "StokHareket", "Sistem" });
        _cmbAuditEntity.SelectedIndex = 0;
        _cmbAuditEntity.SelectedIndexChanged += (s, e) => RefreshAuditLogs();

        _cmbAuditAction.Width = 120;
        _cmbAuditAction.Height = 32;
        _cmbAuditAction.Font = UITheme.RegularFont;
        _cmbAuditAction.Items.Clear();
        _cmbAuditAction.Items.AddRange(new object[] { "Tüm İşlemler", "Giriş", "Güncelleme", "Silindi", "Silindi / Pasife Alındı", "Güncellendi", "Yeni Eklendi" });
        _cmbAuditAction.SelectedIndex = 0;
        _cmbAuditAction.SelectedIndexChanged += (s, e) => RefreshAuditLogs();

        _dtpAuditStart.Width = 95;
        _dtpAuditStart.Height = 32;
        _dtpAuditStart.Font = UITheme.RegularFont;
        _dtpAuditStart.ValueChanged += (s, e) => RefreshAuditLogs();

        _dtpAuditEnd.Width = 95;
        _dtpAuditEnd.Height = 32;
        _dtpAuditEnd.Font = UITheme.RegularFont;
        _dtpAuditEnd.ValueChanged += (s, e) => RefreshAuditLogs();

        var btnRefresh = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshAuditLogs(), 36, 32);

        pnlLeftFilters.Controls.Add(_txtAuditSearch);
        pnlLeftFilters.Controls.Add(_cmbAuditEntity);
        pnlLeftFilters.Controls.Add(_cmbAuditAction);
        pnlLeftFilters.Controls.Add(_dtpAuditStart);
        pnlLeftFilters.Controls.Add(new Label { Text = "-", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 6, 0, 0) });
        pnlLeftFilters.Controls.Add(_dtpAuditEnd);
        pnlLeftFilters.Controls.Add(btnRefresh);

        // Sağ: Aksiyon Butonu
        var pnlRightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnExportExcel = UITheme.CreateButton("📊 Excel'e Aktar", UITheme.Success, Color.White, ExportAuditLogsToExcel, 120, 32);
        pnlRightActions.Controls.Add(btnExportExcel);

        toolbar.Controls.Add(pnlLeftFilters);
        toolbar.Controls.Add(pnlRightActions);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(0) };
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

        var toolbar = new CardPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10, 3, 10, 3) };

        // Sol: Arama & Yenileme
        var pnlLeftFilters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        _txtUserSearch.Width = 200;
        _txtUserSearch.Height = 32;
        _txtUserSearch.PlaceholderText = "🔍 Kullanıcı Ara...";
        _txtUserSearch.Font = UITheme.RegularFont;
        _txtUserSearch.TextChanged += (s, e) => RefreshUsers();

        var btnRefreshUsers = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshUsers(), 36, 32);

        pnlLeftFilters.Controls.Add(_txtUserSearch);
        pnlLeftFilters.Controls.Add(btnRefreshUsers);

        // Sağ: Aksiyon Butonları
        var pnlRightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent
        };

        var btnAddUser = UITheme.CreateButton("➕ Yeni Kullanıcı", UITheme.Primary, Color.White, (s, e) =>
        {
            using var dlg = new UserEditDialog(0);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                RefreshUsers();
        }, 130, 32);

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
        }, 95, 32);

        var btnDeleteUser = UITheme.CreateButton("🗑️ Sil", UITheme.Danger, Color.White, (s, e) =>
        {
            var curUser = UserService.CurrentUser;
            if (curUser != null && !curUser.HasPermission(UserPermissions.Users) && !curUser.IsSuperUser)
            {
                MessageBox.Show("Kullanıcı silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
        }, 80, 32);

        pnlRightActions.Controls.Add(btnAddUser);
        pnlRightActions.Controls.Add(btnEditUser);
        pnlRightActions.Controls.Add(btnDeleteUser);

        toolbar.Controls.Add(pnlLeftFilters);
        toolbar.Controls.Add(pnlRightActions);

        var gridContainer = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0), Padding = new Padding(0) };
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
            var cardGen = new CardPanel { Width = 900, Height = 170, Padding = new Padding(20), Margin = new Padding(0, 15, 0, 0) };
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
                Text = "Bu kontrol paneli yalnızca Süper Kullanıcı oturumunda görüntülenir. Normal yönetici veya personel ekranlarında gizlidir. Buradan müşterilerden gelen makine donanım kodlarına göre 1 yıllık benzersiz çevrimdışı aktivasyon anahtarları üretebilirsiniz.", 
                Font = UITheme.RegularFont, 
                ForeColor = UITheme.TextSecondary, 
                Dock = DockStyle.Top, 
                Height = 58
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
        // Artık üst SaaS Header'da dinamik başlık (_lblPageTitle, _lblPageSubTitle) yer aldığı için
        // sayfa içi çift başlık kapatılarak veri tablolarına dikeyde 70px ekstra alan kazandırılmıştır.
        return new Panel { Dock = DockStyle.Top, Height = 0, Visible = false };
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

            if (_gridInvoices.Columns["Fatura No"] is { } cNo) { cNo.Width = 140; }
            if (_gridInvoices.Columns["Tarih"] is { } cDate) { cDate.Width = 95; }
            if (_gridInvoices.Columns["Fatura Türü"] is { } cType) { cType.Width = 135; }
            if (_gridInvoices.Columns["Cari Ünvanı"] is { } cAcc) { cAcc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; }

            if (_gridInvoices.Columns["Fatura Tutarı"] is { } cGrand)
            {
                cGrand.Width = 115;
                cGrand.DefaultCellStyle.Format = "N2";
                cGrand.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_gridInvoices.Columns["Ödenen Tutar"] is { } cPaid)
            {
                cPaid.Width = 105;
                cPaid.DefaultCellStyle.Format = "N2";
                cPaid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_gridInvoices.Columns["Kalan Tutar"] is { } cRem)
            {
                cRem.Width = 105;
                cRem.DefaultCellStyle.Format = "N2";
                cRem.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            if (_gridInvoices.Columns["Ödeme Durumu"] is { } cStatus) { cStatus.Width = 110; }
            if (_gridInvoices.Columns["Depo / Şube"] is { } cDepo) { cDepo.Width = 120; }

            // Alt Toplamları Hesapla: Alış ve Satış toplamlarını ayrı ayrı topla!
            int count = dt.Rows.Count;
            int purchaseCount = 0;
            int saleCount = 0;
            double purchaseGrand = 0;
            double saleGrand = 0;
            double totalPaid = 0;
            double totalRemaining = 0;

            foreach (DataRow r in dt.Rows)
            {
                string invType = r["Fatura Türü"]?.ToString() ?? "";
                double g = Convert.ToDouble(r["Fatura Tutarı"]);
                double p = Convert.ToDouble(r["Ödenen Tutar"]);
                double rem = Convert.ToDouble(r["Kalan Tutar"]);

                if (invType.Contains("Alış"))
                {
                    purchaseCount++;
                    purchaseGrand += g;
                }
                else
                {
                    saleCount++;
                    saleGrand += g;
                }
                totalPaid += p;
                totalRemaining += rem;
            }

            string typeLabel = _cmbInvType.SelectedItem?.ToString() ?? "Tüm Faturalar";
            if (typeLabel == "Alış Faturası")
            {
                _lblInvSummaryCount.Text = $"📥 Alış Faturaları: {purchaseCount} Adet";
                _lblInvSummaryTotal.Text = $"💰 Toplam Alış: {purchaseGrand:N2} ₺";
            }
            else if (typeLabel == "Satış Faturası")
            {
                _lblInvSummaryCount.Text = $"📤 Satış Faturaları: {saleCount} Adet";
                _lblInvSummaryTotal.Text = $"💰 Toplam Satış: {saleGrand:N2} ₺";
            }
            else
            {
                _lblInvSummaryCount.Text = $"📋 Toplam {count} Fatura ({purchaseCount} Alış, {saleCount} Satış)";
                _lblInvSummaryTotal.Text = $"📥 Alış: {purchaseGrand:N2} ₺   |   📤 Satış: {saleGrand:N2} ₺";
            }

            _lblInvSummaryPaid.Text = $"✅ Ödenen: {totalPaid:N2} ₺";
            _lblInvSummaryRemaining.Text = $"⚠️ Kalan Bakiye: {totalRemaining:N2} ₺";
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
        using var dlg = new InvoiceMetaDialog(invId);
        dlg.ShowDialog(this);
    }

    private void OpenSelectedInvoicePdf()
    {
        if (_gridInvoices.CurrentRow == null || !_gridInvoices.Columns.Contains("Id"))
        {
            MessageBox.Show("Lütfen PDF belgesini açmak istediğiniz faturayı seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long invId = Convert.ToInt64(_gridInvoices.CurrentRow.Cells["Id"].Value);
        if (!InvoiceService.OpenInvoicePdfById(invId))
        {
            MessageBox.Show("Bu faturaya ait PDF belgesi veritabanında veya arşivde bulunamadı.", "PDF Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void DeduplicateInvoicesClick()
    {
        var ask = MessageBox.Show(
            "Sistemdeki mükerrer (çift yüklenmiş) faturalar taranacak ve aynı fatura numarasına ait fazlalık kopyalar temizlenecektir.\n\n" +
            "Her faturanın en dolu ve en güncel ana kaydı korunacaktır.\n\n" +
            "Mükerrer fatura temizliğini başlatmak istiyor musunuz?",
            "Mükerrer Faturaları Temizle",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (ask == DialogResult.Yes)
        {
            var (removed, uniqueCount) = InvoiceService.DeduplicateInvoices();
            RefreshInvoices();
            if (removed > 0)
            {
                MessageBox.Show(
                    $"İşlem tamamlandı!\n\n" +
                    $"• {removed} adet mükerrer fatura kaydı başarıyla silindi.\n" +
                    $"• Kalan tekil gerçek fatura sayısı: {uniqueCount} Adet",
                    "Temizleme Başarılı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                MessageBox.Show("Sistemde mükerrer (çift) fatura kaydı bulunamadı. Tüm faturalarınız zaten tekildir.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void DeleteSelectedInvoiceClick()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.InvoicesDelete))
        {
            MessageBox.Show("Faturayı iptal etme / geri alma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_gridInvoices.CurrentRow == null || !_gridInvoices.Columns.Contains("Id"))
        {
            MessageBox.Show("Lütfen iptal etmek / geri almak istediğiniz faturayı seçiniz.", "Fatura Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long invId = Convert.ToInt64(_gridInvoices.CurrentRow.Cells["Id"].Value);
        string invNo = _gridInvoices.CurrentRow.Cells["Fatura No"].Value?.ToString() ?? "";
        string accName = _gridInvoices.Columns.Contains("Cari Ünvanı") ? _gridInvoices.CurrentRow.Cells["Cari Ünvanı"].Value?.ToString() ?? "" : "";
        double grandTotal = _gridInvoices.Columns.Contains("Fatura Tutarı") ? Convert.ToDouble(_gridInvoices.CurrentRow.Cells["Fatura Tutarı"].Value) : 0;

        var ask = MessageBox.Show(
            $"⚠️ FATURAYI İPTAL ETME & GERİ ALMA (ROLLBACK) ONAYI\n\n" +
            $"• Fatura No: {invNo}\n" +
            $"• Tedarikçi/Cari: {accName}\n" +
            $"• Toplam Tutar: {grandTotal:N2} ₺\n\n" +
            $"Bu işlem onaylandığında:\n" +
            $"1. Faturanın depoya soktuğu tüm ürün stok hareketleri silinir (ürün stok miktarları faturadan önceki eski haline döner).\n" +
            $"2. Tedarikçi cari hesabına işlenen borç kaydı silinir (cari hesap bakiyesi faturadan önceki haline döner).\n" +
            $"3. Fatura ve tüm kalemleri iptal edilir.\n\n" +
            $"Bu faturayı tüm hareketleriyle birlikte geri almak ve iptal etmek istediğinizden emin misiniz?",
            "Faturayı İptal Et ve Geri Al (Ters Kayıt)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (ask == DialogResult.Yes)
        {
            var result = InvoiceService.CancelAndRollbackInvoice(invId, rollbackMovements: true);
            if (result.Success)
            {
                RefreshInvoices();
                RefreshStockMovements();
                RefreshAccountMovements();
                RefreshDashboard();
                MessageBox.Show(result.Message, "Fatura Başarıyla Geri Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(result.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
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

    private void OpenSaleReturn()
    {
        if (!Require(UserPermissions.QuickBuy, "Satış iadesi ve değişim")) return;
        using var dlg = new SaleReturnDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK) RefreshAll();
    }

    private void OpenQuickSale(string operation = "Satış", long? productId = null)
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.QuickSale) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Hızlı satış ve fiş modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using Form f = operation == "Alış"
            ? new QuickPurchaseDialog(productId)
            : new QuickSaleDialog(operation, productId);

        if (f.ShowDialog() == DialogResult.OK)
        {
            RefreshAll();
        }
    }

    private void AddProduct()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductsCreate) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Yeni ürün ekleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new ProductForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void EditProduct()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductsEdit) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Ürün düzenleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductsDelete))
        {
            MessageBox.Show("Ürün silme / çıkarma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductsCreate) && !curUser.HasPermission(UserPermissions.Products))
        {
            MessageBox.Show("Hızlı ürün tanımlama yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (curUser != null && !curUser.HasPermission(UserPermissions.ProductsDelete))
        {
            MessageBox.Show("Toplu ürün silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        if (curUser != null && !curUser.HasPermission(UserPermissions.StockMovementsDelete))
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
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountsDelete))
        {
            MessageBox.Show("Cari silme / çıkarma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountsCreate) && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("Yeni cari hesap ekleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new AccountForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void EditAccount()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountsEdit) && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("Cari hesap bilgilerini düzenleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountsDelete))
        {
            MessageBox.Show("Cari hesap silme / çıkarma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountStatement) && !curUser.HasPermission(UserPermissions.Accounts))
        {
            MessageBox.Show("Cari hesap ekstresini görüntüleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
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
        var curUser = UserService.CurrentUser;
        if (curUser != null)
        {
            if (explicitType == "Tahsilat" && !curUser.HasPermission(UserPermissions.FinanceCollect) && !curUser.HasPermission(UserPermissions.AccountMovements))
            {
                MessageBox.Show("Tahsilat işlemi yapma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (explicitType == "Ödeme" && !curUser.HasPermission(UserPermissions.FinancePay) && !curUser.HasPermission(UserPermissions.AccountMovements))
            {
                MessageBox.Show("Ödeme işlemi yapma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (explicitType == "Borç" && !curUser.HasPermission(UserPermissions.FinanceDebt) && !curUser.HasPermission(UserPermissions.AccountMovements))
            {
                MessageBox.Show("Borç kaydı girme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

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

    /// <summary>Kritik işlemler için yetkiyi kontrol eder; yoksa uyarı gösterir.</summary>
    private bool Require(string permission, string actionDescription)
    {
        var u = UserService.CurrentUser;
        if (u == null || u.HasPermission(permission)) return true;
        MessageBox.Show($"{actionDescription} için yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void AddStock()
    {
        if (!Require(UserPermissions.StockMovementsCreate, "Manuel stok hareketi ekleme")) return;
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
        if (curUser != null && !curUser.HasPermission(UserPermissions.WarehouseTransfer))
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

    private void OpenBatchInvoiceImportDialog()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.InvoicesBatchImport) && !curUser.HasPermission(UserPermissions.InvoiceEntry))
        {
            MessageBox.Show("Toplu PDF Fatura içe aktarma modülüne erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new BatchInvoiceImportDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            RefreshAll();
            RefreshInvoices();
        }
    }

    private void OpenWhatsAppAssistantDialog()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.WhatsAppPortal) && !curUser.IsSuperUser)
        {
            MessageBox.Show("WhatsApp ve online mobil portal asistanına erişim yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dlg = new WhatsAppAssistantDialog();
        dlg.ShowDialog(this);
    }

    private void DeleteStock()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.StockMovementsDelete))
        {
            MessageBox.Show("Stok hareketi silme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.AccountMovements) && !curUser.HasPermission(UserPermissions.FinanceCollect) && !curUser.HasPermission(UserPermissions.FinancePay))
        {
            MessageBox.Show("Finansal hareket ekleme yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        using var f = new AccountMovementForm();
        if (f.ShowDialog() == DialogResult.OK) RefreshAll();
    }

    private void DeleteAccountMovement()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.HasPermission(UserPermissions.FinanceDeleteMovement))
        {
            MessageBox.Show("Finansal hareket silme / çıkarma yetkiniz bulunmamaktadır.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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
        if (!Require(UserPermissions.SettingsBackup, "Veritabanı yedeği alma")) return;
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
                var ver = Database.VerifyBackupFile(sfd.FileName);
                if (ver.Ok)
                    MessageBox.Show("SQL Server veritabanı yedeği (.bak) alındı ve doğrulandı ✅", "Yedekleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show($"Yedek alındı ancak doğrulanamadı ❌\n\n{ver.Message}", "Yedek Doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedekleme sırasında hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RestoreDatabaseClick(object? sender, EventArgs e)
    {
        if (!Require(UserPermissions.SettingsBackup, "Yedekten geri yükleme")) return;
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

    private void ResetDatabaseAction()
    {
        var curUser = UserService.CurrentUser;
        if (curUser != null && !curUser.IsSuperUser && !curUser.HasPermission(UserPermissions.SystemReset))
        {
            MessageBox.Show("Veritabanını sıfırlama işlemi için 'Fabrika Ayarlarına Sıfırlama' yetkisi gereklidir.", "Yetki Yetersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var ask1 = MessageBox.Show(
            "⚠️ DİKKAT: Veritabanındaki TÜM FATURALAR, STOK HAREKETLERİ, ÜRÜN KARTLARI, CARİ HESAPLAR VE KASA İŞLEMLERİ KALICI OLARAK SİLİNECEKTİR!\n\n" +
            "Tüm ID numaraları 1'den başlayacak şekilde sıfırlanacaktır.\n\n" +
            "Bu işlem geri alınamaz! Devam etmek istiyor musunuz?",
            "TÜM VERİLERİ SIFIRLAMA ONAYI (1/2)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );
        if (ask1 != DialogResult.Yes) return;

        var ask2 = MessageBox.Show(
            "SON ONAY: Gerçekten tüm verileri silip sistemi fabrika ayarlarına döndürmek istediğinize emin misiniz?",
            "SON ONAY (2/2)",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Stop,
            MessageBoxDefaultButton.Button2
        );
        if (ask2 != DialogResult.Yes) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            Database.ResetAllDataAndReseed();
            MessageBox.Show(
                "Veritabanı başarıyla sıfırlandı!\n\n" +
                "• Tüm fatura, ürün, cari ve kasa kayıtları silindi.\n" +
                "• Tüm ID sayaçları 1'e çekildi.\n" +
                "• Kullanıcılar da silindi: program yeniden başlatıldığında yönetici şifresini yeniden belirlemeniz istenecek.\n" +
                "• Varsayılan depo: Merkez Depo (ID: 1)\n\n" +
                "Artık sıfırdan veri girişine başlayabilirsiniz.",
                "Fabrika Ayarlarına Dönüldü",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            RefreshAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Sıfırlama sırasında hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
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
