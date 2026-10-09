using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class LoginForm : Form
{
    private readonly TextBox _txtUsername = new();
    private readonly TextBox _txtPassword = new();
    private readonly CheckBox _chkShowPass = new();
    private readonly CheckBox _chkRememberMe = new();
    private readonly Label _lblError = new();
    private readonly Label _lblLicenseInfo = new();

    public LoginForm()
    {
        Text = "BİLENSİS - Kurumsal Giriş";
        Size = new Size(820, 520);
        MinimumSize = new Size(820, 520);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        Icon = AppResources.AppIcon;

        BuildCorporateUI();
    }

    private void BuildCorporateUI()
    {
        Controls.Clear();

        // ===============================================================
        // ANA KAPSAYICI: 2 Sütunlu TableLayoutPanel (Sol ve Sağ Asla Çakışmaz!)
        // ===============================================================
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f)); // Sol Panel (Sabit 320px)
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));   // Sağ Panel (Kalan Alan)
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        // ===============================================================
        // 1. SOL PANEL: Kurumsal Marka & Bilgi Paneli (320px)
        // ===============================================================
        var pnlLeft = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(24, 28, 24, 20)
        };
        pnlLeft.Paint += (s, e) =>
        {
            using var brush = new LinearGradientBrush(
                pnlLeft.ClientRectangle,
                Color.FromArgb(15, 23, 42),
                Color.FromArgb(30, 58, 138),
                LinearGradientMode.ForwardDiagonal
            );
            e.Graphics.FillRectangle(brush, pnlLeft.ClientRectangle);

            using var penGlow = new Pen(Color.FromArgb(22, 255, 255, 255), 1.5f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawEllipse(penGlow, -40, -40, 200, 200);
            e.Graphics.DrawEllipse(penGlow, 170, 250, 250, 250);
        };

        var leftTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        leftTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f)); // 0: Logo & Marka Başlığı
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f)); // 1: Alt Başlık
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 16f)); // 2: Ayraç
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // 3: Madde 1
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // 4: Madde 2
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // 5: Madde 3
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // 6: Madde 4
        leftTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 7: Esnek boşluk
        leftTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f)); // 8: Güvenlik rozeti

        // Başlık Paneli (Logo + BİLENSİS)
        var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };
        var picLogo = new PictureBox
        {
            Size = new Size(40, 40),
            Location = new Point(0, 4),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = RibbonIconFactory.CreateIcon("bilensis_logo", 40),
            BackColor = Color.Transparent
        };
        var lblBrand = new Label
        {
            Text = "BİLENSİS",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(48, 4),
            Size = new Size(220, 38),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };
        pnlHeader.Controls.Add(picLogo);
        pnlHeader.Controls.Add(lblBrand);
        leftTable.Controls.Add(pnlHeader, 0, 0);

        // Alt Başlık
        var lblSubBrand = new Label
        {
            Text = "Stok, Satış & Finans Yönetimi",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(147, 197, 253),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        leftTable.Controls.Add(lblSubBrand, 0, 1);

        // Ayraç Çizgi
        var pnlSep = new Panel
        {
            Height = 1,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(40, 255, 255, 255),
            Margin = new Padding(0, 6, 0, 6)
        };
        leftTable.Controls.Add(pnlSep, 0, 2);

        // Özellik Maddeleri
        string[] features = new[]
        {
            "⚡ Barkodlu POS & Hızlı Satış",
            "📦 Çoklu Depo, Parti & Stok Takibi",
            "👥 Cari Hesap & Veresiye Risk",
            "📄 E-Fatura & Kasa Raporları"
        };

        for (int i = 0; i < features.Length; i++)
        {
            var pnlFeat = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 255, 255, 255),
                Margin = new Padding(0, 2, 0, 2),
                Padding = new Padding(8, 0, 6, 0)
            };
            var lblFeat = new Label
            {
                Text = features[i],
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 245, 249),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            pnlFeat.Controls.Add(lblFeat);
            leftTable.Controls.Add(pnlFeat, 0, 3 + i);
        }

        // Esnek Boşluk (Hücre 7 boş bırakılır)
        var pnlSpacerLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        leftTable.Controls.Add(pnlSpacerLeft, 0, 7);

        // Alt Rozet (Güvenlik & Sürüm)
        var lblSecurity = new Label
        {
            Text = "● 256-Bit Güvenli Oturum  •  v2.6",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        leftTable.Controls.Add(lblSecurity, 0, 8);

        pnlLeft.Controls.Add(leftTable);
        rootLayout.Controls.Add(pnlLeft, 0, 0);

        // ===============================================================
        // 2. SAĞ PANEL: Giriş Alanı (İç İçe Geçmeyi Engelleyen Net Tablo)
        // ===============================================================
        var pnlRight = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(0),
            Padding = new Padding(44, 28, 44, 20)
        };

        var rightTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        rightTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f)); // 0: Hoş Geldiniz
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f)); // 1: Alt açıklama
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 14f)); // 2: Boşluk
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f)); // 3: Kullanıcı Adı Label
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f)); // 4: _txtUsername
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f)); // 5: Şifre Label
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f)); // 6: _txtPassword
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f)); // 7: Seçenekler (Beni Hatırla / Şifre Göster)
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f)); // 8: _lblError
        rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f)); // 9: btnLogin
        rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 10: Alt Kısım (SQL & Lisans)

        // Satır 0: Başlık
        var lblTitle = new Label
        {
            Text = "Hoş Geldiniz 👋",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0)
        };
        rightTable.Controls.Add(lblTitle, 0, 0);

        // Satır 1: Açıklama
        var lblDesc = new Label
        {
            Text = "Devam etmek için kullanıcı bilgilerinizi giriniz.",
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0)
        };
        rightTable.Controls.Add(lblDesc, 0, 1);

        // Satır 2: Boşluk
        var pnlSpacerTop = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        rightTable.Controls.Add(pnlSpacerTop, 0, 2);

        // Satır 3: Kullanıcı Adı Etiketi
        var lblUser = new Label
        {
            Text = "Kullanıcı Adı:",
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0)
        };
        rightTable.Controls.Add(lblUser, 0, 3);

        // Satır 4: _txtUsername
        _txtUsername.Dock = DockStyle.Fill;
        _txtUsername.Margin = new Padding(0);
        _txtUsername.Font = new Font("Segoe UI", 10f);
        _txtUsername.PlaceholderText = "Kullanıcı adınızı girin";
        rightTable.Controls.Add(_txtUsername, 0, 4);

        // Satır 5: Şifre Etiketi
        var lblPass = new Label
        {
            Text = "Giriş Şifresi:",
            Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0)
        };
        rightTable.Controls.Add(lblPass, 0, 5);

        // Satır 6: _txtPassword
        _txtPassword.Dock = DockStyle.Fill;
        _txtPassword.Margin = new Padding(0);
        _txtPassword.PasswordChar = '●';
        _txtPassword.Font = new Font("Segoe UI", 10f);
        _txtPassword.PlaceholderText = "Şifrenizi girin";
        rightTable.Controls.Add(_txtPassword, 0, 6);

        // Satır 7: Seçenekler (Şifre Göster & Beni Hatırla) - 2 Sütunlu Grid
        var optGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        optGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        optGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        optGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _chkShowPass.Text = "Şifreyi Göster";
        _chkShowPass.Dock = DockStyle.Fill;
        _chkShowPass.Font = new Font("Segoe UI", 8.75f);
        _chkShowPass.ForeColor = Color.FromArgb(71, 85, 105);
        _chkShowPass.Cursor = Cursors.Hand;
        _chkShowPass.Margin = new Padding(0);
        _chkShowPass.CheckedChanged += (s, e) => _txtPassword.PasswordChar = _chkShowPass.Checked ? '\0' : '●';

        _chkRememberMe.Text = "Beni Hatırla";
        _chkRememberMe.Dock = DockStyle.Fill;
        _chkRememberMe.CheckAlign = ContentAlignment.MiddleRight;
        _chkRememberMe.TextAlign = ContentAlignment.MiddleRight;
        _chkRememberMe.Font = new Font("Segoe UI", 8.75f);
        _chkRememberMe.ForeColor = Color.FromArgb(71, 85, 105);
        _chkRememberMe.Checked = true;
        _chkRememberMe.Cursor = Cursors.Hand;
        _chkRememberMe.Margin = new Padding(0);

        optGrid.Controls.Add(_chkShowPass, 0, 0);
        optGrid.Controls.Add(_chkRememberMe, 1, 0);
        rightTable.Controls.Add(optGrid, 0, 7);

        // Satır 8: Hata Mesajı
        _lblError.Dock = DockStyle.Fill;
        _lblError.ForeColor = Color.FromArgb(220, 38, 38);
        _lblError.Font = new Font("Segoe UI", 8.75f, FontStyle.Bold);
        _lblError.TextAlign = ContentAlignment.MiddleCenter;
        _lblError.Margin = new Padding(0);
        rightTable.Controls.Add(_lblError, 0, 8);

        // Satır 9: Giriş Butonu
        var btnLogin = new Button
        {
            Text = "GİRİŞ YAP  ➔",
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        btnLogin.BackColor = UITheme.Primary;
        btnLogin.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        btnLogin.ForeColor = Color.White;

        btnLogin.Click += LoginClick;
        rightTable.Controls.Add(btnLogin, 0, 9);
        AcceptButton = btnLogin;

        // Satır 10: Alt Panel (SQL Butonu & Lisans Rozeti) - 2 Sütunlu Grid
        var bottomGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160f)); // SQL butonu
        bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));   // Lisans Rozeti
        bottomGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var btnDbConfig = new Button
        {
            Text = "⚙️ SQL Ayarları",
            Dock = DockStyle.Fill,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.FromArgb(241, 245, 249),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 6)
        };
        btnDbConfig.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnDbConfig.Click += (s, e) =>
        {
            using var dlg = new DatabaseConfigDialog();
            dlg.ShowDialog(this);
        };

        _lblLicenseInfo.Dock = DockStyle.Fill;
        _lblLicenseInfo.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
        _lblLicenseInfo.TextAlign = ContentAlignment.MiddleRight;
        _lblLicenseInfo.Margin = new Padding(6, 0, 0, 0);
        UpdateLicenseLabel();

        bottomGrid.Controls.Add(btnDbConfig, 0, 0);
        bottomGrid.Controls.Add(_lblLicenseInfo, 1, 0);

        rightTable.Controls.Add(bottomGrid, 0, 10);

        pnlRight.Controls.Add(rightTable);
        rootLayout.Controls.Add(pnlRight, 1, 0);

        // Form'a sadece rootLayout eklenir!
        Controls.Add(rootLayout);

        // Kayıtlı kullanıcı adı varsa doldur
        LoadSavedUser();
    }

    private void LoadSavedUser()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Bilensis");
            string? saved = key?.GetValue("LastUsername")?.ToString();
            if (!string.IsNullOrWhiteSpace(saved))
            {
                _txtUsername.Text = saved;
                _chkRememberMe.Checked = true;
                _txtPassword.Focus();
            }
            else
            {
                _txtUsername.Focus();
            }
        }
        catch { }
    }

    private void UpdateLicenseLabel()
    {
        try
        {
            var lic = LicenseService.CurrentLicense;
            if (lic.IsValid)
            {
                _lblLicenseInfo.ForeColor = Color.FromArgb(5, 150, 105);
                _lblLicenseInfo.Text = $"🛡️ Lisans Aktif ({lic.DaysRemaining} Gün)\n{lic.LicensedTo}";
            }
            else
            {
                _lblLicenseInfo.ForeColor = Color.FromArgb(220, 38, 38);
                _lblLicenseInfo.Text = "⚠️ Lisans Süresi Dolmuş";
            }
        }
        catch
        {
            _lblLicenseInfo.Text = "🛡️ Lisans Kontrol Ediliyor...";
        }
    }

    private void LoginClick(object? sender, EventArgs e)
    {
        _lblError.Text = string.Empty;

        // Lisans kontrolü
        var lic = LicenseService.CheckLicenseStatus();
        if (!lic.IsValid)
        {
            _lblError.Text = "Sistem lisansınız geçersiz veya süresi dolmuştur!";
            using var licForm = new LicenseActivationForm(lic.StatusMessage);
            if (licForm.ShowDialog(this) == DialogResult.OK)
            {
                UpdateLicenseLabel();
            }
            return;
        }

        string username = _txtUsername.Text.Trim();
        string password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "Lütfen kullanıcı adı ve şifrenizi giriniz.";
            return;
        }

        // Doğrulama
        var auth = UserService.Authenticate(username, password);
        if (auth.Success && auth.User != null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Bilensis");
                if (_chkRememberMe.Checked)
                {
                    key?.SetValue("LastUsername", username);
                }
                else
                {
                    key?.DeleteValue("LastUsername", false);
                }
            }
            catch { }

            AuditLogService.Log("Kullanıcı", "Giriş Yapıldı", auth.User.Id, auth.User.Username, null, "Başarılı kullanıcı girişi");

            // Bilinen zayıf/varsayılan bir şifreyle girildiyse şifre değişimi zorunludur
            if (UserService.MustChangePassword)
            {
                using var pwDlg = new PasswordSetupDialog("Şifrenizi Değiştirin", "Bu şifre kolay tahmin edilebilir. Devam etmeden önce yeni bir şifre belirleyin.", false);
                if (pwDlg.ShowDialog(this) != DialogResult.OK)
                {
                    UserService.Logout();
                    return;
                }
                UserService.ChangePassword(auth.User.Id, pwDlg.NewPassword);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblError.Text = auth.Message;
            _txtPassword.SelectAll();
            _txtPassword.Focus();
        }
    }
}
