namespace StokVeresiyeApp.Models;

public class ProductVariant
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string VariantName { get; set; } = string.Empty; // Örn: "Mavi / L", "Kırmızı / 42", "Siyah"
    public string? Barcode { get; set; }
    public string? Sku { get; set; }
    public double PriceDifference { get; set; } = 0; // Ana ürün fiyatına eklenecek/düşülecek tutar
    public double StockQuantity { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
