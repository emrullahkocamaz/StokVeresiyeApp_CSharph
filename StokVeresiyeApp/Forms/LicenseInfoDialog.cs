using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class LicenseInfoDialog : Form
{
    private Label _lblStatus = new();
    private Label _lblExpiry = new();
    private Label _lblDaysRemaining = new();
    private Label _lblLicensedTo = new();
    private TextBox _txtMachineCode = new();

    public LicenseInfoDialog()
    {
        Text = "Bilensis - Lisans Durumu & Bilgileri";
        Width = 560;
        Height = 490;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
        LoadLicense();
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 14, 20, 14)
        };
        var lblTitle = new Label
        {
            Text = "🛡️ Lisans Bilgileri & Durumu",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 26
        };
        var lblSub = new Label
        {
            Text = "Bilensis 1 Yıllık Çevrimdışı Lisanslama Sistemi",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Bottom,
            Height = 18
        };
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        // 2. Alt Butonlar
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        var btnClose = UITheme.CreateButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 90, 34);
        btnClose.Dock = DockStyle.Right;

        var btnDeactivate = UITheme.CreateButton("🚫 Lisansı Pasife Al", UITheme.Danger, Color.White, DeactivateClick, 160, 34);
        btnDeactivate.Dock = DockStyle.Left;

        var btnRenew = UITheme.CreateButton("🔑 Yeni Lisans Gir", UITheme.Primary, Color.White, OpenRenewal, 150, 34);
        btnRenew.Dock = DockStyle.Left;

        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(btnRenew);
        bottom.Controls.Add(btnDeactivate);
        Controls.Add(bottom);

        // 3. İçerik Alanı
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18)
        };

        int top = 8;

        // Bilgi Kartı
        var card = new Panel
        {
            Left = 0,
            Top = top,
            Width = 495,
            Height = 140,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(16, 12, 16, 12)
        };

        _lblStatus.Text = "Durum: Kontrol ediliyor...";
        _lblStatus.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        _lblStatus.ForeColor = UITheme.Success;
        _lblStatus.Dock = DockStyle.Top;
        _lblStatus.Height = 28;

        _lblLicensedTo.Text = "Lisans Sahibi: -";
        _lblLicensedTo.Font = UITheme.RegularFont;
        _lblLicensedTo.ForeColor = UITheme.TextPrimary;
        _lblLicensedTo.Dock = DockStyle.Top;
        _lblLicensedTo.Height = 24;

        _lblExpiry.Text = "Son Geçerlilik: -";
        _lblExpiry.Font = UITheme.RegularFont;
        _lblExpiry.ForeColor = UITheme.TextPrimary;
        _lblExpiry.Dock = DockStyle.Top;
        _lblExpiry.Height = 24;

        _lblDaysRemaining.Text = "Kalan Süre: -";
        _lblDaysRemaining.Font = UITheme.TitleFont;
        _lblDaysRemaining.ForeColor = UITheme.Primary;
        _lblDaysRemaining.Dock = DockStyle.Top;
        _lblDaysRemaining.Height = 28;

        card.Controls.Add(_lblDaysRemaining);
        card.Controls.Add(_lblExpiry);
        card.Controls.Add(_lblLicensedTo);
        card.Controls.Add(_lblStatus);
        content.Controls.Add(card);

        top += 155;

        // Makine Kodu
        var lblMc = new Label { Text = "Makine Donanım Kodunuz (Donanım ID):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 495, Height = 22 };
        top += 24;

        _txtMachineCode.Left = 0;
        _txtMachineCode.Top = top;
        _txtMachineCode.Width = 330;
        _txtMachineCode.Height = 32;
        _txtMachineCode.ReadOnly = true;
        _txtMachineCode.BackColor = Color.White;
        _txtMachineCode.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
        _txtMachineCode.Text = LicenseService.GetMachineCode();

        var btnCopy = UITheme.CreateButton("📋 Kopyala", UITheme.Info, Color.White, (s, e) =>
        {
            Clipboard.SetText(_txtMachineCode.Text);
            MessageBox.Show("Makine kodunuz kopyalandı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, 150, 32);
        btnCopy.Left = 345;
        btnCopy.Top = top;

        content.Controls.Add(lblMc);
        content.Controls.Add(_txtMachineCode);
        content.Controls.Add(btnCopy);

        top += 46;

        var btnMailBox = UITheme.CreateButton("🚀 Lisans Talebini Doğrudan İlet (Mail Box)", UITheme.Primary, Color.White, (s, e) =>
        {
            using var mb = new LicenseMailBoxDialog();
            mb.ShowDialog(this);
        }, 320, 36);
        btnMailBox.Left = 0;
        btnMailBox.Top = top;
        btnMailBox.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        var btnMail = UITheme.CreateButton("✉️ E-Posta İstemcisi İle", UITheme.Secondary, Color.White, (s, e) =>
        {
            LicenseService.OpenLicenseMailRequest();
        }, 165, 36);
        btnMail.Left = 330;
        btnMail.Top = top;

        content.Controls.Add(btnMailBox);
        content.Controls.Add(btnMail);

        Controls.Add(content);
        content.BringToFront();
        header.SendToBack();
        bottom.SendToBack();
    }

    private void DeactivateClick(object? sender, EventArgs e)
    {
        var ask = MessageBox.Show(
            "Mevcut sistem lisansını pasife almak (devre dışı bırakmak) istediğinize emin misiniz?\n\n" +
            "Lisans pasife alındığında sistem korumaya geçecek ve yeni geçerli bir lisans girilene kadar kullanılamayacaktır.",
            "Lisansı Pasife Alma Onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (ask == DialogResult.Yes)
        {
            var res = LicenseService.DeactivateLicense("Kullanıcı lisans bilgi ekranından pasife aldı.");
            if (res.Success)
            {
                MessageBox.Show(res.Message, "Lisans Pasife Alındı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.Abort;
                Close();
            }
            else
            {
                MessageBox.Show(res.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadLicense()
    {
        var lic = LicenseService.CheckLicenseStatus();
        if (lic.IsValid)
        {
            _lblStatus.Text = "● LİSANS AKTİF & GEÇERLİ";
            _lblStatus.ForeColor = UITheme.Success;
            _lblLicensedTo.Text = $"Lisans Sahibi: {lic.LicensedTo}";
            _lblExpiry.Text = $"Son Geçerlilik Tarihi: {lic.ExpireDate:dd.MM.yyyy}";
            _lblDaysRemaining.Text = $"Kalan Gün Sayısı: {lic.DaysRemaining} Gün";
        }
        else
        {
            _lblStatus.Text = "● LİSANS GEÇERSİZ VEYA SÜRESİ DOLMUŞ";
            _lblStatus.ForeColor = UITheme.Danger;
            _lblLicensedTo.Text = "Lisans Sahibi: -";
            _lblExpiry.Text = "Son Geçerlilik: Süre Doldu";
            _lblDaysRemaining.Text = "Kalan Gün Sayısı: 0 Gün";
        }
    }

    private void OpenRenewal(object? sender, EventArgs e)
    {
        using var actForm = new LicenseActivationForm();
        if (actForm.ShowDialog(this) == DialogResult.OK)
        {
            LoadLicense();
        }
    }
}
