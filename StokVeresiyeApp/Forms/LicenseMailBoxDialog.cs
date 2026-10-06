using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class LicenseMailBoxDialog : Form
{
    private TextBox _txtMachineCode = new();
    private TextBox _txtCompany = new();
    private TextBox _txtContact = new();
    private TextBox _txtPhone = new();
    private TextBox _txtEmail = new();
    private TextBox _txtNote = new();
    private Label _lblStatus = new();
    private Button _btnSend = new();

    public LicenseMailBoxDialog()
    {
        Text = "Bilensis - Lisans Talep İletişim Kutusu (Mail Box)";
        Width = 560;
        Height = 630;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI();
    }

    private void BuildUI()
    {
        // 1. Üst Başlık
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = "✉️ Lisans Talep İletişim Kutusu",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 26
        };
        var lblSub = new Label
        {
            Text = "İnternet üzerinden doğrudan emrkcm@gmail.com adresine lisans talebinizi iletin.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Bottom,
            Height = 18
        };
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        // 2. Alt Buton Paneli
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(20, 10, 20, 10)
        };

        var btnClose = UITheme.CreateButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 100, 34);
        btnClose.Dock = DockStyle.Right;

        var btnFallbackMail = UITheme.CreateButton("E-Posta İstemcisi İle Gönder", UITheme.Secondary, Color.White, (s, e) =>
        {
            LicenseService.OpenLicenseMailRequest(_txtCompany.Text);
        }, 220, 34);
        btnFallbackMail.Dock = DockStyle.Left;

        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(btnFallbackMail);
        Controls.Add(bottom);

        // 3. İçerik Alanı
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 16, 24, 16),
            AutoScroll = true
        };

        int top = 8;

        // Makine Kodu
        var lblMc = new Label { Text = "Makine Donanım Kodunuz (Otomatik):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;

        _txtMachineCode.Left = 0;
        _txtMachineCode.Top = top;
        _txtMachineCode.Width = 360;
        _txtMachineCode.Height = 28;
        _txtMachineCode.ReadOnly = true;
        _txtMachineCode.BackColor = Color.White;
        _txtMachineCode.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
        _txtMachineCode.Text = LicenseService.GetMachineCode();

        var btnCopyMc = UITheme.CreateButton("📋 Kopyala", UITheme.Info, Color.White, (s, e) =>
        {
            Clipboard.SetText(_txtMachineCode.Text);
            MessageBox.Show("Makine kodunuz kopyalandı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, 120, 28);
        btnCopyMc.Left = 370;
        btnCopyMc.Top = top;

        content.Controls.Add(lblMc);
        content.Controls.Add(_txtMachineCode);
        content.Controls.Add(btnCopyMc);

        top += 38;

        // Firma Adı
        var lblComp = new Label { Text = "Firma / İşletme Adı (*):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _txtCompany.Left = 0;
        _txtCompany.Top = top;
        _txtCompany.Width = 490;
        _txtCompany.Height = 28;
        content.Controls.Add(lblComp);
        content.Controls.Add(_txtCompany);

        top += 38;

        // Yetkili Kişi & Telefon
        var lblContact = new Label { Text = "Yetkili Adı Soyadı:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 240, Height = 20 };
        var lblPhone = new Label { Text = "İletişim Telefonu (*):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 250, Top = top, Width = 240, Height = 20 };
        top += 22;

        _txtContact.Left = 0;
        _txtContact.Top = top;
        _txtContact.Width = 240;
        _txtContact.Height = 28;

        _txtPhone.Left = 250;
        _txtPhone.Top = top;
        _txtPhone.Width = 240;
        _txtPhone.Height = 28;

        content.Controls.Add(lblContact);
        content.Controls.Add(_txtContact);
        content.Controls.Add(lblPhone);
        content.Controls.Add(_txtPhone);

        top += 38;

        // Yanıt E-Posta
        var lblEmail = new Label { Text = "Lisans Anahtarının Gönderileceği E-Posta Adresiniz (*):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _txtEmail.Left = 0;
        _txtEmail.Top = top;
        _txtEmail.Width = 490;
        _txtEmail.Height = 28;
        content.Controls.Add(lblEmail);
        content.Controls.Add(_txtEmail);

        top += 38;

        // Talep Notu
        var lblNote = new Label { Text = "Ek Talep / Notunuz:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 490, Height = 20 };
        top += 22;
        _txtNote.Left = 0;
        _txtNote.Top = top;
        _txtNote.Width = 490;
        _txtNote.Height = 55;
        _txtNote.Multiline = true;
        _txtNote.Text = "1 Yıllık Bilensis lisans anahtarı talep ediyorum.";
        content.Controls.Add(lblNote);
        content.Controls.Add(_txtNote);

        top += 65;

        // Gönder Butonu
        _btnSend = UITheme.CreateButton("🚀 Lisans Talebini Doğrudan İlet (emrkcm@gmail.com)", UITheme.Primary, Color.White, SendMailClick, 490, 42);
        _btnSend.Left = 0;
        _btnSend.Top = top;
        _btnSend.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        content.Controls.Add(_btnSend);

        top += 48;

        _lblStatus.Left = 0;
        _lblStatus.Top = top;
        _lblStatus.Width = 490;
        _lblStatus.Height = 35;
        _lblStatus.ForeColor = UITheme.TextSecondary;
        _lblStatus.Font = UITheme.SmallFont;
        content.Controls.Add(_lblStatus);

        Controls.Add(content);
        content.BringToFront();
        header.SendToBack();
        bottom.SendToBack();
    }

    private async void SendMailClick(object? sender, EventArgs e)
    {
        string company = _txtCompany.Text.Trim();
        string phone = _txtPhone.Text.Trim();
        string email = _txtEmail.Text.Trim();

        if (string.IsNullOrWhiteSpace(company))
        {
            MessageBox.Show("Lütfen Firma / İşletme Adını giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtCompany.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            MessageBox.Show("Lütfen size ulaşabileceğimiz en az bir telefon veya e-posta adresi giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtEmail.Focus();
            return;
        }

        _btnSend.Enabled = false;
        _btnSend.Text = "⏳ Gönderiliyor, lütfen bekleyiniz...";
        _lblStatus.ForeColor = UITheme.Primary;
        _lblStatus.Text = "Lisans talebiniz internet üzerinden emrkcm@gmail.com adresine aktarılıyor...";

        try
        {
            var res = await LicenseService.SendDirectLicenseRequestMailAsync(
                company,
                _txtContact.Text.Trim(),
                phone,
                email,
                _txtNote.Text.Trim()
            );

            if (res.Success)
            {
                _lblStatus.ForeColor = UITheme.Success;
                _lblStatus.Text = "✅ Talebiniz başarıyla iletildi!";
                MessageBox.Show(res.Message, "Talep Gönderildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            else
            {
                _lblStatus.ForeColor = UITheme.Danger;
                _lblStatus.Text = "⚠️ " + res.Message;
                var ask = MessageBox.Show(
                    $"{res.Message}\n\nStandart e-posta istemcinizi (Outlook / Thunderbird vb.) açarak göndermek ister misiniz?",
                    "İletim Durumu",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (ask == DialogResult.Yes)
                {
                    LicenseService.OpenLicenseMailRequest(company);
                }
            }
        }
        catch (Exception ex)
        {
            _lblStatus.ForeColor = UITheme.Danger;
            _lblStatus.Text = "Hata: " + ex.Message;
        }
        finally
        {
            _btnSend.Enabled = true;
            _btnSend.Text = "🚀 Lisans Talebini Doğrudan İlet (emrkcm@gmail.com)";
        }
    }
}
