using System.Diagnostics;
using System.Text;

namespace StokVeresiyeApp.Services;

public static class ReceiptDigitalService
{
    public static string BuildReceiptText(
        string docNo,
        DateTime date,
        string customerName,
        string productName,
        double quantity,
        string unit,
        double unitPrice,
        double discountPercent,
        double vatPercent,
        double totalAmount,
        string paymentMethod,
        string? note = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🧾 *BİLENSİS DİJİTAL SATIŞ FİŞİ*");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine($"📅 *Tarih :* {date:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"📄 *Fiş No:* {docNo}");
        sb.AppendLine($"👤 *Müşteri:* {customerName}");
        sb.AppendLine("─────────────────────");
        sb.AppendLine($"📦 *Ürün :* {productName}");
        sb.AppendLine($"🔢 *Miktar:* {quantity:N2} {unit} x {unitPrice:N2} ₺");
        if (discountPercent > 0)
        {
            sb.AppendLine($"🏷️ *İskonto:* %{discountPercent:N0}");
        }
        sb.AppendLine($"📊 *KDV :* %{vatPercent:N0}");
        sb.AppendLine("─────────────────────");
        sb.AppendLine($"💰 *GENEL TOPLAM: {totalAmount:N2} ₺*");
        sb.AppendLine($"💳 *Ödeme Yöntemi:* {paymentMethod}");
        if (!string.IsNullOrWhiteSpace(note))
        {
            sb.AppendLine($"📝 *Not:* {note}");
        }
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("🙏 Bizi tercih ettiğiniz için teşekkür eder, bol kazançlar dileriz.");

        return sb.ToString();
    }

    public static string CleanPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("90") && digits.Length == 12)
        {
            return digits;
        }
        if (digits.StartsWith("0") && digits.Length == 11)
        {
            return "9" + digits;
        }
        if (digits.Length == 10)
        {
            return "90" + digits;
        }
        return digits;
    }

    public static void OpenWhatsApp(string phone, string message)
    {
        string cleanPhone = CleanPhoneNumber(phone);
        string encodedMsg = Uri.EscapeDataString(message);
        string url = string.IsNullOrEmpty(cleanPhone)
            ? $"https://wa.me/?text={encodedMsg}"
            : $"https://wa.me/{cleanPhone}?text={encodedMsg}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            throw new Exception("WhatsApp Web / Uygulaması başlatılamadı: " + ex.Message);
        }
    }
}
