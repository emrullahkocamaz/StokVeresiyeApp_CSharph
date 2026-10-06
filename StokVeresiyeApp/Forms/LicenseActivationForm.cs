using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class LicenseActivationForm : Form
{
    private TextBox _txtMachineCode = new();
    private TextBox _txtLicenseKey = new();
    private Label _lblStatus = new();

    public LicenseActivationForm(string? message = null)
    {
        Text = "Bilensis - Lisans Aktivasyonu";
        Width = 620;
        Height = 580;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI(message);
    }

    private void BuildUI(string? customMessage)
    {
        // 1. Üst Başlık
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(24, 14, 24, 14)
        };

        var lblLogo = new Label
        {
            Text = "🔒 Bilensis - Lisans Aktivasyonu",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 28
        };
        var lblSub = new Label
        {
            Text = customMessage ?? "Sistemi aktif olarak kullanabilmek için 1 yıllık lisans anahtarınızı giriniz.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(248, 113, 113), // Açık kırmızı
            Dock = DockStyle.Bottom,
            Height = 22
        };
        header.Controls.Add(lblLogo);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        // 2. Alt Buton Paneli
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(24, 12, 24, 12)
        };

        var btnExit = UITheme.CreateButton("Çıkış", UITheme.BorderColor, UITheme.TextPrimary, (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }, 110, 36);
        btnExit.Dock = DockStyle.Right;

        // Süper kullanıcı için gizli küçük kilit simgesi (admin ekranında belirgin buton olarak görünmez)
        var btnSecretKeygen = new Button
        {
            Text = "🔒",
            Dock = DockStyle.Left,
            Width = 36,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(148, 163, 184),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        btnSecretKeygen.FlatAppearance.BorderSize = 0;
        btnSecretKeygen.Click += OpenDevKeygen;

        bottomPanel.Controls.Add(btnExit);
        bottomPanel.Controls.Add(btnSecretKeygen);
        Controls.Add(bottomPanel);

        // Kısayol: Ctrl + Shift + S
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.S)
            {
                OpenDevKeygen(null, EventArgs.Empty);
            }
        };

        // 3. İçerik Alanı
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18),
            AutoScroll = true
        };

        int top = 8;

        // Bilgi Kartı
        var infoPanel = new Panel
        {
            Left = 0,
            Top = top,
            Width = 555,
            Height = 60,
            BackColor = Color.FromArgb(230, 250, 248),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 8, 12, 8)
        };
        var lblInfoText = new Label
        {
            Text = "ℹ️ Lisanslama tamamen çevrimdışı (offline) çalışır. İnternet bağlantısı gerekmez.\nMakine kodunuzu 'emrkcm@gmail.com' adresine ileterek 1 yıllık anahtarınızı alabilirsiniz.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(30, 64, 175),
            Dock = DockStyle.Fill
        };
        infoPanel.Controls.Add(lblInfoText);
        content.Controls.Add(infoPanel);

        top += 70;

        // 1. Makine Kodu
        var lblMc = new Label { Text = "Bu Bilgisayarın Benzersiz Donanım Kodu (Makine ID):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 555, Height = 22 };
        top += 24;

        _txtMachineCode.Left = 0;
        _txtMachineCode.Top = top;
        _txtMachineCode.Width = 350;
        _txtMachineCode.Height = 32;
        _txtMachineCode.ReadOnly = true;
        _txtMachineCode.BackColor = Color.White;
        _txtMachineCode.Font = new Font("Consolas", 11.5f, FontStyle.Bold);
        _txtMachineCode.ForeColor = UITheme.PrimaryDark;
        _txtMachineCode.Text = LicenseService.GetMachineCode();

        var btnCopyMc = UITheme.CreateButton("📋 Kodu Kopyala", UITheme.Info, Color.White, (s, e) =>
        {
            Clipboard.SetText(_txtMachineCode.Text);
            MessageBox.Show("Makine donanım kodunuz panoya kopyalandı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, 130, 32);
        btnCopyMc.Left = 360;
        btnCopyMc.Top = top;

        content.Controls.Add(lblMc);
        content.Controls.Add(_txtMachineCode);
        content.Controls.Add(btnCopyMc);

        top += 42;

        // Mail İletişim Butonları
        var btnMailBox = UITheme.CreateButton("🚀 Lisans Talebini Doğrudan İlet (Mail Box)", UITheme.Primary, Color.White, (s, e) =>
        {
            using var mb = new LicenseMailBoxDialog();
            mb.ShowDialog(this);
        }, 340, 36);
        btnMailBox.Left = 0;
        btnMailBox.Top = top;
        btnMailBox.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        var btnMailClient = UITheme.CreateButton("✉️ E-Posta İstemcisi İle", UITheme.Secondary, Color.White, (s, e) =>
        {
            LicenseService.OpenLicenseMailRequest();
        }, 205, 36);
        btnMailClient.Left = 350;
        btnMailClient.Top = top;

        content.Controls.Add(btnMailBox);
        content.Controls.Add(btnMailClient);

        top += 50;

        // 2. Lisans Anahtarı Giriş Alanı
        var lblKey = new Label { Text = "Size İletilen 1 Yıllık Lisans Anahtarını Yapıştırın:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 555, Height = 22 };
        top += 24;

        _txtLicenseKey.Left = 0;
        _txtLicenseKey.Top = top;
        _txtLicenseKey.Width = 555;
        _txtLicenseKey.Height = 65;
        _txtLicenseKey.Multiline = true;
        _txtLicenseKey.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
        _txtLicenseKey.ScrollBars = ScrollBars.Vertical;
        content.Controls.Add(lblKey);
        content.Controls.Add(_txtLicenseKey);

        top += 74;

        // Aktifleştir Butonu
        var btnActivate = UITheme.CreateButton("🔑 Lisansı Doğrula ve Sistemi Aktifleştir", UITheme.Success, Color.White, ActivateClick, 555, 42);
        btnActivate.Left = 0;
        btnActivate.Top = top;
        btnActivate.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        content.Controls.Add(btnActivate);

        top += 50;

        _lblStatus.Left = 0;
        _lblStatus.Top = top;
        _lblStatus.Width = 555;
        _lblStatus.Height = 30;
        _lblStatus.ForeColor = UITheme.Danger;
        _lblStatus.Font = UITheme.SmallFont;
        content.Controls.Add(_lblStatus);

        Controls.Add(content);

        // Z-order: content arkada, header ve bottom üstte
        content.BringToFront();
        header.SendToBack();
        bottomPanel.SendToBack();
    }

    private void ActivateClick(object? sender, EventArgs e)
    {
        string key = _txtLicenseKey.Text.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            _lblStatus.Text = "Lütfen lisans anahtarınızı giriniz.";
            return;
        }

        var result = LicenseService.ActivateLicense(key);
        if (result.Success)
        {
            MessageBox.Show(result.Message, "Lisans Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            _lblStatus.Text = result.Message;
            MessageBox.Show(result.Message, "Lisans Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenDevKeygen(object? sender, EventArgs e)
    {
        using var auth = new SuperUserAuthDialog();
        if (auth.ShowDialog(this) == DialogResult.OK)
        {
            using var gen = new LicenseGeneratorForm(_txtMachineCode.Text);
            gen.ShowDialog(this);
        }
    }
}
