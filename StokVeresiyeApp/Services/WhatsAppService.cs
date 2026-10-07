using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Web;

namespace StokVeresiyeApp.Services;

public static class WhatsAppService
{
    public static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "";

        var sb = new StringBuilder();
        foreach (char c in phone)
        {
            if (char.IsDigit(c)) sb.Append(c);
        }

        string digits = sb.ToString();

        // 05xx -> 905xx
        if (digits.StartsWith("0") && digits.Length == 11)
        {
            return "90" + digits.Substring(1);
        }

        // 5xx (10 hane) -> 905xx
        if (digits.Length == 10 && digits.StartsWith("5"))
        {
            return "90" + digits;
        }

        return digits;
    }

    /// <summary>
    /// Müşteriye WhatsApp Web / Desktop üzerinden tek tıkla dijital fiş veya tahsilat makbuzu gönderir.
    /// </summary>
    public static bool SendSaleReceipt(
        string rawPhone, 
        string customerName, 
        double totalAmount, 
        double remainingDebt, 
        string docNo, 
        string paymentMethod, 
        List<(string Name, double Qty, double Price, double Total)>? items = null)
    {
        try
        {
            string phone = NormalizePhoneNumber(rawPhone);
            if (string.IsNullOrWhiteSpace(phone)) return false;

            var sb = new StringBuilder();
            sb.AppendLine($"Sayın *{customerName}*,");
            sb.AppendLine();
            sb.AppendLine($"🧾 *BİLGİ FİŞİ / İŞLEM ÖZETİ*");
            if (!string.IsNullOrWhiteSpace(docNo)) sb.AppendLine($"📄 *Belge No:* {docNo}");
            sb.AppendLine($"📅 *Tarih:* {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"💳 *Ödeme Türü:* {paymentMethod}");
            sb.AppendLine();

            if (items != null && items.Count > 0)
            {
                sb.AppendLine("📦 *Satın Alınan Ürünler:*");
                foreach (var it in items)
                {
                    sb.AppendLine($"• {it.Name} ({it.Qty:N2} adet x {it.Price:N2} ₺) = *{it.Total:N2} ₺*");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"💰 *İşlem Tutarı:* {totalAmount:N2} ₺");
            if (remainingDebt > 0)
            {
                sb.AppendLine($"⚠️ *Güncel Kalan Bakiyeniz (Borç):* *{remainingDebt:N2} ₺*");
            }
            else
            {
                sb.AppendLine($"✅ *Kalan Bakiyeniz:* 0,00 ₺ (Ödendi)");
            }

            sb.AppendLine();
            sb.AppendLine("Bizi tercih ettiğiniz için teşekkür eder, iyi günler dileriz! 🙏");

            string message = sb.ToString();
            string url = $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Müşteriye WhatsApp üzerinden güncel bakiye ekstresi özeti gönderir.
    /// </summary>
    public static bool SendBalanceStatement(string rawPhone, string customerName, double currentBalance)
    {
        try
        {
            string phone = NormalizePhoneNumber(rawPhone);
            if (string.IsNullOrWhiteSpace(phone)) return false;

            var sb = new StringBuilder();
            sb.AppendLine($"Sayın *{customerName}*,");
            sb.AppendLine();
            sb.AppendLine($"📊 *HESAP BAKİYE BİLGİLENDİRMESİ*");
            sb.AppendLine($"📅 *Tarih:* {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine();

            if (currentBalance > 0)
            {
                sb.AppendLine($"Kayıtlı cari hesabınızda güncel borç bakiyeniz: *{currentBalance:N2} ₺*");
                sb.AppendLine("Ödemenizi uygun olduğunuzda gerçekleştirmenizi rica ederiz.");
            }
            else if (currentBalance < 0)
            {
                sb.AppendLine($"Kayıtlı cari hesabınızda alacak bakiyeniz bulunmaktadır: *{Math.Abs(currentBalance):N2} ₺*");
            }
            else
            {
                sb.AppendLine($"Cari hesabınızda bekleyen borç/alacak bulunmamaktadır. Bakiyeniz: *0,00 ₺*");
            }

            sb.AppendLine();
            sb.AppendLine("Detaylı bilgi için işletmemizle iletişime geçebilirsiniz. İyi günler dileriz.");

            string message = sb.ToString();
            string url = $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }
}
