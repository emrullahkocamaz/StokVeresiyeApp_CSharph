namespace StokVeresiyeApp.Models;

public class ParkedSaleModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime ParkedAt { get; set; } = DateTime.Now;
    public string Operation { get; set; } = "Satış";
    public long AccountId { get; set; }
    public string AccountName { get; set; } = "(Perakende)";
    public long WarehouseId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
    public double UnitPrice { get; set; } = 0;
    public double DiscountPercent { get; set; } = 0;
    public double VatPercent { get; set; } = 20;
    public double TotalAmount { get; set; } = 0;
    public string PaymentMethod { get; set; } = "Nakit";
    public string DocNo { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public long? VariantId { get; set; }
    public string? VariantName { get; set; }
    public string? CustomTag { get; set; } // Plaka / Masa No / Müşteri Adı (Örn: "Masa 4", "34 ABC 123", "Ahmet Bey")

    public string DisplayText =>
        $"[{ParkedAt:HH:mm}] {(!string.IsNullOrWhiteSpace(CustomTag) ? $"🏷️ {CustomTag} | " : "")}{AccountName} - {ProductName} ({Quantity:N0} adet) - {TotalAmount:N2} ₺";
}
