using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Krypton.Toolkit;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class CloudBackupManageDialog : KryptonForm
{
    // Ayarlar Sekmesi Kontrolleri
    private readonly KryptonTextBox _txtGmail = new();
    private readonly KryptonTextBox _txtPassword = new() { PasswordChar = '●' };
    private readonly KryptonTextBox _txtTargetDir = new();
    private readonly KryptonCheckBox _chkOnExit = new() { Text = "🔒 Program Her Kapandığında Otomatik Bulut Yedeği Al (Tavsiye Edilir)", AutoSize = true };
    private readonly KryptonCheckBox _chkOnClosing = new() { Text = "🔒 Kasa Gün Sonu (Z Raporu) Kapandığında Otomatik Yedek Al", AutoSize = true };
    private readonly KryptonCheckBox _chkDaily = new() { Text = "📅 Günlük Otomatik Arka Plan Yedeği Al", AutoSize = true };
    private readonly KryptonCheckBox _chkSendGmail = new() { Text = "✉️ Alınan .zip Yedeği Gmail Kutusuna Güvenli Ek Olarak İlet", AutoSize = true };

    // Yedek Listesi Kontrolleri
    private readonly KryptonDataGridView _gridBackups = new();
    private readonly Label _lblStatus = new();

    public CloudBackupManageDialog()
    {
        Text = "☁️ Google Drive & Gmail Otomatik Bulut Yedekleme Yönetimi";
        ClientSize = new Size(880, 640);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        BuildInterface();
        LoadConfigData();
        RefreshBackupsList();
    }

    private void BuildInterface()
    {
        // 1. Üst Başlık Paneli
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitle = new Label
        {
            Text = "☁️ Google Drive & Gmail Otomatik Bulut Yedekleme",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 28
        };

        var lblSub = new Label
        {
            Text = "SQL veritabanı yedeklerinizi Google Drive ve Gmail bulutunda güvenle saklayın, dilediğiniz zaman tek tıkla geri yükleyin.",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Fill
        };

        headerPanel.Controls.Add(lblSub);
        headerPanel.Controls.Add(lblTitle);

        // 2. Alt İşlem Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = UITheme.CardBg,
            Padding = new Padding(20, 14, 20, 14)
        };

        var lblHint = new Label
        {
            Text = "💡 İpucu: Google Drive kurulu ise yedekleriniz anında buluta senkronize edilir.",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextMuted,
            Dock = DockStyle.Left,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        var btnClose = UITheme.CreateKryptonButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 90, 36);
        btnClose.Dock = DockStyle.Right;

        var btnSave = UITheme.CreateKryptonButton("💾 Ayarları Kaydet", UITheme.Primary, Color.White, (s, e) => SaveSettings(showMessage: true), 160, 36);
        btnSave.Dock = DockStyle.Right;

        bottomPanel.Controls.Add(lblHint);
        bottomPanel.Controls.Add(btnClose);
        bottomPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
        bottomPanel.Controls.Add(btnSave);

        // 3. Orta TabControl Paneli
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            Padding = new Point(14, 8)
        };

        var tabBackups = new TabPage("📋 Mevcut Yedekler & Geri Yükleme") { BackColor = UITheme.Background, Padding = new Padding(15) };
        var tabSettings = new TabPage("⚙️ Google / Gmail & Yedekleme Ayarları") { BackColor = UITheme.Background, Padding = new Padding(20) };

        // ==========================================
        // 1. SEKME: MEVCUT YEDEKLER & GERİ YÜKLEME
        // ==========================================
        var pnlBackupActions = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 0, 0, 10) };
        var btnTakeBackupNow = UITheme.CreateButton("💾 Hemen Bulut Yedeği Al", UITheme.Primary, Color.White, (s, e) => TakeBackupNow(), 185, 36);
        var btnRestoreSelected = UITheme.CreateButton("📂 Seçilen Yedeği Geri Yükle", Color.FromArgb(220, 38, 38), Color.White, (s, e) => RestoreSelectedBackup(), 210, 36);
        var btnOpenFolder = UITheme.CreateButton("📁 Klasörü Aç", UITheme.Secondary, Color.White, (s, e) => OpenBackupFolder(), 130, 36);
        var btnRefreshList = UITheme.CreateButton("🔄 Yenile", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshBackupsList(), 90, 36);

        var flowActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        flowActions.Controls.Add(btnTakeBackupNow);
        flowActions.Controls.Add(btnRestoreSelected);
        flowActions.Controls.Add(btnOpenFolder);
        flowActions.Controls.Add(btnRefreshList);
        pnlBackupActions.Controls.Add(flowActions);

        var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        UITheme.ApplyGridStyle(_gridBackups);
        _gridBackups.Dock = DockStyle.Fill;
        _gridBackups.ReadOnly = true;
        _gridBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridBackups.MultiSelect = false;
        _gridBackups.RowTemplate.Height = 32;
        gridCard.Controls.Add(_gridBackups);

        _lblStatus.Dock = DockStyle.Bottom;
        _lblStatus.Height = 28;
        _lblStatus.Font = UITheme.SmallFont;
        _lblStatus.ForeColor = UITheme.TextSecondary;
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        tabBackups.Controls.Add(gridCard);
        tabBackups.Controls.Add(pnlBackupActions);
        tabBackups.Controls.Add(_lblStatus);

        // ==========================================
        // 2. SEKME: GOOGLE / GMAIL AYARLARI
        // ==========================================
        var scrollSettings = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var flowSettings = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 20)
        };

        var lblInfoBanner = new Label
        {
            Text = "ℹ️ Google Drive & Gmail Otomatik Bulut Yedekleme Sistemi:\nVeritabanı yedeğiniz her akşam Z Raporu kapandığında ve programdan çıkış yapıldığında otomatik olarak şifreli .zip formatında paketlenir. Google Drive senkronizasyon klasörüne sessizce aktarılır ve istenirse doğrudan Gmail posta kutunuza e-posta eki olarak arşivlenir.",
            Font = UITheme.RegularFont,
            ForeColor = Color.FromArgb(30, 64, 175),
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(12),
            Width = 780,
            Height = 85
        };

        _txtTargetDir.Width = 560;
        _txtGmail.Width = 400;
        _txtPassword.Width = 400;

        var btnBrowse = UITheme.CreateButton("📁 Gözat", UITheme.Secondary, Color.White, (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _txtTargetDir.Text };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                _txtTargetDir.Text = fbd.SelectedPath;
            }
        }, 90, 32);

        var pnlDirRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlDirRow.Controls.Add(_txtTargetDir);
        pnlDirRow.Controls.Add(btnBrowse);

        var btnTest = UITheme.CreateButton("🔌 Google / Gmail Bağlantısını Sına", Color.FromArgb(13, 148, 136), Color.White, (s, e) => TestConnection(), 280, 36);

        flowSettings.Controls.Add(lblInfoBanner);
        flowSettings.Controls.Add(new Label { Text = "Hedef Bulut / Drive Klasörü Konumu:", Font = new Font(UITheme.RegularFont, FontStyle.Bold), Margin = new Padding(0, 15, 0, 4) });
        flowSettings.Controls.Add(pnlDirRow);

        flowSettings.Controls.Add(new Label { Text = "Google / Gmail Kullanıcı Adı (E-posta):", Font = new Font(UITheme.RegularFont, FontStyle.Bold), Margin = new Padding(0, 15, 0, 4) });
        flowSettings.Controls.Add(_txtGmail);

        flowSettings.Controls.Add(new Label { Text = "Google Uygulama Şifresi (App Password):", Font = new Font(UITheme.RegularFont, FontStyle.Bold), Margin = new Padding(0, 10, 0, 4) });
        flowSettings.Controls.Add(_txtPassword);

        flowSettings.Controls.Add(new Label 
        { 
            Text = "💡 İpucu: Google hesabınızda '2 Adımlı Doğrulama' açık ise, myaccount.google.com > Güvenlik > Uygulama Şifreleri bölümünden alacağınız 16 haneli şifreyi giriniz.", 
            Font = UITheme.SmallFont, 
            ForeColor = UITheme.TextMuted,
            Width = 750,
            Margin = new Padding(0, 4, 0, 12) 
        });

        flowSettings.Controls.Add(btnTest);

        flowSettings.Controls.Add(new Label { Text = "Otomatik Yedekleme Seçenekleri:", Font = new Font(UITheme.RegularFont, FontStyle.Bold), Margin = new Padding(0, 20, 0, 8) });
        flowSettings.Controls.Add(_chkOnExit);
        flowSettings.Controls.Add(_chkOnClosing);
        flowSettings.Controls.Add(_chkDaily);
        flowSettings.Controls.Add(_chkSendGmail);

        scrollSettings.Controls.Add(flowSettings);
        tabSettings.Controls.Add(scrollSettings);

        tabs.TabPages.Add(tabBackups);
        tabs.TabPages.Add(tabSettings);

        var centerPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
        centerPanel.Controls.Add(tabs);

        Controls.Add(centerPanel);
        Controls.Add(bottomPanel);
        Controls.Add(headerPanel);

        Controls.SetChildIndex(headerPanel, 0);
        Controls.SetChildIndex(bottomPanel, 1);
        Controls.SetChildIndex(centerPanel, 2);
        centerPanel.BringToFront();
    }

    private void LoadConfigData()
    {
        var cfg = CloudBackupService.Config;
        _txtGmail.Text = cfg.GmailAddress;
        _txtPassword.Text = cfg.GmailAppPassword;
        _txtTargetDir.Text = CloudBackupService.GetBackupTargetDirectory();
        _chkOnExit.Checked = cfg.BackupOnExit;
        _chkOnClosing.Checked = cfg.BackupOnClosing;
        _chkDaily.Checked = cfg.DailyBackupEnabled;
        _chkSendGmail.Checked = cfg.SendToGmail;
    }

    private void SaveSettings(bool showMessage)
    {
        var cfg = CloudBackupService.Config;
        cfg.GmailAddress = _txtGmail.Text.Trim();
        cfg.GmailAppPassword = _txtPassword.Text.Trim();
        cfg.TargetDirectory = _txtTargetDir.Text.Trim();
        cfg.BackupOnExit = _chkOnExit.Checked;
        cfg.BackupOnClosing = _chkOnClosing.Checked;
        cfg.DailyBackupEnabled = _chkDaily.Checked;
        cfg.SendToGmail = _chkSendGmail.Checked;

        CloudBackupService.SaveConfig();

        if (showMessage)
        {
            MessageBox.Show("Bulut yedekleme ve Gmail ayarlarınız başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void TestConnection()
    {
        string email = _txtGmail.Text.Trim();
        string pass = _txtPassword.Text.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            MessageBox.Show("Lütfen test için Gmail e-posta adresinizi ve uygulama şifrenizi giriniz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            var res = await Task.Run(() => CloudBackupService.TestGmailConnection(email, pass));
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Bağlantı Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(res.Message, "Bağlantı Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async void TakeBackupNow()
    {
        SaveSettings(showMessage: false);

        Cursor = Cursors.WaitCursor;
        try
        {
            var result = await Task.Run(() => CloudBackupService.ExecuteBackup(silent: false));
            RefreshBackupsList();
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async void RestoreSelectedBackup()
    {
        if (_gridBackups.CurrentRow == null)
        {
            MessageBox.Show("Lütfen geri yüklemek istediğiniz yedek dosyasını tablodan seçiniz.", "Yedek Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string? fullPath = _gridBackups.CurrentRow.Cells["Dosya Yolu"]?.Value?.ToString();
        string? fileName = _gridBackups.CurrentRow.Cells["Dosya Adı"]?.Value?.ToString();

        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
        {
            MessageBox.Show("Seçilen yedek dosyası diskte bulunamadı.", "Dosya Yok", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var confirm = MessageBox.Show(
            $"DİKKAT! Seçilen yedeği geri yüklemek üzeresiniz:\n\nDosya: {fileName}\n\nBu işlem mevcut veritabanının üzerine seçilen yedeği yazacaktır. Devam etmek istiyor musunuz?",
            "Veritabanı Geri Yükleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirm != DialogResult.Yes) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            var result = await Task.Run(() => CloudBackupService.RestoreBackup(fullPath));
            if (result.Success)
            {
                MessageBox.Show(result.Message, "Geri Yükleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(result.Message, "Geri Yükleme Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OpenBackupFolder()
    {
        string dir = CloudBackupService.GetBackupTargetDirectory();
        if (Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        else
        {
            MessageBox.Show($"Klasör henüz oluşturulmamış:\n{dir}", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void RefreshBackupsList()
    {
        try
        {
            var backups = CloudBackupService.GetExistingBackups();

            var dt = new System.Data.DataTable();
            dt.Columns.Add("Dosya Adı");
            dt.Columns.Add("Boyut");
            dt.Columns.Add("Yedek Tarihi");
            dt.Columns.Add("Dosya Yolu");

            foreach (var b in backups)
            {
                dt.Rows.Add(b.FileName, b.FormattedSize, b.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss"), b.FullPath);
            }

            _gridBackups.DataSource = dt;

            if (_gridBackups.Columns.Contains("Dosya Yolu"))
            {
                _gridBackups.Columns["Dosya Yolu"].Visible = false;
            }

            if (_gridBackups.Columns.Contains("Dosya Adı"))
            {
                _gridBackups.Columns["Dosya Adı"].FillWeight = 45;
            }
            if (_gridBackups.Columns.Contains("Boyut"))
            {
                _gridBackups.Columns["Boyut"].FillWeight = 20;
            }
            if (_gridBackups.Columns.Contains("Yedek Tarihi"))
            {
                _gridBackups.Columns["Yedek Tarihi"].FillWeight = 35;
            }

            _lblStatus.Text = $"Toplam {backups.Count} adet arşivlenmiş yedek listelendi. Klasör: {CloudBackupService.GetBackupTargetDirectory()}";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Yedekler listelenirken hata: {ex.Message}";
        }
    }
}
