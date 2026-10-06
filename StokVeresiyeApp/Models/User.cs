namespace StokVeresiyeApp.Models;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Kullanıcı"; // Admin, Yönetici, Kullanıcı, Kasiyer
    public string Permissions { get; set; } = string.Empty; // Virgülle ayrılmış yetki kodları veya ALL
    public string AssignedWarehouses { get; set; } = "ALL"; // ALL veya "1,2"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool HasWarehousePermission(long warehouseId)
    {
        if (IsSuperUser || AssignedWarehouses == "ALL" || string.IsNullOrWhiteSpace(AssignedWarehouses))
            return true;
        var parts = AssignedWarehouses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Contains(warehouseId.ToString());
    }

    public bool IsSuperUser => Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) || 
                               Username.Equals("super", StringComparison.OrdinalIgnoreCase) ||
                               Username.Equals("superuser", StringComparison.OrdinalIgnoreCase);

    public bool HasPermission(string permissionKey)
    {
        // Açıkça "ALL" atanmışsa tam yetki
        if (Permissions == "ALL")
            return true;

        // Kullanıcıya özel yetkiler atanmışsa bu yetkileri dikkate al (Admin/Super dahil)
        if (!string.IsNullOrWhiteSpace(Permissions))
        {
            var perms = Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return perms.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
        }

        // Yetki alanı boş bırakılmışsa Super ve Admin varsayılan tam yetkilidir
        if (IsSuperUser || Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

public static class UserPermissions
{
    public const string QuickSale = "QuickSale";               // Hızlı Satış ve Fiş Kesme
    public const string InvoiceEntry = "InvoiceEntry";         // E-Fatura / Alış Faturası Girişi
    public const string Products = "Products";                 // Ürün & Stok Kartları Yönetimi
    public const string Accounts = "Accounts";                 // Cari Hesap (Müşteri & Tedarikçi) Yönetimi
    public const string StockMovements = "StockMovements";     // Manuel Stok Giriş & Çıkış İşlemleri
    public const string AccountMovements = "AccountMovements"; // Kasa, Tahsilat ve Ödeme Finansal İşlemleri
    public const string StockCount = "StockCount";             // Stok Sayımı & Envanter Mutabakatı
    public const string Warehouses = "Warehouses";             // Depo Yönetimi & Depolar Arası Transfer
    public const string BarcodeLabels = "BarcodeLabels";       // Barkod ve Fiyat Etiketi Yazdırma
    public const string MobileScanner = "MobileScanner";       // Mobil Canlı Barkod Okuma (Telefon Kamerası)
    public const string Notifications = "Notifications";       // Bildirim Merkezi & SKT / Vade Takibi
    public const string Telegram = "Telegram";                 // Telegram Botu ve Canlı Cep Bildirimleri
    public const string Reports = "Reports";                   // Raporlar, İstatistikler ve Grafikler
    public const string ExcelOperations = "ExcelOperations";   // Excel İle Toplu Veri Yükleme ve Dışa Aktarma
    public const string AuditLogs = "AuditLogs";               // Sistem Değişiklik ve Silme Denetim Logları
    public const string Users = "Users";                       // Kullanıcı Tanımlama ve Yetki Yönetimi
    public const string SettingsBackup = "SettingsBackup";     // Sistem Ayarları ve Veritabanı Yedekleme
    public const string License = "License";                   // Lisans Durumu ve Aktivasyon Bilgileri
    public const string ParkedSales = "ParkedSales";           // Fiş / Sepet Bekletme (Askıya Alma)
    public const string SplitPayment = "SplitPayment";         // Parçalı & Çoklu Tahsilat
    public const string ProductVariants = "ProductVariants";   // Ürün Varyant Yönetimi (Renk/Beden)
    public const string DailyRegister = "DailyRegister";       // Kasa Gün Sonu (Z Raporu Kapatma)
    public const string DigitalReceipt = "DigitalReceipt";     // WhatsApp & SMS Dijital Fiş İletimi
    public const string InvoicePayment = "InvoicePayment";     // Fatura Kısmi Ödeme Yönetimi

    public static readonly Dictionary<string, string> Descriptions = new()
    {
        { QuickSale, "⚡ Hızlı Satış ve Fiş Kesme Modülü" },
        { ParkedSales, "⏸️ Fiş / Sepet Bekletme (Askıya Alma & Çağırma)" },
        { SplitPayment, "💳 Parçalı & Çoklu Tahsilat (Nakit/Kart/Veresiye)" },
        { ProductVariants, "🎨 Ürün Varyant Yönetimi (Renk, Beden, Numara)" },
        { DailyRegister, "🔒 Kasa Gün Sonu (Z Raporu & Kasa Kapatma)" },
        { DigitalReceipt, "📲 WhatsApp & SMS ile Dijital Fiş İletimi" },
        { InvoiceEntry, "📄 E-Fatura ve Alış Faturası Girişi (XML & PDF)" },
        { InvoicePayment, "💵 Fatura Kısmi ve Tam Ödeme Yönetimi" },
        { Products, "📦 Ürün ve Stok Kartları Yönetimi" },
        { Accounts, "👥 Cari Kartlar (Müşteri & Tedarikçi) Yönetimi" },
        { StockMovements, "🔄 Stok Giriş ve Çıkış Hareketleri" },
        { AccountMovements, "💳 Kasa, Tahsilat ve Ödeme Finansal İşlemleri" },
        { StockCount, "📋 Stok Sayımı ve Depo Envanter Düzeltme" },
        { Warehouses, "🏢 Depo Tanımlama ve Depolar Arası Transfer" },
        { BarcodeLabels, "🏷️ Barkod ve Fiyat Etiketi Tasarımı & Basma" },
        { MobileScanner, "📱 Mobil Canlı Barkod Okuyucu (Web & Kamera)" },
        { Notifications, "🔔 Bildirimler, SMS ve SKT & Parti Takibi" },
        { Telegram, "🤖 Telegram Bot Entegrasyonu ve Bildirimler" },
        { Reports, "📊 Raporlar, İstatistikler ve Excel Analizleri" },
        { ExcelOperations, "📥 Excel'den Toplu Veri Yükleme ve Dışa Aktarma" },
        { AuditLogs, "📜 Sistem Değişiklik ve Silme Denetim Logları" },
        { Users, "👤 Kullanıcı Tanımlama ve Yetki Yönetimi" },
        { SettingsBackup, "⚙️ Sistem Ayarları ve Veritabanı Yedekleme" },
        { License, "🔑 Lisans Durumu ve Aktivasyon Yönetimi" }
    };
}
