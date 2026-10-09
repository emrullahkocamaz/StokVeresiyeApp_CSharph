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
        // 1. Açıkça "ALL" atanmışsa tam yetki
        if (Permissions == "ALL")
            return true;

        // 2. SuperAdmin her zaman tam yetkilidir
        if (IsSuperUser)
            return true;

        // 3. Kullanıcıya özel yetkiler atanmışsa
        if (!string.IsNullOrWhiteSpace(Permissions))
        {
            var perms = Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Birebir eşleşme kontrolü (Örn: "Products.Delete")
            if (perms.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
                return true;

            // Eğer sorgulanan yetki ana modül ise (Örn: "Products") ve kullanıcının o modülde herhangi bir alt yetkisi varsa
            // (Örn: "Products.Create" veya "Products.Delete") modül listesine erişebilir
            if (!permissionKey.Contains('.'))
            {
                if (perms.Any(p => p.Equals(permissionKey, StringComparison.OrdinalIgnoreCase) ||
                                   p.StartsWith(permissionKey + ".", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // Eğer sorgulanan yetki bir alt yetki ise (Örn: "Products.Create" veya "Products.Delete"),
            // ama kullanıcıda eski usul genel ana yetki ("Products") kayıtlıysa geriye uyumluluk olarak izin ver
            if (permissionKey.Contains('.'))
            {
                var parentKey = permissionKey.Split('.')[0];
                if (perms.Contains(parentKey, StringComparer.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // Yetki alanı boş bırakılmışsa Admin varsayılan tam yetkilidir
        if (Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

public class PermissionItem
{
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDeleteOrCritical { get; set; } = false; // Silme / Çıkarma / Kritik Yetki
}

public static class UserPermissions
{
    // === 1. ÜRÜN & STOK KARTLARI ===
    public const string Products = "Products";                         // Ürün Listesi & Görüntüleme
    public const string ProductsCreate = "Products.Create";             // Yeni Ürün Ekleme (Kart & Hızlı Giriş)
    public const string ProductsEdit = "Products.Edit";                 // Ürün Bilgi & Fiyat Düzenleme
    public const string ProductsDelete = "Products.Delete";             // Ürün Silme / Pasife Alma / Toplu Silme
    public const string ProductsBulkPrice = "Products.BulkPrice";       // Toplu Fiyat / Zam / İndirim Güncelleme
    public const string BarcodeLabels = "BarcodeLabels";               // Barkod ve Fiyat Etiketi Yazdırma
    public const string ProductVariants = "ProductVariants";           // Ürün Varyant Yönetimi (Renk/Beden)

    // === 2. CARİ HESAP & VERESİYE ===
    public const string Accounts = "Accounts";                         // Cari Kartları Görüntüleme & Arama
    public const string AccountsCreate = "Accounts.Create";             // Yeni Cari Hesap Ekleme
    public const string AccountsEdit = "Accounts.Edit";                 // Cari Bilgilerini & Limitlerini Düzenleme
    public const string AccountsDelete = "Accounts.Delete";             // Cari Hesap Silme / Pasife Alma
    public const string AccountStatement = "Accounts.Statement";       // Cari Hesap Ekstresi & Yazdırma
    public const string TaxLookup = "TaxLookup";                       // GİB / VKN / TCKN Otomatik Bilgi Sorgulama

    // === 3. FATURALAR & E-FATURA ===
    public const string Invoices = "Invoices";                         // Fatura Listesi & Arama
    public const string InvoiceEntry = "InvoiceEntry";                 // Yeni E-Fatura / Alış Faturası Girişi (XML/Manuel)
    public const string InvoicesBatchImport = "Invoices.BatchImport";   // Toplu PDF Fatura İçe Aktarma & Teşhis (Yeni)
    public const string InvoicesDelete = "Invoices.Delete";             // Fatura İptali & Silme (Stok ve Cariyi Geri Alma)
    public const string InvoicePayment = "InvoicePayment";             // Fatura Kısmi ve Tam Ödeme Yönetimi

    // === 4. STOK HAREKETLERİ & ENVANTER ===
    public const string StockMovements = "StockMovements";             // Stok Hareketlerini Görüntüleme
    public const string StockMovementsCreate = "StockMovements.Create"; // Manuel Stok Giriş & Çıkış Ekleme
    public const string StockMovementsDelete = "StockMovements.Delete"; // Stok Hareketi Silme / İptal Etme
    public const string StockCount = "StockCount";                     // Stok Sayımı & Envanter Düzeltme

    // === 5. KASA, TAHSİLAT & FİNANS ===
    public const string AccountMovements = "AccountMovements";         // Kasa & Finans Hareketleri Görüntüleme
    public const string FinanceCollect = "Finance.Collect";             // Tahsilat Alma (Nakit/POS/Banka)
    public const string FinancePay = "Finance.Pay";                     // Ödeme Yapma (Cari/Tedarikçi)
    public const string FinanceDebt = "Finance.Debt";                   // Borç Yazma / Manuel Hareket Ekleme
    public const string FinanceDeleteMovement = "Finance.DeleteMovement"; // Finans / Kasa Hareketini Silme
    public const string DailyRegister = "DailyRegister";               // Kasa Gün Sonu (Z Raporu Kapatma)
    public const string FinanceExpenses = "Finance.Expenses";           // Masraf & Gider Yönetimi (Ekle/Sil/Listele)
    public const string FinanceCurrency = "Finance.Currency";           // Canlı Döviz Kurları & Çevirici

    // === 6. HIZLI SATIŞ & KASA (POS) ===
    public const string QuickSale = "QuickSale";                       // Hızlı Satış ve Fiş Kesme
    public const string QuickBuy = "Sales.QuickBuy";                   // Hızlı Alış & İade İşlemleri
    public const string ParkedSales = "ParkedSales";                   // Fiş / Sepet Bekletme (Askıya Alma & Çağırma)
    public const string CustomerDisplay = "CustomerDisplay";           // Çift Ekran / Müşteri Bilgi Panosu
    public const string SplitPayment = "SplitPayment";                 // Parçalı & Çoklu Tahsilat (Nakit/Kart/Veresiye)
    public const string ScaleBarcode = "ScaleBarcode";                 // Elektronik Barkodlu Terazi & Tartılı Ürünler
    public const string FastQr = "Sales.FastQr";                       // FAST QR Karekod ile Ödeme
    public const string DigitalReceipt = "DigitalReceipt";             // WhatsApp & SMS Dijital Fiş İletimi

    // === 7. MOBİL & ÇEVRİMİÇİ ENTEGRASYONLAR ===
    public const string WhatsAppPortal = "Mobile.WhatsAppPortal";       // WhatsApp Asistanı & Web Mobil Portal (Yeni)
    public const string MobileScanner = "MobileScanner";               // Mobil Canlı Barkod Okuyucu (Web & Kamera)
    public const string Notifications = "Notifications";               // Bildirim Merkezi, SMS ve SKT Takibi

    // === 8. DEPO & SEVKİYAT ===
    public const string Warehouses = "Warehouses";                     // Depo Tanımlama & Yönetimi
    public const string WarehouseTransfer = "Warehouses.Transfer";     // Depolar Arası Stok Transferi

    // === 9. RAPORLAMA & VERİ AKTARIMI ===
    public const string Reports = "Reports";                           // Raporlar, İstatistikler ve Grafikler
    public const string DeadStockReport = "Reports.DeadStock";         // Hareketsiz / Ölü Stok Analiz Raporu
    public const string DueReceivables = "Reports.DueReceivables";     // Vadesi Geçen Alacaklar Raporu
    public const string ExcelOperations = "ExcelOperations";           // Excel İle Toplu Veri Yükleme ve Dışa Aktarma

    // === 10. SİSTEM, GÜVENLİK & YÖNETİM ===
    public const string Users = "Users";                               // Kullanıcı Tanımlama ve Yetki Yönetimi
    public const string SettingsBackup = "SettingsBackup";             // Sistem Ayarları ve Yerel Yedekleme
    public const string CloudBackup = "CloudBackup";                   // Otomatik Bulut & Harici Disk Yedekleme
    public const string SystemReset = "System.Reset";                   // Fabrika Ayarlarına Sıfırlama (Kritik Yetki)
    public const string AuditLogs = "AuditLogs";                       // Sistem Değişiklik ve Silme Denetim Logları
    public const string License = "License";                           // Lisans Durumu ve Aktivasyon Yönetimi

    public static readonly List<PermissionItem> AllPermissions = new()
    {
        // 1. Ürün & Stok
        new() { Key = Products, Category = "📦 Ürün & Stok Yönetimi", Title = "Ürün Listesi & Stok Görüntüleme", Description = "Ürünleri ve stok miktarlarını listeleme" },
        new() { Key = ProductsCreate, Category = "📦 Ürün & Stok Yönetimi", Title = "Yeni Ürün Ekleme", Description = "Yeni ürün kartı açma ve hızlı ürün ekleme" },
        new() { Key = ProductsEdit, Category = "📦 Ürün & Stok Yönetimi", Title = "Ürün Düzenleme & Fiyat Güncelleme", Description = "Ürün bilgilerini, alış/satış fiyatlarını değiştirme" },
        new() { Key = ProductsDelete, Category = "📦 Ürün & Stok Yönetimi", Title = "Ürün Silme / Pasife Alma (Çıkarma)", Description = "Ürünleri sistemden silme veya pasife alma", IsDeleteOrCritical = true },
        new() { Key = ProductsBulkPrice, Category = "📦 Ürün & Stok Yönetimi", Title = "Toplu Zam & İndirim Güncelleme", Description = "Tüm veya seçili ürünlere toplu oranla zam/indirim yapma" },
        new() { Key = BarcodeLabels, Category = "📦 Ürün & Stok Yönetimi", Title = "Barkod & Fiyat Etiketi Basma", Description = "Termal yazıcıdan barkod ve raf fiyat etiketi yazdırma" },
        new() { Key = ProductVariants, Category = "📦 Ürün & Stok Yönetimi", Title = "Ürün Varyant Yönetimi", Description = "Renk, beden, numara varyantları ekleme/silme" },

        // 2. Cari & Veresiye
        new() { Key = Accounts, Category = "👥 Cari Hesap & Veresiye", Title = "Cari Kartları Görüntüleme", Description = "Müşteri ve tedarikçi listesi, bakiye görüntüleme" },
        new() { Key = AccountsCreate, Category = "👥 Cari Hesap & Veresiye", Title = "Yeni Cari Hesap Ekleme", Description = "Yeni müşteri veya tedarikçi hesabı açma" },
        new() { Key = AccountsEdit, Category = "👥 Cari Hesap & Veresiye", Title = "Cari Bilgilerini Düzenleme", Description = "Cari iletişim, limit ve adres bilgilerini güncelleme" },
        new() { Key = AccountsDelete, Category = "👥 Cari Hesap & Veresiye", Title = "Cari Hesap Silme / Pasife Alma (Çıkarma)", Description = "Cari hesap kartını silme veya arşive alma", IsDeleteOrCritical = true },
        new() { Key = AccountStatement, Category = "👥 Cari Hesap & Veresiye", Title = "Cari Ekstre & Yazdırma", Description = "Cari hesap hareket ekstresini inceleme ve döküm alma" },
        new() { Key = TaxLookup, Category = "👥 Cari Hesap & Veresiye", Title = "GİB / VKN / TCKN Bilgi Sorgulama", Description = "Vergi kimlik numarasından otomatik unvan/adres çekme" },

        // 3. Faturalar & E-Fatura
        new() { Key = Invoices, Category = "📄 Faturalar & E-Fatura", Title = "Fatura Listesi Görüntüleme", Description = "Kayıtlı faturaları ve detaylarını inceleme" },
        new() { Key = InvoiceEntry, Category = "📄 Faturalar & E-Fatura", Title = "Yeni Fatura Girişi (Manuel/XML)", Description = "Alış ve satış faturası girme, kalemleri kaydetme" },
        new() { Key = InvoicesBatchImport, Category = "📄 Faturalar & E-Fatura", Title = "Toplu PDF Fatura İçe Aktarma (Yeni)", Description = "Birden fazla PDF faturayı otomatik ayrıştırma ve toplu stok/cari kaydetme" },
        new() { Key = InvoicesDelete, Category = "📄 Faturalar & E-Fatura", Title = "Fatura İptali & Silme (Geri Alma)", Description = "Faturayı iptal ederek stok ve cari bakiyeyi eski haline getirme", IsDeleteOrCritical = true },
        new() { Key = InvoicePayment, Category = "📄 Faturalar & E-Fatura", Title = "Fatura Ödeme Yönetimi", Description = "Faturalara kısmi veya tam tahsilat/ödeme kaydetme" },

        // 4. Stok Hareketleri & Envanter
        new() { Key = StockMovements, Category = "🔄 Stok Hareketleri & Envanter", Title = "Stok Hareketlerini Görüntüleme", Description = "Giriş ve çıkış hareket geçmişini izleme" },
        new() { Key = StockMovementsCreate, Category = "🔄 Stok Hareketleri & Envanter", Title = "Manuel Stok Giriş / Çıkış Ekleme", Description = "Depo girişi, çıkışı, fire veya düzeltme kaydetme" },
        new() { Key = StockMovementsDelete, Category = "🔄 Stok Hareketleri & Envanter", Title = "Stok Hareketini Silme / İptal Etme (Çıkarma)", Description = "Hatalı girilmiş stok hareketini silme", IsDeleteOrCritical = true },
        new() { Key = StockCount, Category = "🔄 Stok Hareketleri & Envanter", Title = "Stok Sayımı & Envanter Düzeltme", Description = "Fiziki stok sayımı yapıp mevcut stokları eşitleme" },

        // 5. Kasa, Tahsilat & Finans
        new() { Key = AccountMovements, Category = "💳 Kasa, Tahsilat & Finans", Title = "Kasa & Finans Hareketleri Görüntüleme", Description = "Kasa giriş-çıkışları ve cari bakiyeleri izleme" },
        new() { Key = FinanceCollect, Category = "💳 Kasa, Tahsilat & Finans", Title = "Tahsilat Alma (Nakit/Kart/Banka)", Description = "Müşteriden nakit veya kredi kartıyla tahsilat yapma" },
        new() { Key = FinancePay, Category = "💳 Kasa, Tahsilat & Finans", Title = "Ödeme Yapma (Tedarikçi/Cari)", Description = "Tedarikçiye nakit veya bankadan ödeme yapma" },
        new() { Key = FinanceDebt, Category = "💳 Kasa, Tahsilat & Finans", Title = "Borç / Alacak Hareketi Ekleme", Description = "Cariye manuel borç veya alacak faturası/kaydı işleme" },
        new() { Key = FinanceDeleteMovement, Category = "💳 Kasa, Tahsilat & Finans", Title = "Finans / Kasa Hareketini Silme (Çıkarma)", Description = "Kasa veya cari finans hareketini sistemden silme", IsDeleteOrCritical = true },
        new() { Key = DailyRegister, Category = "💳 Kasa, Tahsilat & Finans", Title = "Kasa Gün Sonu (Z Raporu Kapatma)", Description = "Günün nakit/pos toplamlarını kapatma ve Z raporu alma" },
        new() { Key = FinanceExpenses, Category = "💳 Kasa, Tahsilat & Finans", Title = "Masraf & Gider Yönetimi", Description = "Kira, fatura, personel vb. işletme giderlerini kaydetme" },
        new() { Key = FinanceCurrency, Category = "💳 Kasa, Tahsilat & Finans", Title = "Canlı Döviz Kurları & Çevirici", Description = "TCMB canlı kurlarını takip etme ve dövizli hesaplama" },

        // 6. Hızlı Satış & POS
        new() { Key = QuickSale, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Hızlı Satış ve Fiş Kesme", Description = "Barkodlu market/mağaza perakende satış ekranı" },
        new() { Key = QuickBuy, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Hızlı Alış & İade İşlemleri", Description = "Kasadan anında müşteri iadesi veya hızlı alış yapma" },
        new() { Key = ParkedSales, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Fiş Bekletme (Askıya Alma & Çağırma)", Description = "Sıradaki müşteriye geçmek için sepeti askıya alma" },
        new() { Key = CustomerDisplay, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Çift Ekran / Müşteri Bilgi Panosu", Description = "İkinci monitörde müşteriye sepet ve toplam tutarı gösterme" },
        new() { Key = SplitPayment, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Parçalı & Çoklu Tahsilat", Description = "Aynı fişte bir kısmı nakit, bir kısmı kart veya veresiye alma" },
        new() { Key = ScaleBarcode, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "Elektronik Terazi & Tartılı Ürünler", Description = "27/28 ile başlayan terazi etiketlerini otomatik gramajla okuma" },
        new() { Key = FastQr, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "FAST QR Karekod ile Tahsilat", Description = "Merkez Bankası FAST QR kodu ile anında cep telefonundan ödeme alma" },
        new() { Key = DigitalReceipt, Category = "⚡ Hızlı Satış & Kasa (POS)", Title = "WhatsApp & SMS Dijital Fiş", Description = "Satış fişini müşterinin WhatsApp numarasına gönderme" },

        // 7. Mobil & Çevrimiçi Entegrasyonlar
        new() { Key = WhatsAppPortal, Category = "📱 Mobil & Çevrimiçi Entegrasyonlar", Title = "WhatsApp Asistanı & Web Mobil Portal (Yeni)", Description = "Cep telefonundan online stok sorgulama, bakiye bakma ve mobil yönetim" },
        new() { Key = MobileScanner, Category = "📱 Mobil & Çevrimiçi Entegrasyonlar", Title = "Mobil Canlı Barkod Okuyucu", Description = "Telefon kamerasını kablosuz el terminali gibi kullanma" },
        new() { Key = Notifications, Category = "📱 Mobil & Çevrimiçi Entegrasyonlar", Title = "Bildirim Merkezi & Vade / SKT Takibi", Description = "Günü geçen borçlar ve tarihi yaklaşan ürünler uyarısı" },

        // 8. Depo & Sevkiyat
        new() { Key = Warehouses, Category = "🏢 Depo & Lojistik", Title = "Depo Tanımlama & Yönetimi", Description = "Yeni depo veya şube açma, depo listesi" },
        new() { Key = WarehouseTransfer, Category = "🏢 Depo & Lojistik", Title = "Depolar Arası Stok Transferi", Description = "Bir depodan diğer depoya ürün sevk etme" },

        // 9. Raporlar & Dışa Aktarma
        new() { Key = Reports, Category = "📊 Raporlar & Dışa Aktarma", Title = "Genel Raporlar & Grafikler", Description = "Satış, ciro, kâr-zarar ve envanter analizleri" },
        new() { Key = DeadStockReport, Category = "📊 Raporlar & Dışa Aktarma", Title = "Hareketsiz / Ölü Stok Raporu", Description = "Belirli süredir satılmayan malları listeleme" },
        new() { Key = DueReceivables, Category = "📊 Raporlar & Dışa Aktarma", Title = "Vadesi Geçen Alacaklar Raporu", Description = "Ödeme süresi dolmuş müşterilerin listesi" },
        new() { Key = ExcelOperations, Category = "📊 Raporlar & Dışa Aktarma", Title = "Excel İle Toplu Veri Yükleme & Dışa Aktarma", Description = "Excel'den toplu ürün yükleme ve listeleri Excel'e aktarma" },

        // 10. Sistem, Güvenlik & Yönetim
        new() { Key = Users, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Kullanıcı Tanımlama & Yetki Yönetimi", Description = "Yeni personel ekleme ve yetkilerini belirleme", IsDeleteOrCritical = true },
        new() { Key = SettingsBackup, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Sistem Ayarları & Yerel Yedekleme", Description = "Veritabanını yerel diske yedekleme ve sistem parametreleri" },
        new() { Key = CloudBackup, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Otomatik Bulut & Harici Disk Yedekleme", Description = "Google Drive / Harici disk senkronizasyonu" },
        new() { Key = SystemReset, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Fabrika Ayarlarına Sıfırlama (Kritik Yetki)", Description = "Veritabanını ve test verilerini tamamen temizleme/sıfırlama", IsDeleteOrCritical = true },
        new() { Key = AuditLogs, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Sistem Denetim Logları", Description = "Kimin ne zaman hangi ürünü veya faturayı sildiğini inceleme" },
        new() { Key = License, Category = "⚙️ Sistem, Güvenlik & Yönetim", Title = "Lisans Durumu ve Aktivasyon", Description = "Program lisans anahtarı ve geçerlilik yönetimi" }
    };

    public static readonly Dictionary<string, string> Descriptions = AllPermissions.ToDictionary(p => p.Key, p => p.Title);
}
