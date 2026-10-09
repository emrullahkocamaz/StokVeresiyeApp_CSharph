using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Web;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class WhatsAppService
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp");
    private static readonly string ConfigPath = Path.Combine(Folder, "whatsapp_config.json");
    private static WhatsAppConfig _config = new();

    public static WhatsAppConfig Config => _config;

    static WhatsAppService()
    {
        LoadConfig();
    }

    public static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<WhatsAppConfig>(json);
                if (loaded != null) _config = loaded;
            }
        }
        catch { }
    }

    public static void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

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

    public static bool OpenWhatsAppUrl(string rawPhone, string message)
    {
        try
        {
            string phone = NormalizePhoneNumber(rawPhone);
            string url = string.IsNullOrWhiteSpace(phone)
                ? $"https://wa.me/?text={Uri.EscapeDataString(message)}"
                : $"https://wa.me/{phone}?text={Uri.EscapeDataString(message)}";

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

            return OpenWhatsAppUrl(phone, sb.ToString());
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

            return OpenWhatsAppUrl(phone, sb.ToString());
        }
        catch
        {
            return false;
        }
    }

    // =========================================================================
    // YÖNETİCİ MOBİL İŞLEM VE ONLİNE ASİSTAN ENTEGRASYONU
    // =========================================================================

    /// <summary>
    /// Yöneticinin WhatsApp'ına cep telefonundan anında bağlanabileceği Mobil Yönetici Portalı linkini gönderir.
    /// </summary>
    public static bool SendAdminPortalLink(string adminPhone, string portalUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📱 *BİLENSİS ONLİNE MOBİL YÖNETİCİ PORTALI*");
        sb.AppendLine();
        sb.AppendLine("Dükkanınızdaki işlemleri cep telefonunuzdan anlık takip etmek ve yönetmek için bağlantıya dokunun:");
        sb.AppendLine();
        sb.AppendLine($"👉 *Giriş Linki:* {portalUrl}");
        sb.AppendLine();
        sb.AppendLine("💡 *Mobilden Yapabilecekleriniz:*");
        sb.AppendLine("• 📊 Canlı Kasa, Günlük Ciro ve Satış Dağılımı");
        sb.AppendLine("• 📷 Telefon kamerasıyla barkod okutma & anlık stok/fiyat sorgusu");
        sb.AppendLine("• 👥 Müşteri borçlular listesi & tek dokunuşla WhatsApp hatırlatması");
        sb.AppendLine("• 💵 Cepten anında hızlı tahsilat kaydetme");
        sb.AppendLine();
        sb.AppendLine("*(Not: Telefonunuzun dükkanın Wi-Fi ağına bağlı olması gerekmektedir.)*");

        return OpenWhatsAppUrl(adminPhone, sb.ToString());
    }

    /// <summary>
    /// Yöneticinin WhatsApp'ına güncel kasa ve ciro özetini gönderir.
    /// </summary>
    public static bool SendAdminCashSummary(string adminPhone)
    {
        string message = GenerateCashSummaryReport();
        return OpenWhatsAppUrl(adminPhone, message);
    }

    /// <summary>
    /// Yöneticinin WhatsApp'ına kritik stok seviyesindeki ürünlerin listesini gönderir.
    /// </summary>
    public static bool SendAdminCriticalStocks(string adminPhone)
    {
        string message = GenerateCriticalStockReport();
        return OpenWhatsAppUrl(adminPhone, message);
    }

    /// <summary>
    /// Yöneticinin WhatsApp'ına en çok borcu olan müşterilerin özetini gönderir.
    /// </summary>
    public static bool SendAdminTopDebtors(string adminPhone)
    {
        string message = GenerateTopDebtorsReport();
        return OpenWhatsAppUrl(adminPhone, message);
    }

    // =========================================================================
    // RAPOR ÜRETEÇLERİ VE WHATSAPP METİN OLUŞTURUCUSU
    // =========================================================================

    public static string GenerateCashSummaryReport()
    {
        try
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");

            // Bugünkü satışlar
            var dtSales = Database.Query(@"
SELECT 
    COALESCE(SUM(CASE WHEN Method = 'Nakit' THEN Amount ELSE 0 END), 0) AS CashSale,
    COALESCE(SUM(CASE WHEN Method = 'Kredi Kartı' THEN Amount ELSE 0 END), 0) AS PosSale,
    COALESCE(SUM(CASE WHEN Method = 'Veresiye' THEN Amount ELSE 0 END), 0) AS CreditSale,
    COALESCE(SUM(Amount), 0) AS TotalSale,
    COUNT(*) AS SaleCount
FROM AccountMovements
WHERE TransactionType = 'Satış' AND MovementDate LIKE @today;",
                ("@today", today + "%")
            );

            double cashSale = 0, posSale = 0, creditSale = 0, totalSale = 0;
            int saleCount = 0;
            if (dtSales.Rows.Count > 0)
            {
                var r = dtSales.Rows[0];
                cashSale = Convert.ToDouble(r["CashSale"]);
                posSale = Convert.ToDouble(r["PosSale"]);
                creditSale = Convert.ToDouble(r["CreditSale"]);
                totalSale = Convert.ToDouble(r["TotalSale"]);
                saleCount = Convert.ToInt32(r["SaleCount"]);
            }

            // Bugünkü tahsilatlar
            var dtCol = Database.Query(@"
SELECT COALESCE(SUM(Amount), 0) AS TotalCol, COUNT(*) AS ColCount
FROM AccountMovements
WHERE TransactionType = 'Tahsilat' AND MovementDate LIKE @today;",
                ("@today", today + "%")
            );
            double totalCol = dtCol.Rows.Count > 0 ? Convert.ToDouble(dtCol.Rows[0]["TotalCol"]) : 0;

            // Kasa Nakit Durumu
            var dtCash = Database.Query(@"
SELECT 
    COALESCE(SUM(CASE WHEN TransactionType IN ('Satış', 'Tahsilat') AND Method = 'Nakit' THEN Amount 
                      WHEN TransactionType IN ('Ödeme', 'Alış') AND Method = 'Nakit' THEN -Amount 
                      ELSE 0 END), 0) AS NetCash
FROM AccountMovements;");
            double netCash = dtCash.Rows.Count > 0 ? Convert.ToDouble(dtCash.Rows[0]["NetCash"]) : 0;

            var sb = new StringBuilder();
            sb.AppendLine("📊 *BİLENSİS GÜNLÜK KASA & CİRO ÖZETİ*");
            sb.AppendLine($"📅 Tarih: *{DateTime.Now:dd.MM.yyyy HH:mm}*");
            sb.AppendLine("───────────────────────");
            sb.AppendLine($"💰 *Bugünkü Toplam Ciro:* *{totalSale:N2} ₺* ({saleCount} işlem)");
            sb.AppendLine($"  💵 Nakit Satış: {cashSale:N2} ₺");
            sb.AppendLine($"  💳 Kredi Kartı: {posSale:N2} ₺");
            sb.AppendLine($"  📝 Veresiye Satış: {creditSale:N2} ₺");
            sb.AppendLine();
            sb.AppendLine($"📥 *Tahsil Edilen Borç:* {totalCol:N2} ₺");
            sb.AppendLine($"🏦 *Güncel Kasa Nakit:* *{netCash:N2} ₺*");
            sb.AppendLine("───────────────────────");
            sb.AppendLine("İyi çalışmalar, bereketli kazançlar dileriz! 🚀");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "❌ Kasa özeti oluşturulurken hata: " + ex.Message;
        }
    }

    public static string GenerateCriticalStockReport()
    {
        try
        {
            var dt = Database.Query(@"
SELECT TOP 15 p.Code, p.Barcode, p.Name, p.MinStockLevel,
       (p.OpeningStock + 
        COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Gelen', 'İade Giriş')), 0) -
        COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış')), 0)
       ) AS CurrentStock
FROM Products p
WHERE p.IsActive = 1 AND
      (p.OpeningStock + 
       COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Gelen', 'İade Giriş')), 0) -
       COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış')), 0)
      ) <= p.MinStockLevel
ORDER BY CurrentStock ASC;");

            var sb = new StringBuilder();
            sb.AppendLine("⚠️ *KRİTİK STOK UYARI RAPORU*");
            sb.AppendLine($"📅 Tarih: *{DateTime.Now:dd.MM.yyyy HH:mm}*");
            sb.AppendLine("───────────────────────");

            if (dt.Rows.Count == 0)
            {
                sb.AppendLine("✅ Tebrikler! Kritik seviyenin altına inen hiçbir ürününüz bulunmamaktadır.");
                return sb.ToString();
            }

            int idx = 1;
            foreach (DataRow r in dt.Rows)
            {
                string name = r["Name"].ToString() ?? "";
                double cur = Convert.ToDouble(r["CurrentStock"]);
                double min = Convert.ToDouble(r["MinStockLevel"]);
                sb.AppendLine($"{idx++}. *{name}*");
                sb.AppendLine($"   Kalan: *{cur:N0} adet* (Kritik Eşik: {min:N0})");
            }
            sb.AppendLine("───────────────────────");
            sb.AppendLine("Sipariş vermeniz tavsiye edilir! 📦");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "❌ Kritik stok raporu hatası: " + ex.Message;
        }
    }

    public static string GenerateTopDebtorsReport()
    {
        try
        {
            var dt = Database.Query(@"
SELECT TOP 15 Id, Name, Phone, Balance
FROM Accounts
WHERE IsActive = 1 AND Type = 'Müşteri' AND Balance > 0
ORDER BY Balance DESC;");

            var sb = new StringBuilder();
            sb.AppendLine("👥 *EN ÇOK BORCU OLAN MÜŞTERİLER (VERESİYE)*");
            sb.AppendLine($"📅 Tarih: *{DateTime.Now:dd.MM.yyyy HH:mm}*");
            sb.AppendLine("───────────────────────");

            if (dt.Rows.Count == 0)
            {
                sb.AppendLine("✅ Kayıtlı hiçbir müşterinizin borcu bulunmamaktadır.");
                return sb.ToString();
            }

            double totalDebt = 0;
            int idx = 1;
            foreach (DataRow r in dt.Rows)
            {
                string name = r["Name"].ToString() ?? "";
                string phone = r["Phone"]?.ToString() ?? "";
                double bal = Convert.ToDouble(r["Balance"]);
                totalDebt += bal;

                sb.AppendLine($"{idx++}. *{name}* : *{bal:N2} ₺*");
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    sb.AppendLine($"   Tel: {phone}");
                }
            }
            sb.AppendLine("───────────────────────");
            sb.AppendLine($"💰 *Seçili Toplam Alacak:* *{totalDebt:N2} ₺*");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "❌ Borçlu raporu hatası: " + ex.Message;
        }
    }

    /// <summary>
    /// WhatsApp'tan veya Webhook'tan gelen metin mesajını yorumlayıp otomatik cevap metni üretir.
    /// </summary>
    public static string FormatWhatsAppBotResponse(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return "Lütfen bir komut veya aramak istediğiniz ürün/barkod adını yazınız.";

        string cmd = command.Trim();

        if (cmd.Equals("KASA", StringComparison.OrdinalIgnoreCase) || cmd.Equals("/kasa", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateCashSummaryReport();
        }
        else if (cmd.Equals("STOK", StringComparison.OrdinalIgnoreCase) || cmd.Equals("KRITIK", StringComparison.OrdinalIgnoreCase) || cmd.Equals("/kritik", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateCriticalStockReport();
        }
        else if (cmd.Equals("BORC", StringComparison.OrdinalIgnoreCase) || cmd.Equals("BORCLULAR", StringComparison.OrdinalIgnoreCase) || cmd.Equals("/borclular", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateTopDebtorsReport();
        }
        else if (cmd.Equals("MOBIL", StringComparison.OrdinalIgnoreCase) || cmd.Equals("PORTAL", StringComparison.OrdinalIgnoreCase) || cmd.Equals("/mobil", StringComparison.OrdinalIgnoreCase))
        {
            string url = MobileScannerService.GetPrimaryUrl();
            return $"📱 *Bilensis Mobil Yönetim Portalı*\n\nTelefonunuzdan anlık kasa, stok ve tahsilat yapmak için tıklayınız:\n👉 {url}";
        }
        else if (cmd.Equals("YARDIM", StringComparison.OrdinalIgnoreCase) || cmd.Equals("MENU", StringComparison.OrdinalIgnoreCase) || cmd.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            return "🤖 *Bilensis WhatsApp Asistanı*\n\nKullanabileceğiniz komutlar:\n• *KASA* : Günlük ciro ve kasa nakit durumu\n• *STOK* : Kritik seviyeye düşen ürünler\n• *BORC* : En çok borcu olan müşteriler\n• *MOBIL* : Canlı cep telefonu portal linki\n• Ya da doğrudan bir *Barkod* veya *Ürün Adı* yazıp gönderin.";
        }
        else
        {
            // Ürün arama sorgusu
            try
            {
                var dt = Database.Query(@"
SELECT TOP 3 Id, Code, Barcode, Name, SalePrice, WholesalePrice,
       (OpeningStock + 
        COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Gelen', 'İade Giriş')), 0) -
        COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış')), 0)
       ) AS CurrentStock
FROM Products p
WHERE IsActive = 1 AND (Barcode = @q OR Code = @q OR Name LIKE @like)
ORDER BY CASE WHEN Barcode = @q THEN 0 WHEN Code = @q THEN 1 ELSE 2 END, Id DESC;",
                    ("@q", cmd),
                    ("@like", "%" + cmd + "%"));

                if (dt.Rows.Count == 0)
                {
                    return $"🔍 '{cmd}' ile eşleşen ürün bulunamadı.\nKomutlar için *YARDIM* yazabilirsiniz.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🔍 *'{cmd}' İÇİN BULUNAN ÜRÜNLER:*");
                sb.AppendLine();
                foreach (DataRow r in dt.Rows)
                {
                    string name = r["Name"].ToString() ?? "";
                    string barcode = r["Barcode"]?.ToString() ?? "";
                    double cur = Convert.ToDouble(r["CurrentStock"]);
                    double price = Convert.ToDouble(r["SalePrice"]);
                    double wholesale = Convert.ToDouble(r["WholesalePrice"]);

                    sb.AppendLine($"📦 *{name}*");
                    if (!string.IsNullOrWhiteSpace(barcode)) sb.AppendLine($"   Barkod: {barcode}");
                    sb.AppendLine($"   📊 Kalan Stok: *{cur:N0} Adet*");
                    sb.AppendLine($"   🏷️ Satış Fiyatı: *{price:N2} ₺*");
                    if (wholesale > 0) sb.AppendLine($"   💼 Toptan: {wholesale:N2} ₺");
                    sb.AppendLine("───────────────────────");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "❌ Arama hatası: " + ex.Message;
            }
        }
    }
}
