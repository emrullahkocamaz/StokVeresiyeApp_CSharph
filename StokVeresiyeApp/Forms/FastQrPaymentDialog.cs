using System.Drawing;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class FastQrPaymentDialog : BaseModernForm
{
    private readonly double _amount;
    private readonly string _docNo;
    private readonly string _customerName;

    private readonly PictureBox _picQr = new() { Size = new Size(260, 260), SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
    private readonly TextBox _txtIban = new() { Width = 310, Font = new Font("Consolas", 10f) };
    private readonly TextBox _txtReceiver = new() { Width = 310, Font = new Font("Segoe UI", 9.5f) };
    private readonly TextBox _txtBank = new() { Width = 310, Font = new Font("Segoe UI", 9.5f) };
    private readonly Label _lblAmount = new() { Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = Color.FromArgb(16, 185, 129), AutoSize = true };

    public bool PaymentConfirmed { get; private set; } = false;

    public FastQrPaymentDialog(double amount, string docNo, string customerName = "(Perakende)")
        : base("⚡ FAST / Karekod ile Anında IBAN Ödeme Alma", 760, 580)
    {
        _amount = amount;
        _docNo = string.IsNullOrWhiteSpace(docNo) ? "SAT-" + DateTime.Now.ToString("yyyyMMdd-HHmm") : docNo;
        _customerName = customerName;

        LoadBankSettings();
        BuildLayout();
        GenerateQr();
    }

    private void LoadBankSettings()
    {
        _txtIban.Text = SettingsService.Get("Fast_Iban", "TR00 0000 0000 0000 0000 0000 00");
        _txtReceiver.Text = SettingsService.Get("Fast_Receiver", "İşletme Sahibi / Ticari Unvan");
        _txtBank.Text = SettingsService.Get("Fast_Bank", "Ziraat Bankası / FAST");
    }

    private void SaveBankSettings()
    {
        SettingsService.Set("Fast_Iban", _txtIban.Text.Trim());
        SettingsService.Set("Fast_Receiver", _txtReceiver.Text.Trim());
        SettingsService.Set("Fast_Bank", _txtBank.Text.Trim());
    }

    private void BuildLayout()
    {
        if (Controls.Find("bodyPanel", true).FirstOrDefault() is not Panel bodyPanel)
            return;

        bodyPanel.Controls.Clear();

        // 1. Üst Bilgi Banner
        var topBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(240, 253, 244),
            Padding = new Padding(12, 10, 12, 10)
        };
        var lblBanner = new Label
        {
            Text = "📱 Müşteriniz herhangi bir banka uygulamasından bu karekodu okuttuğunda tutar ve açıklama otomatik dolar; para anında hesabınıza geçer.",
            Font = UITheme.SmallFont,
            ForeColor = Color.FromArgb(22, 101, 52),
            Dock = DockStyle.Fill
        };
        topBanner.Controls.Add(lblBanner);

        // 2. Ana Gövde: Sol QR Kod, Sağ IBAN/Banka Bilgileri
        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 20, 10) };

        // Sol Panel: Karekod ve Tutar
        var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 300, Padding = new Padding(10) };
        _lblAmount.Text = $"{_amount:N2} ₺";
        _lblAmount.TextAlign = ContentAlignment.MiddleCenter;
        _lblAmount.Dock = DockStyle.Top;

        var lblSub = new Label
        {
            Text = $"Fiş / Belge: {_docNo}",
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 22
        };

        _picQr.Dock = DockStyle.Bottom;
        _picQr.Height = 260;

        pnlLeft.Controls.Add(_lblAmount);
        pnlLeft.Controls.Add(lblSub);
        pnlLeft.Controls.Add(_picQr);

        // Sağ Panel: IBAN & Alıcı Tanımı
        var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 10, 10, 10) };

        var lblRightTitle = new Label
        {
            Text = "🏦 FAST / Havale Hesap Bilgileriniz:",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = UITheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 30
        };

        var flowFields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true };

        void AddField(string title, Control ctrl)
        {
            var pnl = new Panel { Width = 330, Height = 58, Margin = new Padding(0, 0, 0, 8) };
            var l = new Label { Text = title, Font = UITheme.SmallFont, ForeColor = UITheme.TextSecondary, Dock = DockStyle.Top, Height = 18 };
            ctrl.Dock = DockStyle.Bottom;
            pnl.Controls.Add(l);
            pnl.Controls.Add(ctrl);
            flowFields.Controls.Add(pnl);
        }

        AddField("Banka Adı:", _txtBank);
        AddField("Alıcı Adı / Unvanı:", _txtReceiver);
        AddField("IBAN Numarası (TR...):", _txtIban);

        _txtIban.TextChanged += (s, e) => { SaveBankSettings(); GenerateQr(); };
        _txtReceiver.TextChanged += (s, e) => { SaveBankSettings(); GenerateQr(); };
        _txtBank.TextChanged += (s, e) => { SaveBankSettings(); GenerateQr(); };

        var btnRefreshQr = UITheme.CreateButton("🔄 Karekodu Yenile", UITheme.Primary, Color.White, (s, e) => GenerateQr(), 160, 34);
        btnRefreshQr.Margin = new Padding(0, 10, 0, 0);
        flowFields.Controls.Add(btnRefreshQr);

        pnlRight.Controls.Add(flowFields);
        pnlRight.Controls.Add(lblRightTitle);

        mainPanel.Controls.Add(pnlRight);
        mainPanel.Controls.Add(pnlLeft);

        bodyPanel.Controls.Add(mainPanel);
        bodyPanel.Controls.Add(topBanner);

        // Alt Butonlar
        BtnSave.Text = "✅ Ödeme Geldi (Satışı Tamamla)";
        BtnSave.BackColor = Color.FromArgb(16, 185, 129);
        BtnSave.Width = 260;
        BtnSave.Click += (s, e) =>
        {
            PaymentConfirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    private void GenerateQr()
    {
        try
        {
            string cleanIban = _txtIban.Text.Replace(" ", "").Trim().ToUpperInvariant();
            string receiver = _txtReceiver.Text.Trim();
            string amountStr = _amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            // Standart FAST / Banka Karekod Metni
            // Format: Alıcı, IBAN, Tutar, Açıklama
            string qrPayload = $"TR-QR|FAST|{cleanIban}|{receiver}|{amountStr}|TRY|{_docNo}";

            // Alternatif evrensel bankacılık EPC / DeepLink formatı
            string universalPayload = $"iban:{cleanIban}?amount={amountStr}&currency=TRY&name={Uri.EscapeDataString(receiver)}&desc={Uri.EscapeDataString(_docNo)}";

            var bmp = BarcodeRenderer.GenerateQrCode(universalPayload, 250, 250);
            _picQr.Image?.Dispose();
            _picQr.Image = bmp;
        }
        catch { }
    }
}
