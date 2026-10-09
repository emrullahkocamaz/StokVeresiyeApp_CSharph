using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

public class WhatsAppAssistantDialog : BaseModernForm
{
    private readonly TextBox _txtAdminPhone = new() { PlaceholderText = "Örn: 0532 123 45 67 veya 5321234567" };
    private readonly CheckBox _chkAutoDaily = new() { Text = "Akşam kasa kapanışında / gün sonunda WhatsApp raporu otomatik oluşturulsun", AutoSize = true, Checked = true };
    private readonly CheckBox _chkAutoCritical = new() { Text = "Kritik stok seviyesine düşen ürünler için uyarı hazırla", AutoSize = true, Checked = true };
    private readonly Label _lblPortalUrl = new() { Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = UITheme.Primary, AutoSize = true };

    public WhatsAppAssistantDialog() : base("💬 WhatsApp Yönetici Asistanı & Online Mobil İşlem Merkezi", 720, 680)
    {
        Icon = AppResources.AppIcon;
        BuildContent();
        LoadConfig();
    }

    private void BuildContent()
    {
        // 1. Üst Durum & Bilgilendirme Kartı
        var statusCard = new CardPanel
        {
            Dock = DockStyle.Top,
            Height = 110,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.FromArgb(240, 253, 244)
        };

        var lblStatus = new Label
        {
            Text = "📲 WhatsApp Asistanı & Cep Telefonundan Online İşlem",
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 101, 52),
            Dock = DockStyle.Top,
            Height = 26
        };

        var lblDesc = new Label
        {
            Text = "Dükkanınızın güncel kasa durumunu, cirosunu ve borçlularını WhatsApp üzerinden takip edebilir; cep telefonunuzdan canlı mobil portala tek dokunuşla bağlanıp tahsilat ve stok sorgusu yapabilirsiniz.",
            Font = UITheme.RegularFont,
            ForeColor = Color.FromArgb(21, 128, 61),
            Dock = DockStyle.Fill
        };

        statusCard.Controls.Add(lblDesc);
        statusCard.Controls.Add(lblStatus);
        AddRow("Durum", statusCard, 120);

        // 2. Yönetici Telefon Numarası
        var pnlPhone = new Panel { Dock = DockStyle.Fill };
        _txtAdminPhone.Dock = DockStyle.Top;
        _txtAdminPhone.Height = 32;

        var lblPhoneHint = new Label
        {
            Text = "💡 Raporların ve mobil bağlantı linkinin gönderileceği yönetici cep telefonu numarasını giriniz.",
            Font = new Font("Segoe UI", 8.2f),
            ForeColor = UITheme.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 20
        };
        pnlPhone.Controls.Add(_txtAdminPhone);
        pnlPhone.Controls.Add(lblPhoneHint);
        AddRow("Yönetici WhatsApp No (*)", pnlPhone, 60);

        // 3. Mobil Yönetici Portalı Linki & Hızlı Bağlantı
        var pnlPortal = new Panel { Dock = DockStyle.Fill };
        string portalUrl = MobileScannerService.GetPrimaryUrl();
        _lblPortalUrl.Text = portalUrl;
        _lblPortalUrl.Dock = DockStyle.Top;
        _lblPortalUrl.Height = 24;

        var btnSendPortalLink = UITheme.CreateButton("📲 Cep Telefonuma Mobil Portal Linkini Gönder", Color.FromArgb(37, 211, 102), Color.White, (s, e) => SendPortalLinkClick(), 340, 36);
        btnSendPortalLink.Dock = DockStyle.Bottom;

        pnlPortal.Controls.Add(_lblPortalUrl);
        pnlPortal.Controls.Add(btnSendPortalLink);
        AddRow("Canlı Mobil Portal", pnlPortal, 70);

        // 4. WhatsApp Anlık Rapor Gönderim Butonları
        var flowReports = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        var btnSendCash = UITheme.CreateButton("📊 Kasa & Ciro Özeti", Color.FromArgb(16, 185, 129), Color.White, (s, e) => SendCashClick(), 175, 36);
        var btnSendDebtors = UITheme.CreateButton("👥 Borçlu Listesi", Color.FromArgb(245, 158, 11), Color.White, (s, e) => SendDebtorsClick(), 165, 36);
        var btnSendStock = UITheme.CreateButton("⚠️ Kritik Stoklar", Color.FromArgb(239, 68, 68), Color.White, (s, e) => SendStockClick(), 165, 36);

        flowReports.Controls.Add(btnSendCash);
        flowReports.Controls.Add(btnSendDebtors);
        flowReports.Controls.Add(btnSendStock);
        AddRow("WhatsApp Raporları", flowReports, 50);

        // 5. Akıllı Ayarlar
        var pnlChecks = new Panel { Dock = DockStyle.Fill };
        _chkAutoDaily.Dock = DockStyle.Top;
        _chkAutoCritical.Dock = DockStyle.Bottom;
        pnlChecks.Controls.Add(_chkAutoDaily);
        pnlChecks.Controls.Add(_chkAutoCritical);
        AddRow("Otomatik Bildirim", pnlChecks, 52);

        // 6. Bilgilendirme Kartı (WhatsApp & Mobil Portal)
        var infoCard = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Color.FromArgb(248, 250, 252)
        };

        var lblHelp = new Label
        {
            Text = "💡 WHATSAPP & MOBİL İŞLEM KULLANIMI:\n\n" +
                   "1. 'Cep Telefonuma Mobil Portal Linkini Gönder' butonuna basarak bağlantıyı telefonunuza atın.\n" +
                   "2. Telefonunuzda linke tıkladığınızda; Günlük Kasa & Ciro, Ürün/Fiyat Sorgulama, Borçlu Listesi ve Cepten Tahsilat Alma ekranı açılır.\n" +
                   "3. Dükkandayken veya yerel ağdayken cepten tahsilat yaptığınızda veya barkod okuttuğunuzda anında masaüstü sisteme işlenir.\n" +
                   "4. WhatsApp'tan müşterilerinize tek dokunuşla dijital fiş ve borç hatırlatma mesajları iletebilirsiniz.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill
        };
        infoCard.Controls.Add(lblHelp);
        AddRow("Mobil Kılavuz", infoCard, 130);

        // Kaydet butonu davranışı
        BtnSave.Text = "💾 Ayarları Kaydet";
        BtnSave.Click += (s, e) => SaveConfigClick();
    }

    private void LoadConfig()
    {
        _txtAdminPhone.Text = WhatsAppService.Config.AdminPhoneNumber;
        _chkAutoDaily.Checked = WhatsAppService.Config.AutoDailySummary;
        _chkAutoCritical.Checked = WhatsAppService.Config.AutoCriticalStockAlert;
        _lblPortalUrl.Text = "👉 " + MobileScannerService.GetPrimaryUrl();
    }

    private void SaveConfigClick()
    {
        WhatsAppService.Config.AdminPhoneNumber = _txtAdminPhone.Text.Trim();
        WhatsAppService.Config.AutoDailySummary = _chkAutoDaily.Checked;
        WhatsAppService.Config.AutoCriticalStockAlert = _chkAutoCritical.Checked;
        WhatsAppService.SaveConfig();

        MessageBox.Show("WhatsApp asistan ayarları başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private string GetValidPhone()
    {
        string phone = _txtAdminPhone.Text.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            MessageBox.Show("Lütfen geçerli bir yönetici cep telefonu numarası giriniz.", "Telefon Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAdminPhone.Focus();
            return "";
        }
        return phone;
    }

    private void SendPortalLinkClick()
    {
        string phone = GetValidPhone();
        if (string.IsNullOrWhiteSpace(phone)) return;

        SaveConfigClickInternal();
        string url = MobileScannerService.GetPrimaryUrl();
        WhatsAppService.SendAdminPortalLink(phone, url);
    }

    private void SendCashClick()
    {
        string phone = GetValidPhone();
        if (string.IsNullOrWhiteSpace(phone)) return;

        SaveConfigClickInternal();
        WhatsAppService.SendAdminCashSummary(phone);
    }

    private void SendDebtorsClick()
    {
        string phone = GetValidPhone();
        if (string.IsNullOrWhiteSpace(phone)) return;

        SaveConfigClickInternal();
        WhatsAppService.SendAdminTopDebtors(phone);
    }

    private void SendStockClick()
    {
        string phone = GetValidPhone();
        if (string.IsNullOrWhiteSpace(phone)) return;

        SaveConfigClickInternal();
        WhatsAppService.SendAdminCriticalStocks(phone);
    }

    private void SaveConfigClickInternal()
    {
        WhatsAppService.Config.AdminPhoneNumber = _txtAdminPhone.Text.Trim();
        WhatsAppService.Config.AutoDailySummary = _chkAutoDaily.Checked;
        WhatsAppService.Config.AutoCriticalStockAlert = _chkAutoCritical.Checked;
        WhatsAppService.SaveConfig();
    }
}
