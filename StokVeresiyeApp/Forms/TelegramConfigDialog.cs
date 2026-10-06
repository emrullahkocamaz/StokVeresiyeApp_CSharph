using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class TelegramConfigDialog : BaseModernForm
{
    private TextBox _txtToken = new() { UseSystemPasswordChar = true };
    private TextBox _txtChatId = new() { PlaceholderText = "Örn: 123456789 (veya telefondan /bagla yazın)" };
    private CheckBox _chkShowToken = new() { Text = "Token'ı Göster", AutoSize = true };
    private CheckBox _chkCriticalStock = new() { Text = "Kritik stok seviyesine düşen ürünleri telefonuma bildir", AutoSize = true, Checked = true };
    private CheckBox _chkDailySummary = new() { Text = "Akşam gün sonu ciro ve kasa raporunu otomatik gönder", AutoSize = true, Checked = true };

    private Label _lblStatusText = new();
    private Label _lblBotInfo = new();
    private Button _btnToggleBot = new();
    private Button _btnTestMessage = new();

    public TelegramConfigDialog() : base("🤖 Telegram Asistanı & Cep Takip Yapılandırması", 680, 680)
    {
        Icon = AppResources.AppIcon;
        BuildContent();
        LoadCurrentConfig();
        UpdateStatusDisplay();
    }

    private void BuildContent()
    {
        // 1. Durum Kartı (Üst Bilgi)
        var statusCard = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 105,
            Padding = new Padding(15, 12, 15, 12),
            Margin = new Padding(0, 0, 0, 15)
        };

        _lblStatusText.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        _lblStatusText.Dock = DockStyle.Top;
        _lblStatusText.Height = 28;

        _lblBotInfo.Font = UITheme.RegularFont;
        _lblBotInfo.ForeColor = UITheme.TextSecondary;
        _lblBotInfo.Dock = DockStyle.Fill;

        statusCard.Controls.Add(_lblBotInfo);
        statusCard.Controls.Add(_lblStatusText);

        // Durum kartını ve butonlarını scrollContainer'a değil formun üstüne entegre edebilmek için TableLayoutPanel'e satır olarak ekliyoruz
        AddRow("Bağlantı Durumu", statusCard, 115);

        // 2. Token Satırı
        var pnlToken = new Panel { Dock = DockStyle.Fill };
        _txtToken.Dock = DockStyle.Top;
        _txtToken.Height = 32;
        _chkShowToken.Dock = DockStyle.Bottom;
        _chkShowToken.CheckedChanged += (s, e) => _txtToken.UseSystemPasswordChar = !_chkShowToken.Checked;
        pnlToken.Controls.Add(_txtToken);
        pnlToken.Controls.Add(_chkShowToken);

        AddRow("Bot API Token (*)", pnlToken, 65);

        // 3. Yetkili Telefon / Chat ID
        AddRow("Yetkili Chat ID", _txtChatId, 38);

        // 4. Bildirim Seçenekleri
        var pnlChecks = new Panel { Dock = DockStyle.Fill };
        _chkCriticalStock.Dock = DockStyle.Top;
        _chkDailySummary.Dock = DockStyle.Bottom;
        pnlChecks.Controls.Add(_chkCriticalStock);
        pnlChecks.Controls.Add(_chkDailySummary);

        AddRow("Akıllı Bildirimler", pnlChecks, 55);

        // 5. Bot Kontrol Butonları
        var pnlActionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        _btnToggleBot = UITheme.CreateButton("▶️ Botu Başlat", UITheme.Primary, Color.White, ToggleBotClick, 160, 36);
        _btnTestMessage = UITheme.CreateButton("📱 Test Bildirimi Gönder", Color.FromArgb(16, 185, 129), Color.White, SendTestMessageClick, 190, 36);

        pnlActionButtons.Controls.Add(_btnToggleBot);
        pnlActionButtons.Controls.Add(_btnTestMessage);

        AddRow("Hızlı İşlemler", pnlActionButtons, 50);

        // 6. Rehber / Yardım Paneli
        var pnlGuide = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var lblGuide = new Label
        {
            Dock = DockStyle.Fill,
            Font = UITheme.SmallFont,
            ForeColor = UITheme.TextSecondary,
            Text = "💡 1 DAKİKADA TELEGRAM BOTU KURMA REHBERİ:\n\n" +
                   "1. Telefonunuzdan veya bilgisayardan Telegram'ı açın, arama kısmına @BotFather yazın.\n" +
                   "2. /newbot komutunu gönderin, botunuza bir ad (Örn: BilgeStokBot) belirleyin.\n" +
                   "3. BotFather'ın size vereceği uzun 'API Token' kodunu kopyalayıp yukarıdaki alana yapıştırın.\n" +
                   "4. 'Botu Başlat' butonuna basın. Ardından telefonunuzdan botunuza girip /start veya /bagla yazın!\n" +
                   "5. Artık telefonunuzdan ürün barkodlarının fotoğrafını atarak anında stok ve fiyat görebilirsiniz."
        };
        pnlGuide.Controls.Add(lblGuide);
        AddRow("Kurulum Rehberi", pnlGuide, 135);

        BtnSave.Text = "Kaydet";
        BtnSave.Click += SaveSettingsClick;
    }

    private void LoadCurrentConfig()
    {
        var cfg = TelegramBotService.Config;
        _txtToken.Text = cfg.BotToken;
        _txtChatId.Text = cfg.AuthorizedChatId > 0 ? cfg.AuthorizedChatId.ToString() : "";
        _chkCriticalStock.Checked = cfg.NotifyOnCriticalStock;
        _chkDailySummary.Checked = cfg.NotifyDailySummary;
    }

    private void UpdateStatusDisplay()
    {
        bool running = TelegramBotService.IsRunning;
        var cfg = TelegramBotService.Config;

        if (running)
        {
            _lblStatusText.Text = "🟢 Telegram Botu Aktif & Çalışıyor";
            _lblStatusText.ForeColor = UITheme.Success;
            _lblBotInfo.Text = $"Bot: @{TelegramBotService.BotUsername} ({TelegramBotService.BotFirstName})\n" +
                               $"Yetkili Sohbet: {(cfg.AuthorizedChatId > 0 ? $"{cfg.AuthorizedChatId} (@{cfg.AuthorizedUserName})" : "⚠️ Henüz bağlanmadı (Telefondan /bagla yazın)")}";

            _btnToggleBot.Text = "⏹️ Botu Durdur";
            _btnToggleBot.BackColor = UITheme.Danger;
            _btnTestMessage.Enabled = cfg.AuthorizedChatId > 0;
        }
        else
        {
            _lblStatusText.Text = "⚪ Telegram Botu Kapalı";
            _lblStatusText.ForeColor = UITheme.TextSecondary;
            _lblBotInfo.Text = "Bot kapalı durumdadır. Başlatmak için Token girip 'Botu Başlat'a tıklayınız.";

            _btnToggleBot.Text = "▶️ Botu Başlat";
            _btnToggleBot.BackColor = UITheme.Primary;
            _btnTestMessage.Enabled = false;
        }
    }

    private async void ToggleBotClick(object? sender, EventArgs e)
    {
        if (TelegramBotService.IsRunning)
        {
            TelegramBotService.StopBot();
            var cfg = TelegramBotService.Config;
            cfg.IsEnabled = false;
            TelegramBotService.SaveConfig();
            UpdateStatusDisplay();
            MessageBox.Show("Telegram botu durduruldu.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            string token = _txtToken.Text.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Lütfen önce BotFather'dan aldığınız API Token'ı giriniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Cursor = Cursors.WaitCursor;
            _btnToggleBot.Enabled = false;

            var res = await TelegramBotService.StartBotAsync(token);

            Cursor = Cursors.Default;
            _btnToggleBot.Enabled = true;

            UpdateStatusDisplay();

            if (res.Success)
            {
                MessageBox.Show(res.Message + "\n\nŞimdi telefonunuzdan botunuza girip /start veya /bagla yazınız.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(res.Message, "Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void SendTestMessageClick(object? sender, EventArgs e)
    {
        if (!TelegramBotService.IsRunning)
        {
            MessageBox.Show("Lütfen önce botu başlatınız.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await TelegramBotService.SendAlertToAdminAsync(
                "🔔 *Bilensis Test Bildirimi*\n\n" +
                "Masaüstü programınız ile cep telefonunuz arasındaki bağlantı *başarıyla sağlandı!*\n\n" +
                "Fotoğraf çekip barkod okutabilir veya /kasa, /stok, /kritik komutlarını test edebilirsiniz."
            );
            MessageBox.Show("Test bildirimi telefonunuza gönderildi! Lütfen Telegram uygulamanızı kontrol ediniz.", "Bildirim Gönderildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Bildirim gönderilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettingsClick(object? sender, EventArgs e)
    {
        var cfg = TelegramBotService.Config;
        cfg.BotToken = _txtToken.Text.Trim();

        if (long.TryParse(_txtChatId.Text.Trim(), out long cid))
            cfg.AuthorizedChatId = cid;

        cfg.NotifyOnCriticalStock = _chkCriticalStock.Checked;
        cfg.NotifyDailySummary = _chkDailySummary.Checked;

        TelegramBotService.SaveConfig();
        UpdateStatusDisplay();
    }
}
