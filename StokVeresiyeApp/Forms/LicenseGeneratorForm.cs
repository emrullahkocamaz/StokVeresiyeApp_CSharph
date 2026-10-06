using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class LicenseGeneratorForm : Form
{
    private TextBox _txtMachineCode = new();
    private TextBox _txtClientName = new();
    private NumericUpDown _numDays = new();
    private TextBox _txtGeneratedKey = new();
    private Label _lblStatus = new();

    public LicenseGeneratorForm(string? initialMachineCode = null)
    {
        Text = "Bilensis - Lisans Anahtarı Üretici (Yönetici & Geliştirici)";
        Width = 620;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        BuildUI(initialMachineCode);
    }

    private void BuildUI(string? initialMachineCode)
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitle = new Label
        {
            Text = "🔑 Lisans Anahtarı Üretici",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 26
        };
        var lblSub = new Label
        {
            Text = "Müşteriden gelen makine koduna özel 1 yıllık çevrimdışı aktivasyon anahtarı üretin.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Bottom,
            Height = 18
        };
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        Controls.Add(header);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 18, 24, 18),
            AutoScroll = true
        };

        int top = 12;

        // 1. Müşteri Makine Kodu
        var lblMc = new Label { Text = "Müşteri Makine Kodu (Donanım ID):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 550, Height = 22 };
        top += 24;
        _txtMachineCode.Left = 0;
        _txtMachineCode.Top = top;
        _txtMachineCode.Width = 400;
        _txtMachineCode.Height = 32;
        _txtMachineCode.Font = new Font("Consolas", 10.5f, FontStyle.Bold);
        _txtMachineCode.Text = initialMachineCode ?? LicenseService.GetMachineCode();

        var btnPaste = UITheme.CreateButton("📋 Yapıştır", UITheme.Secondary, Color.White, (s, e) =>
        {
            if (Clipboard.ContainsText())
                _txtMachineCode.Text = Clipboard.GetText().Trim();
        }, 80, 30);
        btnPaste.Left = 410;
        btnPaste.Top = top;

        var btnCurrent = UITheme.CreateButton("Bu Cihaz", UITheme.Info, Color.White, (s, e) =>
        {
            _txtMachineCode.Text = LicenseService.GetMachineCode();
        }, 75, 30);
        btnCurrent.Left = 495;
        btnCurrent.Top = top;

        content.Controls.Add(lblMc);
        content.Controls.Add(_txtMachineCode);
        content.Controls.Add(btnPaste);
        content.Controls.Add(btnCurrent);

        top += 44;

        // 2. Firma / Müşteri Adı
        var lblName = new Label { Text = "Lisans Sahibi / Müşteri Firma Adı:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 550, Height = 22 };
        top += 24;
        _txtClientName.Left = 0;
        _txtClientName.Top = top;
        _txtClientName.Width = 550;
        _txtClientName.Height = 30;
        _txtClientName.Text = "Lisanslı Firma";
        content.Controls.Add(lblName);
        content.Controls.Add(_txtClientName);

        top += 44;

        // 3. Lisans Süresi (Varsayılan 365 Gün)
        var lblDays = new Label { Text = "Geçerlilik Süresi (Gün - Standart: 365 Gün = 1 Yıl):", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 550, Height = 22 };
        top += 24;
        _numDays.Left = 0;
        _numDays.Top = top;
        _numDays.Width = 200;
        _numDays.Height = 30;
        _numDays.Minimum = 1;
        _numDays.Maximum = 3650;
        _numDays.Value = 365;
        content.Controls.Add(lblDays);
        content.Controls.Add(_numDays);

        top += 46;

        // 4. Üret Butonu
        var btnGenerate = UITheme.CreateButton("⚡ 1 Yıllık Lisans Anahtarı Üret", UITheme.Primary, Color.White, GenerateClick, 550, 42);
        btnGenerate.Left = 0;
        btnGenerate.Top = top;
        btnGenerate.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        content.Controls.Add(btnGenerate);

        top += 54;

        // 5. Üretilen Lisans Anahtarı
        var lblKey = new Label { Text = "Üretilen Benzersiz Lisans Anahtarı:", Font = UITheme.TitleFont, ForeColor = UITheme.TextPrimary, Left = 0, Top = top, Width = 550, Height = 22 };
        top += 24;
        _txtGeneratedKey.Left = 0;
        _txtGeneratedKey.Top = top;
        _txtGeneratedKey.Width = 550;
        _txtGeneratedKey.Height = 55;
        _txtGeneratedKey.Multiline = true;
        _txtGeneratedKey.ReadOnly = true;
        _txtGeneratedKey.BackColor = Color.White;
        _txtGeneratedKey.Font = new Font("Consolas", 9.5f, FontStyle.Regular);
        content.Controls.Add(lblKey);
        content.Controls.Add(_txtGeneratedKey);

        top += 62;

        var btnCopyKey = UITheme.CreateButton("📋 Anahtarı Kopyala", UITheme.Success, Color.White, (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_txtGeneratedKey.Text))
            {
                Clipboard.SetText(_txtGeneratedKey.Text);
                MessageBox.Show("Lisans anahtarı panoya kopyalandı! Müşteriye iletebilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }, 180, 34);
        btnCopyKey.Left = 0;
        btnCopyKey.Top = top;

        var btnCopyEmailTemplate = UITheme.CreateButton("📧 Müşteri Yanıt Şablonunu Kopyala", UITheme.Secondary, Color.White, CopyTemplateClick, 240, 34);
        btnCopyEmailTemplate.Left = 190;
        btnCopyEmailTemplate.Top = top;

        var btnClose = UITheme.CreateButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 110, 34);
        btnClose.Left = 440;
        btnClose.Top = top;

        content.Controls.Add(btnCopyKey);
        content.Controls.Add(btnCopyEmailTemplate);
        content.Controls.Add(btnClose);

        Controls.Add(content);
    }

    private void GenerateClick(object? sender, EventArgs e)
    {
        string mc = _txtMachineCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(mc))
        {
            MessageBox.Show("Lütfen makine kodunu giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            int days = (int)_numDays.Value;
            string key = LicenseService.GenerateLicenseKey(mc, _txtClientName.Text.Trim(), days);
            _txtGeneratedKey.Text = key;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Anahtar üretilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyTemplateClick(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtGeneratedKey.Text))
        {
            MessageBox.Show("Önce lisans anahtarı üretiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string template = 
            $"Merhaba,\r\n\r\n" +
            $"Bilensis (Stok & Cari Yönetim Sistemi) için 1 yıllık lisans anahtarınız başarıyla üretilmiştir.\r\n\r\n" +
            $"LİSANS BİLGİLERİNİZ:\r\n" +
            $"--------------------------------------------------\r\n" +
            $"Lisans Sahibi: {_txtClientName.Text}\r\n" +
            $"Süre: {_numDays.Value} Gün (1 Yıl)\r\n" +
            $"Lisans Anahtarınız:\r\n{_txtGeneratedKey.Text}\r\n" +
            $"--------------------------------------------------\r\n\r\n" +
            $"AKTİVASYON ADIMLARI:\r\n" +
            $"1. Bilensis uygulamasını açın.\r\n" +
            $"2. Lisans Aktivasyon ekranındaki 'Lisans Anahtarı' kutusuna yukarıdaki anahtarı yapıştırın.\r\n" +
            $"3. 'Lisansı Doğrula ve Sistemi Aktifleştir' butonuna tıklayın.\r\n\r\n" +
            $"Sisteminiz anında internet gerektirmeden (çevrimdışı) aktifleşecektir.\r\n" +
            $"İyi günlerde kullanmanızı dileriz.\r\n\r\n" +
            $"Destek & İletişim: {LicenseService.SupportEmail}";

        Clipboard.SetText(template);
        MessageBox.Show("Müşteriye gönderilecek hazır yanıt metni panoya kopyalandı! E-posta gövdesine yapıştırıp yollayabilirsiniz.", "Kopyalandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
