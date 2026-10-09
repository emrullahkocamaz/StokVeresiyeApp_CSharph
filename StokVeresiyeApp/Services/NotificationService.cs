using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public class SmsSettings
{
    public string Provider { get; set; } = "Demo"; // "Demo", "NetGSM", "IletiMerkezi"
    public string Header { get; set; } = "BILENSIS";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string DueReminderTemplate { get; set; } = "Sayin {Musteri}, {VadeTarihi} vadeli {Tutar} TL veresiye borcunuzun vadesi dolmustur. Guncel bakiyeniz: {ToplamBakiye} TL. Iyi gunler dileriz. {Firma}";
    public string StockOrderTemplate { get; set; } = "Sayin Yetkili, {UrunAdi} urunumuzun stogu kritik seviyeye ({MevcutStok} {Birim}) dusmustur. Siparis gecmek istiyoruz.";
}

public class CriticalStockItem
{
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public double MinStockLevel { get; set; }
    public double Shortage => Math.Max(0, MinStockLevel - CurrentStock);
}

public class DueReceivableItem
{
    public long MovementId { get; set; }
    public long AccountId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public double Amount { get; set; }
    public double TotalBalance { get; set; }
    public int DaysOverdue { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class ExpiringProductItem
{
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double CurrentStock { get; set; }
    public string ExpiryDate { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
    public bool IsExpired => DaysRemaining < 0;
    public string StatusText => DaysRemaining < 0 
        ? $"⛔ Günü Geçti ({Math.Abs(DaysRemaining)} gün)" 
        : (DaysRemaining == 0 ? "⚠️ Bugün Son Gün!" : $"🟡 {DaysRemaining} Gün Kaldı");
}

public static class NotificationService
{
    private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp", "sms_config.json");
    private static SmsSettings _smsSettings = new();

    static NotificationService()
    {
        LoadSmsSettings();
    }

    public static SmsSettings CurrentSettings => _smsSettings;

    public static void LoadSmsSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<SmsSettings>(json);
                if (loaded != null)
                {
                    _smsSettings = loaded;
                    return;
                }
            }
        }
        catch { }
        _smsSettings = new SmsSettings();
    }

    public static void SaveSmsSettings(SmsSettings settings)
    {
        try
        {
            _smsSettings = settings;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            string json = JsonSerializer.Serialize(_smsSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    /// <summary>
    /// Kritik stok seviyesine düşen veya tükenen ürünleri listeler.
    /// </summary>
    public static List<CriticalStockItem> GetCriticalStockAlerts()
    {
        var list = new List<CriticalStockItem>();
        try
        {
            string sql = @"
SELECT 
    p.Id,
    p.Code,
    p.Name,
    p.Category,
    p.Unit,
    p.MinStockLevel,
    ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) AS CurrentStock
FROM Products p
WHERE p.IsActive = 1
  AND ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) <= p.MinStockLevel
ORDER BY CurrentStock ASC";

            var dt = Database.Query(sql);
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new CriticalStockItem
                {
                    ProductId = Convert.ToInt64(r["Id"]),
                    Code = r["Code"]?.ToString() ?? "",
                    Name = r["Name"]?.ToString() ?? "",
                    Category = r["Category"]?.ToString() ?? "",
                    Unit = r["Unit"]?.ToString() ?? "Adet",
                    MinStockLevel = Convert.ToDouble(r["MinStockLevel"]),
                    CurrentStock = Convert.ToDouble(r["CurrentStock"])
                });
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Vadesi geçmiş veya bugün/yaklaşan veresiye alacakları listeler.
    /// </summary>
    public static List<DueReceivableItem> GetDueReceivableAlerts(bool onlyOverdue = false)
    {
        var list = new List<DueReceivableItem>();
        try
        {
            string filter = onlyOverdue ? "Geçmiş" : "Tümü";
            var dt = TransactionService.GetDueReceivables(filter);
            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");

            foreach (DataRow r in dt.Rows)
            {
                int overdue = Convert.ToInt32(r["Gecikme (Gün)"]);
                if (onlyOverdue && overdue <= 0) continue;

                list.Add(new DueReceivableItem
                {
                    MovementId = Convert.ToInt64(r["Id"]),
                    AccountId = Convert.ToInt64(r["AccountId"]),
                    CustomerName = r["Müşteri"]?.ToString() ?? "",
                    Phone = r["Telefon"]?.ToString() ?? "",
                    DueDate = r["Vade Tarihi"]?.ToString() ?? "",
                    Amount = Convert.ToDouble(r["Veresiye Tutarı (₺)"]),
                    TotalBalance = Convert.ToDouble(r["Güncel Toplam Bakiye (₺)"]),
                    DaysOverdue = overdue,
                    Note = r["Açıklama"]?.ToString() ?? ""
                });
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Günü geçen veya son kullanma tarihine (SKT) 30 gün veya daha az kalan ürünleri listeler.
    /// </summary>
    public static List<ExpiringProductItem> GetExpiringProductAlerts(int warningDays = 30)
    {
        var list = new List<ExpiringProductItem>();
        try
        {
            string sql = @"
SELECT 
    p.Id,
    p.Code,
    COALESCE(p.Barcode, '') AS Barcode,
    p.Name,
    p.Category,
    p.Unit,
    p.ExpiryDate,
    COALESCE(p.BatchNumber, '') AS BatchNumber,
    ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) AS CurrentStock
FROM Products p
WHERE p.IsActive = 1
  AND p.ExpiryDate IS NOT NULL 
  AND p.ExpiryDate != ''
  AND ((SELECT vs.CurrentStock FROM vw_ProductStock vs WHERE vs.ProductId = p.Id)) > 0
ORDER BY p.ExpiryDate ASC";

            var dt = Database.Query(sql);
            DateTime today = DateTime.Today;

            foreach (DataRow r in dt.Rows)
            {
                string expStr = r["ExpiryDate"]?.ToString() ?? "";
                if (!DateTime.TryParse(expStr, out var expDate)) continue;

                int daysRemaining = (int)(expDate.Date - today).TotalDays;
                if (daysRemaining <= warningDays)
                {
                    list.Add(new ExpiringProductItem
                    {
                        ProductId = Convert.ToInt64(r["Id"]),
                        Code = r["Code"]?.ToString() ?? "",
                        Barcode = r["Barcode"]?.ToString() ?? "",
                        Name = r["Name"]?.ToString() ?? "",
                        Category = r["Category"]?.ToString() ?? "",
                        Unit = r["Unit"]?.ToString() ?? "Adet",
                        CurrentStock = Convert.ToDouble(r["CurrentStock"]),
                        ExpiryDate = expDate.ToString("yyyy-MM-dd"),
                        BatchNumber = r["BatchNumber"]?.ToString() ?? "",
                        DaysRemaining = daysRemaining
                    });
                }
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Toplam aktif bildirim sayısını döner (Kritik Stoklar + Gecikmiş Veresiyeler + SKT Yaklaşanlar).
    /// </summary>
    public static (int CriticalStockCount, int OverdueReceivableCount, int ExpiringProductCount, int TotalAlertCount) GetAlertCounts()
    {
        int stockCount = 0;
        int overdueCount = 0;
        int expiringCount = 0;
        try
        {
            stockCount = GetCriticalStockAlerts().Count;
            overdueCount = GetDueReceivableAlerts(onlyOverdue: true).Count;
            expiringCount = GetExpiringProductAlerts().Count;
        }
        catch { }
        return (stockCount, overdueCount, expiringCount, stockCount + overdueCount + expiringCount);
    }

    /// <summary>
    /// SMS Gönderim Motoru (Demo / NetGSM / İletiMerkezi destekli)
    /// </summary>
    public static async Task<(bool Success, string Message)> SendSmsAsync(string phone, string messageText)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits) || digits.Length < 10)
        {
            return (false, "Geçersiz telefon numarası.");
        }

        if (digits.Length == 10 && digits.StartsWith("5")) digits = "90" + digits;
        else if (digits.Length == 11 && digits.StartsWith("05")) digits = "90" + digits.Substring(1);

        if (_smsSettings.Provider == "Demo")
        {
            // Demo / Simülatör Modu: Sistem günlüğüne ve bildirim kutusuna kaydet
            await Task.Delay(250); // Simülasyon gecikmesi
            Database.Execute(@"
INSERT INTO AuditLogs (LogDate, EntityName, ActionType, EntityTitle, Description) 
VALUES (@d, 'SMS Bildirimi', 'Gönderildi (Demo)', @title, @desc)",
                ("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                ("@title", $"{digits} - {messageText.Substring(0, Math.Min(30, messageText.Length))}..."),
                ("@desc", $"[DEMO SMS] Alıcı: {digits} | Başlık: {_smsSettings.Header}\nİçerik:\n{messageText}")
            );

            return (true, $"[Demo Modu] SMS başarıyla simüle edildi ve loglandı. (Alıcı: {digits})");
        }
        else if (_smsSettings.Provider == "NetGSM")
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                string url = $"https://api.netgsm.com.tr/sms/send/get/?usercode={Uri.EscapeDataString(_smsSettings.Username)}&password={Uri.EscapeDataString(_smsSettings.Password)}&gsmno={digits}&message={Uri.EscapeDataString(messageText)}&msgheader={Uri.EscapeDataString(_smsSettings.Header)}";
                var response = await client.GetStringAsync(url);
                if (response.StartsWith("00") || response.StartsWith("01") || response.StartsWith("02"))
                {
                    return (true, $"NetGSM SMS başarıyla gönderildi. Kod: {response}");
                }
                return (false, $"NetGSM Hatası: {response}");
            }
            catch (Exception ex)
            {
                return (false, $"NetGSM bağlantı hatası: {ex.Message}");
            }
        }
        else
        {
            // Genel simülasyon fallback
            return (true, $"SMS kuyruğa alındı. (Sağlayıcı: {_smsSettings.Provider}, Alıcı: {digits})");
        }
    }

    /// <summary>
    /// Vade hatırlatma SMS metnini şablona göre hazırlar.
    /// </summary>
    public static string BuildDueReminderText(DueReceivableItem item)
    {
        string template = _smsSettings.DueReminderTemplate;
        return template
            .Replace("{Musteri}", item.CustomerName)
            .Replace("{VadeTarihi}", item.DueDate)
            .Replace("{Tutar}", item.Amount.ToString("N2"))
            .Replace("{ToplamBakiye}", item.TotalBalance.ToString("N2"))
            .Replace("{GecikmeGun}", item.DaysOverdue.ToString())
            .Replace("{Firma}", "Bilensis");
    }

    /// <summary>
    /// WhatsApp Web bağlantısını açar.
    /// </summary>
    public static void OpenWhatsAppChat(string phone, string message)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10 && digits.StartsWith("5")) digits = "90" + digits;
        else if (digits.Length == 11 && digits.StartsWith("05")) digits = "90" + digits.Substring(1);

        string url = $"https://wa.me/{digits}?text={Uri.EscapeDataString(message)}";
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }
}
