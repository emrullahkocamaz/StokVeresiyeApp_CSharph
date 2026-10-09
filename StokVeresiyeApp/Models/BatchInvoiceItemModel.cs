using System;
using System.IO;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Models;

public enum BatchInvoiceStatus
{
    Success,       // 🟢 Tam ve Sorunsuz Okundu
    Warning,       // 🟡 Eksik Alanlar veya Tutar Farkı
    Failed,        // 🔴 Okunamadı (Metin yok / Bozuk dosya / Kalem bulunamadı)
    Duplicate      // ⚠️ Sistemde Daha Önce Kayıtlı
}

public class BatchInvoiceItemModel
{
    public int Index { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted => (FileSizeBytes / 1024.0).ToString("N0") + " KB";

    public BatchInvoiceStatus Status { get; set; } = BatchInvoiceStatus.Success;
    private string? _customBadge;
    public string StatusBadge
    {
        get => _customBadge ?? Status switch
        {
            BatchInvoiceStatus.Success => "🟢 Başarılı",
            BatchInvoiceStatus.Warning => "🟡 Uyarı / Eksik",
            BatchInvoiceStatus.Failed => "🔴 Okunamadı",
            BatchInvoiceStatus.Duplicate => "⚠️ Mükerrer",
            _ => "⚪ Bilinmiyor"
        };
        set => _customBadge = value;
    }

    public string DiagnosticMessage { get; set; } = string.Empty;

    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierTaxNo { get; set; } = string.Empty;
    public long? MatchedAccountId { get; set; }
    public string? MatchedAccountName { get; set; }

    public int ItemCount { get; set; }
    public double SubTotal { get; set; }
    public double VatTotal { get; set; }
    public double GrandTotal { get; set; }
    public double ItemsCalculatedTotal { get; set; }
    public double Difference => Math.Round(Math.Abs(ItemsCalculatedTotal - GrandTotal), 2);

    public ParsedInvoiceResult? ParsedResult { get; set; }
    public List<BatchInvoiceLineItemViewModel> LineItems { get; set; } = new();
    public bool IsSelected { get; set; } = true;
    public bool IsSaved { get; set; }
    public string? SaveError { get; set; }

    // Fatura Meta Bilgileri (Kullanıcının satırlarda görmek istediği E-Fatura / E-Arşiv detayları)
    public string ReferenceNo { get; set; } = string.Empty; // ETTN / Fatura UUID
    public string Scenario { get; set; } = string.Empty;    // TICARIFATURA, TEMELFATURA, EARŞİVFATURA
    public string InvoiceKind { get; set; } = string.Empty; // SATIS, IADE, TEVKIFAT
    public string IssueTime { get; set; } = string.Empty;   // 14:35:00
    public string SupplierTaxOffice { get; set; } = string.Empty;
    public string SupplierAddress { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderDate { get; set; } = string.Empty;
    public string MissingFieldsWarning { get; set; } = string.Empty; // Belirlenen net eksikler listesi
}

public class BatchInvoiceLineItemViewModel
{
    public int LineNo { get; set; } = 1;
    public string? Barcode { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
    public string Unit { get; set; } = "Adet";
    public double UnitPrice { get; set; }
    public double VatPercent { get; set; } = 20;
    public double LineTotal { get; set; }

    // Ürün eşleşme ve aktarım durumu
    public long? MatchedProductId { get; set; }
    public string? MatchedProductCode { get; set; }
    public string? MatchedProductName { get; set; }
    public string MatchStatus { get; set; } = "🆕 Yeni Kart Açılacak";
    public bool IsImported { get; set; } = false;

    public string ImportStatusBadge => IsImported 
        ? "✅ Aktarıldı" 
        : (MatchedProductId.HasValue ? "📦 Eşleşti (Bekliyor)" : "🆕 Yeni Stok (Bekliyor)");

    // Stok Takip Bilgileri (Kullanıcının güvenle stok kontrolü yapması için)
    public double CurrentStock { get; set; } = 0; // Sistemdeki mevcut stok
    public double ProjectedStock => CurrentStock + Quantity; // Fatura girilince oluşacak yeni stok

    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string SourceFileName { get; set; } = string.Empty;
}
