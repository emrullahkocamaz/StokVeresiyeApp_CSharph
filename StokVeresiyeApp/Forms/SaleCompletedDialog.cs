using System.Drawing.Printing;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Forms;

/// <summary>
/// Satış kaydedildikten sonra açılır: fişi seçili yazıcıdan basma, cari müşteriler için PDF belgesi hazırlama
/// ve müşterinin kayıtlı numarasına WhatsApp'tan gönderme.
/// </summary>
public class SaleCompletedDialog : Form
{
    private readonly SaleReceiptData _data;
    private readonly ComboBox _cmbPrinter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _lblStatus = new();
    private readonly Button _btnShowPdf;
    private string? _pdfPath;

    public SaleCompletedDialog(SaleReceiptData data)
    {
        _data = data;

        Text = "Satış Tamamlandı";
        Icon = AppResources.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 430);
        BackColor = UITheme.Background;
        Font = UITheme.RegularFont;

        var lblTitle = new Label
        {
            Text = "✅ Satış kaydedildi",
            Font = UITheme.HeaderFont,
            ForeColor = UITheme.Primary,
            Left = 24, Top = 18, Width = 510, Height = 32
        };

        string who = data.HasAccount ? $"{data.CustomerName}" : "Perakende müşteri";
        string balance = data.HasAccount && data.BalanceAfterSale.HasValue
            ? (data.BalanceAfterSale.Value > 0.005 ? $"\nGüncel cari borç: {data.BalanceAfterSale.Value:N2} ₺" : "\nGüncel cari borç: yok")
            : "";
        var lblSummary = new Label
        {
            Text = $"{who}\nBelge: {(string.IsNullOrWhiteSpace(data.DocNo) ? "-" : data.DocNo)}   Tutar: {data.NetTotal:N2} ₺   Ödeme: {data.PaymentMethod}{balance}",
            ForeColor = UITheme.TextPrimary,
            Left = 24, Top = 54, Width = 510, Height = 66
        };

        // Fiş yazdırma
        var lblPrinter = new Label { Text = "Fiş yazıcısı:", ForeColor = UITheme.TextSecondary, Left = 24, Top = 132, Width = 120, Height = 22 };
        _cmbPrinter.Left = 24; _cmbPrinter.Top = 156; _cmbPrinter.Width = 340;
        foreach (string p in PrinterSettings.InstalledPrinters) _cmbPrinter.Items.Add(p);
        string saved = ThermalReceiptService.Config.PrinterName;
        if (!string.IsNullOrWhiteSpace(saved) && _cmbPrinter.Items.Contains(saved)) _cmbPrinter.SelectedItem = saved;
        else if (_cmbPrinter.Items.Count > 0)
        {
            string def = new PrinterSettings().PrinterName;
            _cmbPrinter.SelectedItem = _cmbPrinter.Items.Contains(def) ? def : _cmbPrinter.Items[0];
        }

        var btnPrint = UITheme.CreateButton("🖨️ Fişi Yazdır", UITheme.Primary, Color.White, (s, e) => PrintClick(), 170, 36);
        btnPrint.Left = 374; btnPrint.Top = 152;

        // PDF + WhatsApp
        string phoneInfo = data.HasAccount
            ? (string.IsNullOrWhiteSpace(data.Phone) ? "Bu carinin kayıtlı telefonu yok; göndermek için numara sorulacak." : $"Kayıtlı numara: {data.Phone}")
            : "Perakende satış: göndermek için numara sorulacak.";
        var lblPdf = new Label
        {
            Text = (data.HasAccount ? "Cari belge (PDF): müşteri bilgileri, ürünler, ödeme ve güncel bakiye içerir.\n" : "Satış belgesi (PDF) hazırlayıp WhatsApp'tan gönderebilirsiniz.\n") + phoneInfo,
            ForeColor = UITheme.TextSecondary,
            Left = 24, Top = 212, Width = 510, Height = 44
        };

        var btnWa = UITheme.CreateButton("📲 PDF Hazırla ve WhatsApp'tan Gönder", Color.FromArgb(22, 163, 74), Color.White, (s, e) => WhatsAppClick(), 340, 38);
        btnWa.Left = 24; btnWa.Top = 262;

        _btnShowPdf = UITheme.CreateButton("📂 PDF'i Göster", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => { if (_pdfPath != null) SaleReceiptService.ShowInExplorer(_pdfPath); }, 160, 38);
        _btnShowPdf.Left = 374; _btnShowPdf.Top = 262; _btnShowPdf.Enabled = false;

        _lblStatus.Left = 24; _lblStatus.Top = 312; _lblStatus.Width = 510; _lblStatus.Height = 70;
        _lblStatus.ForeColor = UITheme.TextSecondary;

        var btnClose = UITheme.CreateButton("Kapat", UITheme.BorderColor, UITheme.TextPrimary, (s, e) => Close(), 110, 36);
        btnClose.Left = 424; btnClose.Top = 382;

        Controls.AddRange(new Control[] { lblTitle, lblSummary, lblPrinter, _cmbPrinter, btnPrint, lblPdf, btnWa, _btnShowPdf, _lblStatus, btnClose });
        AcceptButton = btnPrint;
        CancelButton = btnClose;

        Shown += (s, e) =>
        {
            // Cari müşteri için PDF belgesi otomatik hazırlanır
            if (_data.HasAccount) EnsurePdf();

            if (ThermalReceiptService.Config.AutoPrintOnSale && _cmbPrinter.Items.Count > 0)
            {
                PrintClick();
            }
        };
    }

    private bool EnsurePdf()
    {
        if (_pdfPath != null && File.Exists(_pdfPath)) return true;
        try
        {
            _pdfPath = SaleReceiptService.CreatePdf(_data);
            _btnShowPdf.Enabled = true;
            SetStatus($"📄 PDF belgesi hazırlandı:\n{_pdfPath}", false);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("PDF hazırlanamadı: " + ex.Message, true);
            return false;
        }
    }

    private void PrintClick()
    {
        string? printer = _cmbPrinter.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(printer))
        {
            SetStatus("Kurulu yazıcı bulunamadı. Windows'ta bir yazıcı ekleyin.", true);
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            var res = SaleReceiptService.PrintReceipt(_data, printer);
            SetStatus(res.Success ? $"🖨️ Fiş '{printer}' yazıcısına gönderildi." : res.Message, !res.Success);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void WhatsAppClick()
    {
        string phone = _data.Phone ?? "";
        if (string.IsNullOrWhiteSpace(phone))
        {
            phone = PromptDialog.Show("Müşterinin WhatsApp numarasını girin (örn: 0532 123 45 67):", "WhatsApp Numarası", "").Trim();
            if (string.IsNullOrWhiteSpace(phone)) return;
        }

        string normalized = WhatsAppService.NormalizePhoneNumber(phone);
        if (normalized.Length < 11)
        {
            SetStatus("Telefon numarası geçersiz görünüyor. Başında 0 veya 90 ile tam numara girin.", true);
            return;
        }

        if (!EnsurePdf()) return;

        var res = SaleReceiptService.PreparePdfForWhatsApp(_pdfPath!, phone, _data);
        SetStatus((res.Success ? "📲 " : "⚠️ ") + res.Message, !res.Success);
    }

    private void SetStatus(string text, bool error)
    {
        _lblStatus.ForeColor = error ? UITheme.Danger : UITheme.TextSecondary;
        _lblStatus.Text = text;
    }
}
