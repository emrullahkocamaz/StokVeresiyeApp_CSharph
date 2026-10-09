namespace StokVeresiyeApp.Models;

public class Product
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Genel";
    public string Unit { get; set; } = "Adet";
    public double OpeningStock { get; set; }
    public double PurchasePrice { get; set; }
    public double SalePrice { get; set; }
    public double WholesalePrice { get; set; } // Toptan Satış Fiyatı
    public double SpecialPrice { get; set; }   // Özel / Bayi Fiyatı
    public double DiscountPercent { get; set; }
    public double VatPercent { get; set; } = 20;
    public double MinStockLevel { get; set; } = 5;
    public double PackSize { get; set; } = 1; // Koli / paket içi adet (faturada koli yazarsa stoğa bu kadar adet girer)
    public bool IsActive { get; set; } = true;
    public string? ExpiryDate { get; set; } // yyyy-MM-dd
    public string? BatchNumber { get; set; } // Parti / Lot No
    public string? Features { get; set; } // Ürün Özellikleri / Formu (Sprey, Sıvı, Tablet, Toz vb.)

    // Hesaplanmış alanlar
    public double InQuantity { get; set; }
    public double OutQuantity { get; set; }
    public double CurrentStock => OpeningStock + InQuantity - OutQuantity;
    public double FinalSalePrice => SalePrice > 0 
        ? SalePrice 
        : PurchasePrice * (1 - DiscountPercent / 100.0) * (1 + VatPercent / 100.0);
    public double TotalStockValue => CurrentStock * FinalSalePrice;
    public bool IsCriticalStock => CurrentStock <= MinStockLevel;
    public bool IsExpired => !string.IsNullOrWhiteSpace(ExpiryDate) && DateTime.TryParse(ExpiryDate, out var dt) && dt.Date < DateTime.Today;
    public bool IsExpiringSoon => !string.IsNullOrWhiteSpace(ExpiryDate) && DateTime.TryParse(ExpiryDate, out var dt) && dt.Date >= DateTime.Today && (dt.Date - DateTime.Today).TotalDays <= 30;
    public int? DaysToExpiry => !string.IsNullOrWhiteSpace(ExpiryDate) && DateTime.TryParse(ExpiryDate, out var dt) ? (int)(dt.Date - DateTime.Today).TotalDays : null;
}

public class Warehouse
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string Type { get; set; } = "Depo"; // Depo, Şube, Araç / Saha
    public string? ResponsiblePerson { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockMovement
{
    public long Id { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.Now;
    public long ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = "Gelen"; // Gelen, Satılan, Düzeltme, İade, Transfer Giriş, Transfer Çıkış
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double TotalPrice => Quantity * UnitPrice;
    public string? Note { get; set; }
    public string? DocumentNo { get; set; }
    public long? AccountId { get; set; }
    public string? AccountName { get; set; }
    public long? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public long? TargetWarehouseId { get; set; }
    public string? TargetWarehouseName { get; set; }
    public string? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
}

public class Account
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Müşteri"; // Müşteri, Tedarikçi
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public string? Description { get; set; }
    public double BalanceLimit { get; set; }
    public string PriceGroup { get; set; } = "Perakende"; // Perakende, Toptan, Özel / Bayi
    public double DefaultDiscountPercent { get; set; } = 0; // Müşteriye özel sabit iskonto (%)
    public bool IsBlacklisted { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Hesaplanmış bakiye
    public double TotalDebit { get; set; }   // Borç (Müşteriye Satış veya Tedarikçiye Ödeme)
    public double TotalCredit { get; set; }  // Alacak (Müşteriden Tahsilat veya Tedarikçiden Alış)
    public double Balance => Type == "Müşteri" ? (TotalDebit - TotalCredit) : (TotalCredit - TotalDebit);
}

public class AccountMovement
{
    public long Id { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.Now;
    public long AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Satış"; // Satış, Tahsilat, Alış, Ödeme
    public string? DocumentNo { get; set; }
    public double Amount { get; set; }
    public string Method { get; set; } = "Nakit"; // Nakit, Kredi Kartı, Havale/EFT, Çek/Senet, Veresiye
    public string CashBank { get; set; } = "Merkez Kasa";
    public string? Note { get; set; }
    public string? DueDate { get; set; } // Vade / Ödeme Sözü Tarihi (yyyy-MM-dd)
}

public class DashboardSummary
{
    public double TotalStockQuantity { get; set; }
    public double TotalStockValue { get; set; }
    public double TotalCustomerReceivable { get; set; } // Müşterilerden Alacak
    public double TotalSupplierPayable { get; set; }    // Tedarikçilere Borç
    public double NetCashToday { get; set; }            // Bugünkü Net Nakit Girişi
    public int CriticalStockCount { get; set; }         // Kritik Stoktaki Ürün Sayısı
    public int OverdueReceivableCount { get; set; }      // Vadesi Geçen Alacak Sayısı
    public double OverdueReceivableTotal { get; set; }   // Vadesi Geçen Toplam Alacak (₺)
    public int ActiveProductCount { get; set; }
    public int ActiveCustomerCount { get; set; }
    public int ActiveSupplierCount { get; set; }
}

public class Invoice
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string InvoiceType { get; set; } = "Alış Faturası"; // Alış Faturası, Satış Faturası
    public long AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public double SubTotal { get; set; }
    public double VatTotal { get; set; }
    public double GrandTotal { get; set; }
    public string? PdfPath { get; set; }
    public byte[]? PdfData { get; set; }
    public string? Note { get; set; }
    public string? CustomizationId { get; set; } // Özelleştirme No (TR1.2)
    public string? Scenario { get; set; } // Senaryo (TICARIFATURA vb.)
    public string? InvoiceKind { get; set; } // Fatura Tipi (SATIS vb.)
    public string? OrderNumber { get; set; } // Sipariş No
    public string? OrderDate { get; set; } // Sipariş Tarihi
    public string? RelatedStore { get; set; } // İlgili Kitabevi
    public string? CargoId { get; set; } // Kargo ID
    public string? ReferenceNo { get; set; } // Referans No
    public string? IssueTime { get; set; } // Düzenleme Saati
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<InvoiceItem> Items { get; set; } = new();
}

public class InvoiceItem
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public long? ProductId { get; set; }
    public int LineNo { get; set; } = 1;
    public string? Barcode { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
    public string Unit { get; set; } = "Adet";
    public double UnitPrice { get; set; }
    public double DiscountPercent { get; set; }
    public double DiscountAmount { get; set; }
    public double VatPercent { get; set; } = 20;
    public double VatAmount { get; set; }
    public double OtherTaxes { get; set; }
    public double LineTotal { get; set; }
    public string? ExpiryDate { get; set; }
    public string? BatchNumber { get; set; }
    public string? ActionDecision { get; set; } // "Alış Fiyatını Güncelle", "Satış Fiyatına Zam Yap", "Olduğu Gibi Ekle", "Faturadan Sil"
    public double? NewSalePrice { get; set; }
}

public class ProductPriceHistory
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ChangeDate { get; set; } = string.Empty;
    public string? DocumentNo { get; set; }
    public string? SupplierName { get; set; }
    public double OldStock { get; set; }
    public double AddedStock { get; set; }
    public double NewStock { get; set; }
    public double OldPurchasePrice { get; set; }
    public double NewPurchasePrice { get; set; }
    public double OldSalePrice { get; set; }
    public double NewSalePrice { get; set; }
    public string? Note { get; set; }
}
