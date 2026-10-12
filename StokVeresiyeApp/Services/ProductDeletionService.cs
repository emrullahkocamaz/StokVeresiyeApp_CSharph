using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

/// <summary>Ürün silinmeden önce kullanıcıya gösterilen etki özeti.</summary>
public class ProductDeletionImpact
{
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public double CurrentStock { get; set; }
    public int StockMovementCount { get; set; }
    public double SoldQty { get; set; }
    public int InvoiceCount { get; set; }
    public int PriceHistoryCount { get; set; }
    public int VariantCount { get; set; }

    public bool HasRecords => StockMovementCount > 0 || InvoiceCount > 0 || PriceHistoryCount > 0;
}

/// <summary>
/// Ürün silme prosedürü (cari silmeyle aynı yaklaşım):
/// • Hareketi olmayan ürün kalıcı silinir.
/// • Hareketi olan ürün ya pasife alınır (arşiv) ya da kalıcı silinir: stok hareketleri, fiyat geçmişi ve varyantları silinir.
///   Faturalardaki kalem satırları ve cari bakiyeler resmi belge olduğu için korunur; yalnızca ürün bağlantısı kaldırılır.
///   Tek transaction'dır: bir adım hata verirse hiçbir şey silinmez.
/// Not: Code sütunu UNIQUE olduğundan kalıcı silinen ürünün kodu yeni bir ürün kartında tekrar kullanılabilir.
/// </summary>
public static class ProductDeletionService
{
    public static ProductDeletionImpact GetImpact(long productId)
    {
        var impact = new ProductDeletionImpact { ProductId = productId };

        var p = Database.Query(@"
SELECT p.Code + ' - ' + p.Name AS Title, COALESCE(v.CurrentStock, p.OpeningStock) AS Stock
FROM Products p LEFT JOIN vw_ProductStock v ON v.ProductId = p.Id
WHERE p.Id = @id;", ("@id", productId));
        if (p.Rows.Count > 0)
        {
            impact.Name = p.Rows[0]["Title"]?.ToString() ?? $"Ürün #{productId}";
            impact.CurrentStock = Convert.ToDouble(p.Rows[0]["Stock"]);
        }

        var sm = Database.Query(@"
SELECT COUNT(*) AS N,
       COALESCE(SUM(CASE WHEN MovementType IN (N'Satılan', N'Satış', N'Giden') THEN Quantity ELSE 0 END), 0) AS Sold
FROM StockMovements WHERE ProductId = @id;", ("@id", productId));
        impact.StockMovementCount = Convert.ToInt32(sm.Rows[0]["N"]);
        impact.SoldQty = Convert.ToDouble(sm.Rows[0]["Sold"]);

        impact.InvoiceCount = Convert.ToInt32(Database.ExecuteScalar("SELECT COUNT(DISTINCT InvoiceId) FROM InvoiceItems WHERE ProductId = @id;", ("@id", productId)) ?? 0);
        impact.PriceHistoryCount = Convert.ToInt32(Database.ExecuteScalar("SELECT COUNT(*) FROM ProductPriceHistory WHERE ProductId = @id;", ("@id", productId)) ?? 0);
        impact.VariantCount = Convert.ToInt32(Database.ExecuteScalar("SELECT COUNT(*) FROM ProductVariants WHERE ProductId = @id;", ("@id", productId)) ?? 0);

        return impact;
    }

    public static void Archive(long productId)
    {
        var impact = GetImpact(productId);
        Database.Execute("UPDATE Products SET IsActive = 0 WHERE Id = @id;", ("@id", productId));
        AuditLogService.Log("Urun", "Arşive Alındı (Pasif)", productId, impact.Name, $"Kalan stok: {impact.CurrentStock:N2}", null, "Ürün kayıtları saklanarak pasife alındı");
    }

    public static void DeletePermanently(long productId)
    {
        var impact = GetImpact(productId);

        using (var conn = Database.Open())
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                void Exec(string sql)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@id", productId);
                    cmd.ExecuteNonQuery();
                }

                // Fatura satırı (ad, kod, tutar) belge olarak kalır; yalnızca silinen karta bağlantısı kaldırılır
                Exec("UPDATE InvoiceItems SET ProductId = NULL WHERE ProductId = @id;");
                Exec("DELETE FROM StockMovements WHERE ProductId = @id;");
                Exec("DELETE FROM ProductPriceHistory WHERE ProductId = @id;");
                Exec("DELETE FROM ProductVariants WHERE ProductId = @id;");
                Exec("DELETE FROM Products WHERE Id = @id;");

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        AuditLogService.Log("Urun", "Kalıcı Silindi", productId, impact.Name,
            $"Kalan stok: {impact.CurrentStock:N2}, Stok hareketi: {impact.StockMovementCount}, Fatura: {impact.InvoiceCount}, Satılan: {impact.SoldQty:N2}",
            null, "Ürün tüm stok kayıtlarıyla kalıcı olarak silindi (fatura satırları korundu)");
    }
}
