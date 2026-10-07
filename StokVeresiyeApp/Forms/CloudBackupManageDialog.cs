using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Krypton.Toolkit;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class CloudBackupManageDialog : BaseModernForm
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
    private readonly KryptonLabel _lblStatus = new();

    public CloudBackupManageDialog() : base("☁️ Google Drive & Gmail Otomatik Bulut Yedekleme Yönetimi", 860, 680)
    {
        BuildInterface();
        LoadConfigData();
        RefreshBackupsList();
    }

    private void BuildInterface()
    {
        // Standart Kaydet / İptal butonlarını ayarla
        BtnSave.Text = "💾 Ayarları Kaydet";
        BtnSave.Click += (s, e) => SaveSettings(showMessage: true);

        // TabControl ile Yedek Listesi ve Ayarları Ayır
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = UITheme.RegularFont
        };

        var tabBackups = new TabPage("📋 Mevcut Yedekler & Geri Yükleme") { BackColor = UITheme.CardBg, Padding = new Padding(12) };
        var tabSettings = new TabPage("⚙️ Google / Gmail & Yedekleme Ayarları") { BackColor = UITheme.CardBg, Padding = new Padding(16) };

        // ==========================================
        // 1. SEKME: MEVCUT YEDEKLER & GERİ YÜKLEME
        // ==========================================
        var pnlBackupActions = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(0, 0, 0, 10) };
        var btnTakeBackupNow = UITheme.CreateButton("💾 Hemen Bulut Yedeği Al", UITheme.Primary, Color.White, (s, e) => TakeBackupNow(), 180, 36);
        var btnRestoreSelected = UITheme.CreateButton("📂 Seçilen Yedeği Geri Yükle (Restore)", Color.FromArgb(220, 38, 38), Color.White, (s, e) => RestoreSelectedBackup(), 260, 36);
        var btnOpenFolder = UITheme.CreateButton("📁 Yedek Klasörünü Aç", UITheme.Secondary, Color.White, (s, e) => OpenBackupFolder(), 160, 36);
        var btnRefreshList = UITheme.CreateButton("🔄", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => RefreshBackupsList(), 40, 36);

        pnlBackupActions.Controls.Add(btnRefreshList);
        pnlBackupActions.Controls.Add(btnOpenFolder);
        pnlBackupActions.Controls.Add(btnRestoreSelected);
        pnlBackupActions.Controls.Add(btnTakeBackupNow);

        btnTakeBackupNow.Dock = DockStyle.Left;
        btnRestoreSelected.Dock = DockStyle.Left;
        btnRestoreSelected.Margin = new Padding(10, 0, 0, 0);
        btnOpenFolder.Dock = DockStyle.Left;
        btnRefreshList.Dock = DockStyle.Right;

        UITheme.ApplyGridStyle(_gridBackups);
        _gridBackups.Dock = DockStyle.Fill;
        _gridBackups.ReadOnly = true;
        _gridBackups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridBackups.MultiSelect = false;

        _lblStatus.Dock = DockStyle.Bottom;
        _lblStatus.Height = 28;
        _lblStatus.StateCommon.ShortText.Font = UITheme.SmallFont;
        _lblStatus.StateCommon.ShortText.Color1 = UITheme.TextSecondary;

        tabBackups.Controls.Add(_gridBackups);
        tabBackups.Controls.Add(pnlBackupActions);
        tabBackups.Controls.Add(_lblStatus);

        // ==========================================
        // 2. SEKME: GOOGLE / GMAIL AYARLARI
        // ==========================================
        var flowSettings = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblInfoBanner = new Label
        {
            Text = "ℹ️ Google Drive & Gmail Otomatik Bulut Yedekleme Sistemi:\nVeritabanı yedeğiniz her akşam Z Raporu kapandığında ve programdan çıkış yapıldığında otomatik olarak şifreli .zip formatında paketlenir. Google Drive senkronizasyon klasörüne sessizce aktarılır ve istenirse doğrudan Gmail posta kutunuza e-posta eki olarak arşivlenir.",
            Font = UITheme.RegularFont,
            ForeColor = UITheme.Primary,
            BackColor = Color.FromArgb(239, 246, 255),
            Padding = new Padding(12),
            Width = 780,
            Height = 85
        };

        _txtGmail.Width = 350;
        _txtGmail.CueHint.CueHintText = "ornek@gmail.com";
        _txtPassword.Width = 350;
        _txtPassword.CueHint.CueHintText = "Google Uygulama Şifresi (16 Haneli)";
        _txtTargetDir.Width = 550;

        var btnBrowse = UITheme.CreateButton("📁 Gözat", UITheme.Secondary, Color.White, (s, e) =>
        {
            using var fbd = new FolderBrowserDialog { SelectedPath = _txtTargetDir.Text };
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                _txtTargetDir.Text = fbd.SelectedPath;
            }
        }, 80, 32);

        var pnlDirRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        pnlDirRow.Controls.Add(_txtTargetDir);
        pnlDirRow.Controls.Add(btnBrowse);

        var btnTest = UITheme.CreateButton("🔌 Google / Gmail Bağlantısını Sına", Color.FromArgb(13, 148, 136), Color.White, (s, e) => TestConnection(), 260, 36);

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
            Margin = new Padding(0, 2, 0, 12) 
        });

        flowSettings.Controls.Add(btnTest);

        flowSettings.Controls.Add(new Label { Text = "Otomatik Yedekleme Seçenekleri:", Font = new Font(UITheme.RegularFont, FontStyle.Bold), Margin = new Padding(0, 20, 0, 8) });
        flowSettings.Controls.Add(_chkOnExit);
        flowSettings.Controls.Add(_chkOnClosing);
        flowSettings.Controls.Add(_chkDaily);
        flowSettings.Controls.Add(_chkSendGmail);

        tabSettings.Controls.Add(flowSettings);

        tabs.TabPages.Add(tabBackups);
        tabs.TabPages.Add(tabSettings);

        // BaseModernForm ContentTable yerine tabs kontrolünü ekle
        ContentTable.Visible = false;
        if (Controls.Find("actionPanel", true).FirstOrDefault() is Panel actionPanel)
        {
            var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            container.Controls.Add(tabs);
            Controls.Add(container);
            container.BringToFront();
        }
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
            MessageBox.Show("Bulut yedekleme yapılandırmanız başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void TestConnection()
    {
        string email = _txtGmail.Text.Trim();
        string pass = _txtPassword.Text.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            MessageBox.Show("Lütfen Gmail adresinizi ve uygulama şifrenizi giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            var res = CloudBackupService.TestGmailConnection(email, pass);
            MessageBox.Show(res.Message, res.Success ? "Bağlantı Başarılı" : "Bağlantı Hatası", MessageBoxButtons.OK, res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void TakeBackupNow()
    {
        SaveSettings(showMessage: false);

        Cursor = Cursors.WaitCursor;
        try
        {
            var res = CloudBackupService.ExecuteBackup(silent: false);
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Yedekleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshBackupsList();
            }
            else
            {
                MessageBox.Show(res.Message, "Yedekleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RefreshBackupsList()
    {
        var backups = CloudBackupService.GetExistingBackups();
        _gridBackups.DataSource = backups;

        if (_gridBackups.Columns["FullPath"] is { } colPath) colPath.Visible = false;
        if (_gridBackups.Columns["SizeMb"] is { } colSize) colSize.Visible = false;
        if (_gridBackups.Columns["CreatedAt"] is { } colCreated) colCreated.Visible = false;

        if (_gridBackups.Columns["FileName"] is { } colName)
        {
            colName.HeaderText = "Yedek Dosyası";
            colName.Width = 360;
        }
        if (_gridBackups.Columns["FormattedSize"] is { } colSizeFmt)
        {
            colSizeFmt.HeaderText = "Boyut";
            colSizeFmt.Width = 120;
            colSizeFmt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        if (_gridBackups.Columns["FormattedDate"] is { } colDateFmt)
        {
            colDateFmt.HeaderText = "Yedek Tarihi";
            colDateFmt.Width = 160;
        }

        _lblStatus.Text = $"Toplam {backups.Count} adet bulut/yerel yedek arşivi bulundu. Hedef Dizin: {CloudBackupService.GetBackupTargetDirectory()}";
    }

    private void RestoreSelectedBackup()
    {
        if (_gridBackups.CurrentRow?.DataBoundItem is not BackupFileInfo backup)
        {
            MessageBox.Show("Lütfen geri yüklemek istediğiniz yedeği tablodan seçiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"DİKKAT: Veritabanı '{backup.FileName}' isimli yedeğe geri döndürülecektir!\n\nSeçilen Yedeğin Tarihi: {backup.FormattedDate}\n\nBu işlem mevcut tüm anlık verilerin üzerine yazacaktır. Devam etmek istiyor musunuz?",
            "Geri Yükleme Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirm != DialogResult.Yes) return;

        Cursor = Cursors.WaitCursor;
        try
        {
            var res = CloudBackupService.RestoreBackup(backup.FullPath);
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Geri Yükleme Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(res.Message, "Geri Yükleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void OpenBackupFolder()
    {
        try
        {
            string dir = CloudBackupService.GetBackupTargetDirectory();
            if (Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Klasör açılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
