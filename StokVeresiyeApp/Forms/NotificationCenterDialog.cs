using System.Data;
using System.Diagnostics;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class NotificationCenterDialog : Form
{
    private readonly TabControl _tabControl = new();
    private readonly DataGridView _gridCriticalStock = new();
    private readonly DataGridView _gridDueReceivables = new();
    private readonly DataGridView _gridExpiring = new();
    
    // SMS Ayar Kontrolleri
    private readonly ComboBox _cmbSmsProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _txtSmsHeader = new();
    private readonly TextBox _txtSmsUsername = new();
    private readonly TextBox _txtSmsPassword = new() { UseSystemPasswordChar = true };
    private readonly TextBox _txtSmsTemplate = new() { Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical };
    private readonly Label _lblAlertSummary = new();

    public NotificationCenterDialog(int initialTab = 0)
    {
        Text = "🔔 Bildirim & Uyarı Merkezi (Kritik Stok & Vadesi Dolan Veresiye SMS)";
        ClientSize = new Size(1160, 690);
        MinimumSize = new Size(980, 620);
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        try
        {
            LoadData();
        }
        catch
        {
            _lblAlertSummary.Text = "⚠️ Bildirim verileri yüklenirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
            _lblAlertSummary.ForeColor = UITheme.Danger;
        }

        if (initialTab >= 0 && initialTab < _tabControl.TabCount)
        {
            _tabControl.SelectedIndex = initialTab;
        }
    }

    private void BuildUI()
    {
        // 1. Üst Başlık Paneli
        var headerPanel = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 72,
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = "🔔 Bildirim, Kritik Stok & Vade Hatırlatma Merkezi",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        _lblAlertSummary.Font = UITheme.RegularFont;
        _lblAlertSummary.ForeColor = UITheme.Danger;
        _lblAlertSummary.Dock = DockStyle.Top;
        _lblAlertSummary.Height = 22;
        _lblAlertSummary.Text = "Veriler yükleniyor...";

        headerPanel.Controls.Add(_lblAlertSummary);
        headerPanel.Controls.Add(lblTitle);
        Controls.Add(headerPanel);

        // 2. Sekmeler (TabControl)
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _tabControl.Padding = new Point(14, 8);

        BuildCriticalStockTab();
        BuildDueReceivablesTab();
        BuildExpiringTab();
        BuildSmsSettingsTab();

        Controls.Add(_tabControl);

        // 3. Alt Buton Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = UITheme.CardBg,
            Padding = new Padding(15, 10, 15, 10)
        };

        var btnClose = UITheme.CreateButton("Kapat", UITheme.Secondary, Color.White, (s, e) => Close(), 100, 34);
        btnClose.Dock = DockStyle.Right;
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);

        // Z-Order: arka plan başlık ve alt buton katmanları sabit kalsın, içerik sekmesi üstte görünsün.
        Controls.SetChildIndex(headerPanel, 0);
        Controls.SetChildIndex(bottomPanel, 1);
        Controls.SetChildIndex(_tabControl, 2);
        _tabControl.BringToFront();
    }

    private void BuildCriticalStockTab()
    {
        var tab = new TabPage("📦 Kritik Stok Uyarıları");
        tab.BackColor = UITheme.Background;
        tab.Padding = new Padding(12);

        // Toolbar
        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var btnRefresh = UITheme.CreateButton("🔄 Listeyi Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => LoadCriticalStocks(), 130, 34);
        var btnSupplierWa = UITheme.CreateButton("📲 Tedarikçiye WhatsApp Siparişi At", Color.FromArgb(37, 211, 102), Color.White, (s, e) => SendSupplierOrderWhatsApp(), 260, 34);

        btnRefresh.Dock = DockStyle.Left;
        btnSupplierWa.Dock = DockStyle.Left;

        pnlBar.Controls.Add(btnSupplierWa);
        pnlBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        pnlBar.Controls.Add(btnRefresh);

        // Grid
        UITheme.ApplyGridStyle(_gridCriticalStock);
        _gridCriticalStock.Dock = DockStyle.Fill;
        _gridCriticalStock.CellFormatting += GridCriticalStock_CellFormatting;

        var gridWrap = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        gridWrap.Controls.Add(_gridCriticalStock);

        tab.Controls.Add(gridWrap);
        tab.Controls.Add(pnlBar);
        _tabControl.TabPages.Add(tab);
    }

    private void BuildDueReceivablesTab()
    {
        var tab = new TabPage("⏰ Vadesi Dolan Veresiyeler & SMS");
        tab.BackColor = UITheme.Background;
        tab.Padding = new Padding(12);

        // Toolbar
        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var btnSendSms = UITheme.CreateButton("✉️ Seçiliye SMS Gönder", UITheme.Primary, Color.White, async (s, e) => await SendSelectedSms(), 180, 34);
        var btnBulkSms = UITheme.CreateButton("📢 Tüm Gecikenlere Toplu SMS", Color.FromArgb(220, 38, 38), Color.White, async (s, e) => await SendBulkSms(), 240, 34);
        var btnWhatsApp = UITheme.CreateButton("📲 WhatsApp ile Hatırlat", Color.FromArgb(37, 211, 102), Color.White, (s, e) => SendCustomerWhatsApp(), 200, 34);
        var btnRefresh = UITheme.CreateButton("🔄 Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => LoadDueReceivables(), 90, 34);

        btnSendSms.Dock = DockStyle.Left;
        btnBulkSms.Dock = DockStyle.Left;
        btnWhatsApp.Dock = DockStyle.Left;
        btnRefresh.Dock = DockStyle.Left;

        pnlBar.Controls.Add(btnWhatsApp);
        pnlBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        pnlBar.Controls.Add(btnBulkSms);
        pnlBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        pnlBar.Controls.Add(btnSendSms);
        pnlBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        pnlBar.Controls.Add(btnRefresh);

        // Grid
        UITheme.ApplyGridStyle(_gridDueReceivables);
        _gridDueReceivables.Dock = DockStyle.Fill;
        _gridDueReceivables.CellFormatting += GridDueReceivables_CellFormatting;

        var gridWrap = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        gridWrap.Controls.Add(_gridDueReceivables);

        tab.Controls.Add(gridWrap);
        tab.Controls.Add(pnlBar);
        _tabControl.TabPages.Add(tab);
    }

    private void BuildExpiringTab()
    {
        var tab = new TabPage("⏳ SKT & Parti Takibi");
        tab.BackColor = UITheme.Background;
        tab.Padding = new Padding(12);

        // Toolbar
        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var btnRefresh = UITheme.CreateButton("🔄 Listeyi Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => LoadExpiringProducts(), 130, 34);
        var btnEditProd = UITheme.CreateButton("✏️ Ürün Kartını Düzenle", UITheme.Primary, Color.White, (s, e) => EditSelectedExpiringProduct(), 180, 34);

        btnRefresh.Dock = DockStyle.Left;
        btnEditProd.Dock = DockStyle.Left;

        pnlBar.Controls.Add(btnEditProd);
        pnlBar.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
        pnlBar.Controls.Add(btnRefresh);

        // Grid
        UITheme.ApplyGridStyle(_gridExpiring);
        _gridExpiring.Dock = DockStyle.Fill;
        _gridExpiring.CellFormatting += GridExpiring_CellFormatting;
        _gridExpiring.CellDoubleClick += (s, e) => EditSelectedExpiringProduct();

        var gridWrap = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(6) };
        gridWrap.Controls.Add(_gridExpiring);

        tab.Controls.Add(gridWrap);
        tab.Controls.Add(pnlBar);
        _tabControl.TabPages.Add(tab);
    }

    private void BuildSmsSettingsTab()
    {
        var tab = new TabPage("⚙️ SMS Entegrasyonu & Şablon");
        tab.BackColor = UITheme.Background;
        tab.Padding = new Padding(20);

        var pnlForm = new CardPanel { Dock = DockStyle.Top, Height = 480, Padding = new Padding(25) };

        _cmbSmsProvider.Items.AddRange(new object[] { "Demo (Simülatör / Ücretsiz)", "NetGSM (API Gateway)", "İletiMerkezi" });
        _cmbSmsProvider.Width = 320;

        _txtSmsHeader.Width = 320;
        _txtSmsUsername.Width = 320;
        _txtSmsPassword.Width = 320;
        _txtSmsTemplate.Width = 550;

        // Ayarları Doldur
        var cfg = NotificationService.CurrentSettings;
        _cmbSmsProvider.SelectedItem = cfg.Provider == "NetGSM" ? "NetGSM (API Gateway)" : (cfg.Provider == "IletiMerkezi" ? "İletiMerkezi" : "Demo (Simülatör / Ücretsiz)");
        _txtSmsHeader.Text = cfg.Header;
        _txtSmsUsername.Text = cfg.Username;
        _txtSmsPassword.Text = cfg.Password;
        _txtSmsTemplate.Text = cfg.DueReminderTemplate;

        int rowTop = 20;
        void AddSettingRow(string label, Control ctrl, string hint = "")
        {
            var lbl = new Label { Text = label, Top = rowTop + 4, Left = 20, Width = 180, Font = UITheme.TitleFont };
            ctrl.Top = rowTop;
            ctrl.Left = 210;
            pnlForm.Controls.Add(lbl);
            pnlForm.Controls.Add(ctrl);

            if (!string.IsNullOrEmpty(hint))
            {
                var lblHint = new Label { Text = hint, Top = rowTop + 6, Left = ctrl.Right + 15, Width = 300, ForeColor = UITheme.TextMuted, Font = UITheme.SmallFont };
                pnlForm.Controls.Add(lblHint);
            }

            rowTop += ctrl.Height + 16;
        }

        AddSettingRow("SMS Servis Sağlayıcı:", _cmbSmsProvider, "Demo modunda SMS simüle edilir ve işlem loglarına yazılır.");
        AddSettingRow("SMS Başlığı (Originator):", _txtSmsHeader, "Operatör onaylı SMS başlığınız (örn: FIRMAADI).");
        AddSettingRow("API Kullanıcı Adı:", _txtSmsUsername, "SMS operatörü kullanıcı kodu.");
        AddSettingRow("API Şifre / Token:", _txtSmsPassword);
        AddSettingRow("Vade Hatırlatma Şablonu:", _txtSmsTemplate, "Değişkenler: {Musteri}, {VadeTarihi}, {Tutar}, {ToplamBakiye}, {GecikmeGun}, {Firma}");

        var btnSaveSms = UITheme.CreateButton("💾 SMS Ayarlarını Kaydet", UITheme.Success, Color.White, (s, e) => SaveSmsConfig(), 200, 36);
        btnSaveSms.Top = rowTop + 10;
        btnSaveSms.Left = 210;

        var btnTestSms = UITheme.CreateButton("📲 Test SMS Gönder", UITheme.Primary, Color.White, async (s, e) => await SendTestSms(), 160, 36);
        btnTestSms.Top = rowTop + 10;
        btnTestSms.Left = btnSaveSms.Right + 12;

        pnlForm.Controls.Add(btnSaveSms);
        pnlForm.Controls.Add(btnTestSms);

        tab.Controls.Add(pnlForm);
        _tabControl.TabPages.Add(tab);
    }

    private void LoadData()
    {
        LoadCriticalStocks();
        LoadDueReceivables();
        LoadExpiringProducts();
        UpdateSummaryBadge();
    }

    private void UpdateSummaryBadge()
    {
        try
        {
            var counts = NotificationService.GetAlertCounts();
            _lblAlertSummary.Text = $"⚠️ Aktif Durumlar: {counts.CriticalStockCount} Kritik Stok | {counts.OverdueReceivableCount} Vadesi Geçmiş Veresiye | {counts.ExpiringProductCount} SKT Uyarısı";
            _lblAlertSummary.ForeColor = counts.TotalAlertCount > 0 ? Color.FromArgb(220, 38, 38) : UITheme.Success;
        }
        catch
        {
            _lblAlertSummary.Text = "⚠️ Bildirim özeti yüklenemedi.";
            _lblAlertSummary.ForeColor = UITheme.Danger;
        }
    }

    private void LoadExpiringProducts()
    {
        var list = NotificationService.GetExpiringProductAlerts(warningDays: 30);
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("Ürün Kodu", typeof(string));
        dt.Columns.Add("Barkod", typeof(string));
        dt.Columns.Add("Ürün Adı", typeof(string));
        dt.Columns.Add("Kategori", typeof(string));
        dt.Columns.Add("Mevcut Stok", typeof(double));
        dt.Columns.Add("Birim", typeof(string));
        dt.Columns.Add("SKT", typeof(string));
        dt.Columns.Add("Kalan Gün", typeof(int));
        dt.Columns.Add("Durum", typeof(string));
        dt.Columns.Add("Parti / Lot No", typeof(string));

        foreach (var item in list)
        {
            dt.Rows.Add(item.ProductId, item.Code, item.Barcode, item.Name, item.Category, item.CurrentStock, item.Unit, item.ExpiryDate, item.DaysRemaining, item.StatusText, item.BatchNumber);
        }

        _gridExpiring.DataSource = dt;
        if (_gridExpiring.Columns["Id"] is { } colExpId) colExpId.Visible = false;
        if (_gridExpiring.Columns["Mevcut Stok"] is { } colExpStock) colExpStock.DefaultCellStyle.Format = "N2";
    }

    private void GridExpiring_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridExpiring.Rows.Count) return;
        var row = _gridExpiring.Rows[e.RowIndex];

        int days = Convert.ToInt32(row.Cells["Kalan Gün"].Value ?? 0);
        if (days < 0)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226); // Açık kırmızı
            row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
        }
        else if (days <= 10)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199); // Açık sarı/turuncu
            row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
        }
    }

    private void EditSelectedExpiringProduct()
    {
        if (_gridExpiring.CurrentRow == null || _gridExpiring.CurrentRow.Index < 0)
        {
            MessageBox.Show("Lütfen ürün seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        long id = Convert.ToInt64(_gridExpiring.CurrentRow.Cells["Id"].Value);
        using var f = new ProductForm(id);
        if (f.ShowDialog() == DialogResult.OK)
        {
            LoadExpiringProducts();
            UpdateSummaryBadge();
        }
    }

    private void LoadCriticalStocks()
    {
        var list = NotificationService.GetCriticalStockAlerts();
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(long));
        dt.Columns.Add("Ürün Kodu", typeof(string));
        dt.Columns.Add("Ürün Adı", typeof(string));
        dt.Columns.Add("Kategori", typeof(string));
        dt.Columns.Add("Mevcut Stok", typeof(double));
        dt.Columns.Add("Kritik Eşik", typeof(double));
        dt.Columns.Add("İhtiyaç Miktarı", typeof(double));
        dt.Columns.Add("Birim", typeof(string));
        dt.Columns.Add("Durum", typeof(string));

        foreach (var item in list)
        {
            string durum = item.CurrentStock <= 0 ? "⛔ TÜKENDİ" : "⚠️ KRİTİK SEVİYE";
            dt.Rows.Add(item.ProductId, item.Code, item.Name, item.Category, item.CurrentStock, item.MinStockLevel, item.Shortage, item.Unit, durum);
        }

        _gridCriticalStock.DataSource = dt;
        if (_gridCriticalStock.Columns["Id"] is { } colCritId) colCritId.Visible = false;
        if (_gridCriticalStock.Columns["Mevcut Stok"] is { } colCritStock) colCritStock.DefaultCellStyle.Format = "N2";
        if (_gridCriticalStock.Columns["Kritik Eşik"] is { } colCritMin) colCritMin.DefaultCellStyle.Format = "N2";
        if (_gridCriticalStock.Columns["İhtiyaç Miktarı"] is { } colCritShort) colCritShort.DefaultCellStyle.Format = "N2";
    }

    private void GridCriticalStock_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridCriticalStock.Rows.Count) return;
        var row = _gridCriticalStock.Rows[e.RowIndex];

        double currentStock = Convert.ToDouble(row.Cells["Mevcut Stok"].Value ?? 0);
        if (currentStock <= 0)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
        }
        else
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
        }
    }

    private void LoadDueReceivables()
    {
        var list = NotificationService.GetDueReceivableAlerts(onlyOverdue: false);
        var dt = new DataTable();
        dt.Columns.Add("MovementId", typeof(long));
        dt.Columns.Add("AccountId", typeof(long));
        dt.Columns.Add("Müşteri", typeof(string));
        dt.Columns.Add("Telefon", typeof(string));
        dt.Columns.Add("Vade Tarihi", typeof(string));
        dt.Columns.Add("Veresiye Tutarı (₺)", typeof(double));
        dt.Columns.Add("Güncel Toplam Bakiye (₺)", typeof(double));
        dt.Columns.Add("Gecikme (Gün)", typeof(int));
        dt.Columns.Add("Durum", typeof(string));
        dt.Columns.Add("Açıklama", typeof(string));

        foreach (var item in list)
        {
            string durum = item.DaysOverdue > 0 ? $"⚠️ {item.DaysOverdue} Gün Gecikti" : (item.DaysOverdue == 0 ? "🔔 BUGÜN VADELİ" : $"{Math.Abs(item.DaysOverdue)} Gün Kaldı");
            dt.Rows.Add(item.MovementId, item.AccountId, item.CustomerName, item.Phone, item.DueDate, item.Amount, item.TotalBalance, item.DaysOverdue, durum, item.Note);
        }

        _gridDueReceivables.DataSource = dt;
        if (_gridDueReceivables.Columns["MovementId"] is { } colDueMov) colDueMov.Visible = false;
        if (_gridDueReceivables.Columns["AccountId"] is { } colDueAcc) colDueAcc.Visible = false;
        if (_gridDueReceivables.Columns["Veresiye Tutarı (₺)"] is { } colDueAmt) colDueAmt.DefaultCellStyle.Format = "N2";
        if (_gridDueReceivables.Columns["Güncel Toplam Bakiye (₺)"] is { } colDueBal) colDueBal.DefaultCellStyle.Format = "N2";
    }

    private void GridDueReceivables_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridDueReceivables.Rows.Count) return;
        var row = _gridDueReceivables.Rows[e.RowIndex];

        int days = Convert.ToInt32(row.Cells["Gecikme (Gün)"].Value ?? 0);
        if (days > 0)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 242, 242);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(153, 27, 27);
        }
        else if (days == 0)
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(146, 64, 14);
        }
        else
        {
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(22, 101, 52);
        }
    }

    private void SaveSmsConfig()
    {
        var cfg = new SmsSettings
        {
            Provider = _cmbSmsProvider.SelectedIndex == 1 ? "NetGSM" : (_cmbSmsProvider.SelectedIndex == 2 ? "IletiMerkezi" : "Demo"),
            Header = _txtSmsHeader.Text.Trim(),
            Username = _txtSmsUsername.Text.Trim(),
            Password = _txtSmsPassword.Text.Trim(),
            DueReminderTemplate = _txtSmsTemplate.Text.Trim()
        };
        NotificationService.SaveSmsSettings(cfg);
        MessageBox.Show("SMS yapılandırma ayarları başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SendTestSms()
    {
        string phone = PromptDialog.Show("Test SMS Gönderilecek Cep Telefonu:", "Test SMS", "05");
        if (string.IsNullOrWhiteSpace(phone)) return;

        var res = await NotificationService.SendSmsAsync(phone, "Bilensis SMS Sistemi Test Mesajıdır. Entegrasyon aktif!");
        if (res.Success)
            MessageBox.Show(res.Message, "SMS Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(res.Message, "SMS Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task SendSelectedSms()
    {
        if (_gridDueReceivables.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen SMS göndermek istediğiniz müşteriyi tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var row = _gridDueReceivables.SelectedRows[0];
        var item = new DueReceivableItem
        {
            CustomerName = row.Cells["Müşteri"].Value?.ToString() ?? "",
            Phone = row.Cells["Telefon"].Value?.ToString() ?? "",
            DueDate = row.Cells["Vade Tarihi"].Value?.ToString() ?? "",
            Amount = Convert.ToDouble(row.Cells["Veresiye Tutarı (₺)"].Value ?? 0),
            TotalBalance = Convert.ToDouble(row.Cells["Güncel Toplam Bakiye (₺)"].Value ?? 0),
            DaysOverdue = Convert.ToInt32(row.Cells["Gecikme (Gün)"].Value ?? 0)
        };

        if (string.IsNullOrWhiteSpace(item.Phone))
        {
            MessageBox.Show($"'{item.CustomerName}' müşterisine ait kayıtlı cep telefonu bulunamadı.", "Telefon Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string smsBody = NotificationService.BuildDueReminderText(item);
        if (MessageBox.Show($"Alıcı: {item.CustomerName} ({item.Phone})\n\nMesaj:\n{smsBody}\n\nSMS gönderilsin mi?", "SMS Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var res = await NotificationService.SendSmsAsync(item.Phone, smsBody);
        if (res.Success)
            MessageBox.Show(res.Message, "SMS Gönderildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(res.Message, "SMS Gönderilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private async Task SendBulkSms()
    {
        var overdueList = NotificationService.GetDueReceivableAlerts(onlyOverdue: true);
        if (overdueList.Count == 0)
        {
            MessageBox.Show("Vadesi geçmiş alacak bulunmamaktadır.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show($"Toplam {overdueList.Count} adet vadesi geçmiş müşteriye otomatik hatırlatma SMS'i gönderilecek. Onaylıyor musunuz?", "Toplu SMS Gönderimi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        int sent = 0;
        int failed = 0;

        foreach (var item in overdueList)
        {
            if (string.IsNullOrWhiteSpace(item.Phone))
            {
                failed++;
                continue;
            }

            string text = NotificationService.BuildDueReminderText(item);
            var res = await NotificationService.SendSmsAsync(item.Phone, text);
            if (res.Success) sent++;
            else failed++;
        }

        MessageBox.Show($"Toplu SMS işlemi tamamlandı!\nBaşarılı: {sent}\nBaşarısız/Telefonsuz: {failed}", "Sonuç", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SendCustomerWhatsApp()
    {
        if (_gridDueReceivables.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen tablodan bir kayıt seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var row = _gridDueReceivables.SelectedRows[0];
        string customerName = row.Cells["Müşteri"].Value?.ToString() ?? "";
        string phone = row.Cells["Telefon"].Value?.ToString() ?? "";
        string dueDate = row.Cells["Vade Tarihi"].Value?.ToString() ?? "";
        double amount = Convert.ToDouble(row.Cells["Veresiye Tutarı (₺)"].Value ?? 0);
        double balance = Convert.ToDouble(row.Cells["Güncel Toplam Bakiye (₺)"].Value ?? 0);
        int days = Convert.ToInt32(row.Cells["Gecikme (Gün)"].Value ?? 0);

        if (string.IsNullOrWhiteSpace(phone))
        {
            MessageBox.Show("Kayıtlı telefon numarası bulunamadı.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string msg = days > 0
            ? $"Sayın *{customerName}*,\n\n*{dueDate}* vadeli ({days} gün geciken) *{amount:N2} ₺* tutarındaki veresiye ödemenizi hatırlatırız.\n💰 Toplam Güncel Borcunuz: *{balance:N2} ₺*.\n\nİyi günler dileriz."
            : $"Sayın *{customerName}*,\n\n*{dueDate}* vadeli *{amount:N2} ₺* tutarındaki ödemenizi hatırlatır, iyi çalışmalar dileriz.";

        NotificationService.OpenWhatsAppChat(phone, msg);
    }

    private void SendSupplierOrderWhatsApp()
    {
        if (_gridCriticalStock.SelectedRows.Count == 0)
        {
            MessageBox.Show("Lütfen sipariş vermek istediğiniz kritik stoklu ürünü seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var row = _gridCriticalStock.SelectedRows[0];
        string pName = row.Cells["Ürün Adı"].Value?.ToString() ?? "";
        double cur = Convert.ToDouble(row.Cells["Mevcut Stok"].Value ?? 0);
        string unit = row.Cells["Birim"].Value?.ToString() ?? "Adet";

        string supplierPhone = PromptDialog.Show("Tedarikçi Cep Telefonu (veya boş bırakıp WhatsApp kişinizi seçin):", "Tedarikçi WhatsApp Siparişi", "05");
        if (supplierPhone == null) return;

        string msg = $"Merhaba Sayın Yetkili,\n\nStoklarımızda *{pName}* ürünümüz kritik seviyeye ({cur:N2} {unit}) gerilemiştir.\nAcil sipariş geçmek istiyoruz. Fiyat ve teslimat termin bilgisi rica ederiz.\n\nBilensis Yönetimi";
        NotificationService.OpenWhatsAppChat(supplierPhone, msg);
    }
}
