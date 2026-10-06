using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class ProductVariantService
{
    public static DataTable GetVariantsTable(long productId)
    {
        return Database.Query(@"
SELECT 
    v.Id,
    v.VariantName AS [Varyant (Renk/Beden)],
    v.Barcode AS [Barkod],
    v.Sku AS [Stok Kodu / SKU],
    v.PriceDifference AS [Fiyat Farkı (₺)],
    v.StockQuantity AS [Stok Miktarı]
FROM ProductVariants v
WHERE v.ProductId = @pId AND v.IsActive = 1
ORDER BY v.VariantName", 
            Database.Param("@pId", productId));
    }

    public static List<ProductVariant> GetVariantsByProductId(long productId)
    {
        var dt = Database.Query(@"
SELECT Id, ProductId, VariantName, Barcode, Sku, PriceDifference, StockQuantity, IsActive
FROM ProductVariants
WHERE ProductId = @pId AND IsActive = 1
ORDER BY VariantName", Database.Param("@pId", productId));

        var list = new List<ProductVariant>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new ProductVariant
            {
                Id = Convert.ToInt64(r["Id"]),
                ProductId = Convert.ToInt64(r["ProductId"]),
                VariantName = r["VariantName"]?.ToString() ?? "",
                Barcode = r["Barcode"]?.ToString(),
                Sku = r["Sku"]?.ToString(),
                PriceDifference = Convert.ToDouble(r["PriceDifference"]),
                StockQuantity = Convert.ToDouble(r["StockQuantity"]),
                IsActive = Convert.ToBoolean(r["IsActive"])
            });
        }
        return list;
    }

    public static long SaveVariant(ProductVariant variant)
    {
        if (variant.Id > 0)
        {
            Database.Execute(@"
UPDATE ProductVariants 
SET VariantName = @name, Barcode = @bar, Sku = @sku, PriceDifference = @pdiff, StockQuantity = @stock 
WHERE Id = @id",
                Database.Param("@name", variant.VariantName.Trim()),
                Database.Param("@bar", string.IsNullOrWhiteSpace(variant.Barcode) ? (object)DBNull.Value : variant.Barcode.Trim()),
                Database.Param("@sku", string.IsNullOrWhiteSpace(variant.Sku) ? (object)DBNull.Value : variant.Sku.Trim()),
                Database.Param("@pdiff", variant.PriceDifference),
                Database.Param("@stock", variant.StockQuantity),
                Database.Param("@id", variant.Id));
            return variant.Id;
        }
        else
        {
            return Database.ExecuteScalar<long>(@"
INSERT INTO ProductVariants (ProductId, VariantName, Barcode, Sku, PriceDifference, StockQuantity, IsActive)
VALUES (@pId, @name, @bar, @sku, @pdiff, @stock, 1);
SELECT SCOPE_IDENTITY();",
                Database.Param("@pId", variant.ProductId),
                Database.Param("@name", variant.VariantName.Trim()),
                Database.Param("@bar", string.IsNullOrWhiteSpace(variant.Barcode) ? (object)DBNull.Value : variant.Barcode.Trim()),
                Database.Param("@sku", string.IsNullOrWhiteSpace(variant.Sku) ? (object)DBNull.Value : variant.Sku.Trim()),
                Database.Param("@pdiff", variant.PriceDifference),
                Database.Param("@stock", variant.StockQuantity));
        }
    }

    public static void DeleteVariant(long variantId)
    {
        Database.Execute("UPDATE ProductVariants SET IsActive = 0 WHERE Id = @id", Database.Param("@id", variantId));
    }

    public static (long ProductId, long VariantId, string VariantName, double PriceDiff)? FindByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var dt = Database.Query(@"
SELECT TOP 1 v.Id, v.ProductId, v.VariantName, v.PriceDifference
FROM ProductVariants v
WHERE v.Barcode = @bar AND v.IsActive = 1", Database.Param("@bar", barcode.Trim()));

        if (dt.Rows.Count > 0)
        {
            var r = dt.Rows[0];
            return (
                Convert.ToInt64(r["ProductId"]),
                Convert.ToInt64(r["Id"]),
                r["VariantName"]?.ToString() ?? "",
                Convert.ToDouble(r["PriceDifference"])
            );
        }
        return null;
    }
}
