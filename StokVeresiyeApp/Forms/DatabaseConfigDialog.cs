using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;

namespace StokVeresiyeApp.Forms;

public class DatabaseConfigDialog : Form
{
    private TextBox _txtServer = new();
    private TextBox _txtDatabase = new();
    private RadioButton _rbWindowsAuth = new() { Text = "Windows Kimlik Doğrulaması (Önerilen)", Checked = true, AutoSize = true };
    private RadioButton _rbSqlAuth = new() { Text = "SQL Server Kimlik Doğrulaması (Kullanıcı & Şifre)", AutoSize = true };
    private TextBox _txtUser = new();
    private TextBox _txtPassword = new();
    private Label _lblStatus = new();

    public DatabaseConfigDialog()
    {
        Text = "Bilensis - SQL Server Veritabanı Bağlantı Ayarları";
        Width = 560;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;
        Icon = AppResources.AppIcon;

        BuildUI();
        LoadCurrentConfig();
    }

    private void BuildUI()
    {
        // 1. Header
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 12, 20, 12)
        };
        var lblTitle = new Label
        {
            Text = "🗄️ SQL Server Veritabanı Yapılandırması",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 26
        };
        var lblSub = new Label
        {
            Text = "Microsoft SQL Server bağlantı parametrelerini belirleyin ve test edin.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Bottom,
            Height = 18
        };
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        // 2. Bottom
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        var btnCancel = UITheme.CreateButton("İptal", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 90, 34);
        btnCancel.Dock = DockStyle.Right;

        var btnSave = UITheme.CreateButton("💾 Kaydet ve Bağlan", UITheme.Primary, Color.White, SaveClick, 160, 34);
        btnSave.Dock = DockStyle.Right;

        var btnTest = UITheme.CreateButton("⚡ Bağlantıyı Test Et", UITheme.Success, Color.White, TestClick, 160, 34);
        btnTest.Dock = DockStyle.Left;

        bottom.Controls.Add(btnCancel);
        bottom.Controls.Add(btnSave);
        bottom.Controls.Add(btnTest);
        Controls.Add(bottom);

        // 3. Content
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16),
            AutoScroll = true
        };

        int top = 8;

        // Server
        var lblServer = new Label { Text = "SQL Sunucu Adı veya Adresi (Örn: (localdb)\\MSSQLLocalDB, localhost, .\\SQLEXPRESS):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _txtServer.Left = 0;
        _txtServer.Top = top;
        _txtServer.Width = 490;
        _txtServer.Height = 28;
        content.Controls.Add(lblServer);
        content.Controls.Add(_txtServer);

        top += 38;

        // Database
        var lblDb = new Label { Text = "Veritabanı Adı (Otomatik Oluşturulur):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _txtDatabase.Left = 0;
        _txtDatabase.Top = top;
        _txtDatabase.Width = 490;
        _txtDatabase.Height = 28;
        _txtDatabase.Text = "BilgeStokDB";
        content.Controls.Add(lblDb);
        content.Controls.Add(_txtDatabase);

        top += 42;

        // Auth type
        var lblAuth = new Label { Text = "Kimlik Doğrulama Yöntemi:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _rbWindowsAuth.Left = 0;
        _rbWindowsAuth.Top = top;
        _rbWindowsAuth.CheckedChanged += (s, e) => ToggleAuth();

        _rbSqlAuth.Left = 250;
        _rbSqlAuth.Top = top;
        _rbSqlAuth.CheckedChanged += (s, e) => ToggleAuth();

        content.Controls.Add(lblAuth);
        content.Controls.Add(_rbWindowsAuth);
        content.Controls.Add(_rbSqlAuth);

        top += 34;

        // User & Pass
        var lblUser = new Label { Text = "Kullanıcı Adı (sa):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 235, Height = 20 };
        var lblPass = new Label { Text = "Şifre:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 250, Top = top, Width = 235, Height = 20 };
        top += 22;

        _txtUser.Left = 0;
        _txtUser.Top = top;
        _txtUser.Width = 235;
        _txtUser.Height = 28;
        _txtUser.Enabled = false;

        _txtPassword.Left = 250;
        _txtPassword.Top = top;
        _txtPassword.Width = 235;
        _txtPassword.Height = 28;
        _txtPassword.UseSystemPasswordChar = true;
        _txtPassword.Enabled = false;

        content.Controls.Add(lblUser);
        content.Controls.Add(_txtUser);
        content.Controls.Add(lblPass);
        content.Controls.Add(_txtPassword);

        top += 44;

        _lblStatus.Left = 0;
        _lblStatus.Top = top;
        _lblStatus.Width = 490;
        _lblStatus.Height = 35;
        _lblStatus.Font = UITheme.SmallFont;
        content.Controls.Add(_lblStatus);

        Controls.Add(content);
        Controls.SetChildIndex(header, 0);
        Controls.SetChildIndex(bottom, 1);
        Controls.SetChildIndex(content, 2);
        content.BringToFront();
    }

    private void ToggleAuth()
    {
        bool sql = _rbSqlAuth.Checked;
        _txtUser.Enabled = sql;
        _txtPassword.Enabled = sql;
    }

    private void LoadCurrentConfig()
    {
        var cfg = Database.Config;
        _txtServer.Text = cfg.Server;
        _txtDatabase.Text = cfg.DatabaseName;
        _rbWindowsAuth.Checked = cfg.IntegratedSecurity;
        _rbSqlAuth.Checked = !cfg.IntegratedSecurity;
        _txtUser.Text = cfg.UserId ?? "";
        _txtPassword.Text = cfg.Password ?? "";
        ToggleAuth();
    }

    private DatabaseConfig BuildConfigFromUi()
    {
        return new DatabaseConfig
        {
            Server = _txtServer.Text.Trim(),
            DatabaseName = _txtDatabase.Text.Trim(),
            IntegratedSecurity = _rbWindowsAuth.Checked,
            UserId = _rbSqlAuth.Checked ? _txtUser.Text.Trim() : null,
            Password = _rbSqlAuth.Checked ? _txtPassword.Text : null
        };
    }

    private void TestClick(object? sender, EventArgs e)
    {
        _lblStatus.ForeColor = UITheme.Primary;
        _lblStatus.Text = "Sunucuya bağlanılıyor...";
        Application.DoEvents();

        var cfg = BuildConfigFromUi();
        try
        {
            Database.EnsureLocalDbStarted(force: true);
            string cs = cfg.BuildConnectionString("master");
            using var conn = new SqlConnection(cs);
            conn.Open();
            _lblStatus.ForeColor = UITheme.Success;
            _lblStatus.Text = "✅ SQL Server bağlantısı başarılı! Sunucu hazır ve erişilebilir.";
            MessageBox.Show("SQL Server bağlantısı başarıyla sağlandı!", "Bağlantı Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = UITheme.Danger;
            _lblStatus.Text = "❌ Bağlantı hatası: " + ex.Message;
            MessageBox.Show("SQL Server bağlantısı kurulamadı:\n\n" + ex.Message, "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveClick(object? sender, EventArgs e)
    {
        var cfg = BuildConfigFromUi();
        if (string.IsNullOrWhiteSpace(cfg.Server))
        {
            MessageBox.Show("Lütfen sunucu adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Database.Config.Server = cfg.Server;
            Database.Config.DatabaseName = cfg.DatabaseName;
            Database.Config.IntegratedSecurity = cfg.IntegratedSecurity;
            Database.Config.UserId = cfg.UserId;
            Database.Config.Password = cfg.Password;
            Database.SaveConfig();
            Database.Initialize();

            MessageBox.Show("SQL Server veritabanı ayarları kaydedildi ve veritabanı başlatıldı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Veritabanı başlatılırken hata: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
