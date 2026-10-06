using System.Data;
using ClosedXML.Excel;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public class ProductService
{
    public static DataTable GetAllProducts(string? search = null, string? category = null, bool onlyCritical = false, long? warehouseId = null)
    {
        string subQuery = warehouseId.HasValue && warehouseId.Value > 0
            ? @"
    SELECT 
        ProductId,
        SUM(CASE 
            WHEN MovementType IN ('Gelen', 'İade Giriş') AND WarehouseId = $wId THEN Quantity 
            WHEN MovementType = 'Transfer Giriş' AND TargetWarehouseId = $wId THEN Quantity
            ELSE 0 END) AS Gelen,
        SUM(CASE 
            WHEN MovementType IN ('Satılan', 'Çıkış', 'Fire', 'Transfer Çıkış') AND WarehouseId = $wId THEN Quantity 
            ELSE 0 END) AS Satilan,
        SUM(CASE 
            WHEN (MovementType IN ('Gelen', 'İade Giriş') AND WarehouseId = $wId) OR (MovementType = 'Transfer Giriş' AND TargetWarehouseId = $wId) THEN Quantity 
            WHEN (MovementType IN ('Satılan', 'Çıkış', 'Fire', 'Transfer Çıkış') AND WarehouseId = $wId) THEN -Quantity 
            ELSE 0 END) AS NetHareket
    FROM StockMovements
    WHERE WarehouseId = $wId OR TargetWarehouseId = $wId
    GROUP BY ProductId"
            : @"
    SELECT 
        ProductId,
        SUM(CASE WHEN MovementType IN ('Gelen', 'İade Giriş') THEN Quantity ELSE 0 END) AS Gelen,
        SUM(CASE WHEN MovementType IN ('Satılan', 'Çıkış', 'Fire') THEN Quantity ELSE 0 END) AS Satilan,
        SUM(CASE WHEN MovementType IN ('Gelen', 'İade Giriş') THEN Quantity ELSE -Quantity END) AS NetHareket
    FROM StockMovements
    GROUP BY ProductId";

        string openingCol = warehouseId.HasValue && warehouseId.Value > 0
            ? "(CASE WHEN (SELECT TOP 1 IsDefault FROM Warehouses WHERE Id = $wId) = 1 THEN p.OpeningStock ELSE 0 END)"
            : "p.OpeningStock";

        var sql = $@"
SELECT 
    p.Id,
    p.Code AS [Ürün Kodu],
    COALESCE(p.Barcode, '') AS [Barkod],
    p.Name AS [Ürün Adı],
    COALESCE(p.Category, 'Genel') AS [Kategori],
    p.Unit AS [Birim],
    {openingCol} AS [Açılış],
    COALESCE(sm.Gelen, 0) AS [Gelen],
    COALESCE(sm.Satilan, 0) AS [Satılan],
    ({openingCol} + COALESCE(sm.NetHareket, 0)) AS [Kalan Stok],
    p.MinStockLevel AS [Kritik Seviye],
    p.PurchasePrice AS [Alış Fiyatı],
    CASE WHEN p.SalePrice > 0 THEN p.SalePrice ELSE (p.PurchasePrice * (1 - p.DiscountPercent/100.0) * (1 + p.VatPercent/100.0)) END AS [Satış Fiyatı],
    COALESCE(p.WholesalePrice, 0) AS [Toptan Fiyat],
    COALESCE(p.SpecialPrice, 0) AS [Özel / Bayi Fiyat],
    p.VatPercent AS [KDV %],
    (({openingCol} + COALESCE(sm.NetHareket, 0)) * 
     CASE WHEN p.SalePrice > 0 THEN p.SalePrice ELSE (p.PurchasePrice * (1 - p.DiscountPercent/100.0) * (1 + p.VatPercent/100.0)) END) AS [Toplam Tutar],
     (SELECT TOP 1 inv.InvoiceNumber 
      FROM InvoiceItems ii 
      INNER JOIN Invoices inv ON ii.InvoiceId = inv.Id 
      WHERE ii.ProductId = p.Id AND (inv.PdfData IS NOT NULL OR (inv.PdfPath IS NOT NULL AND inv.PdfPath != ''))
      ORDER BY inv.InvoiceDate DESC, inv.Id DESC) AS [Fatura No],
     COALESCE(p.ExpiryDate, '') AS [SKT],
     COALESCE(p.BatchNumber, '') AS [Parti / Lot],
     COALESCE(p.Features, '') AS [Özellik / Tür]
 FROM Products p
 LEFT JOIN (
 {subQuery}
 ) sm ON sm.ProductId = p.Id
 WHERE p.IsActive = 1
 ";
         var conditions = new List<string>();
         var parameters = new List<(string, object?)>();

         if (warehouseId.HasValue && warehouseId.Value > 0)
         {
             parameters.Add(("$wId", warehouseId.Value));
         }

         if (!string.IsNullOrWhiteSpace(search))
         {
             conditions.Add("(p.Code LIKE $search OR p.Name LIKE $search OR p.Barcode LIKE $search OR p.Category LIKE $search OR p.Features LIKE $search)");
             parameters.Add(("$search", $"%{search.Trim()}%"));
         }

         if (!string.IsNullOrWhiteSpace(category) && !category.Trim().Equals("Tümü", StringComparison.OrdinalIgnoreCase))
         {
             conditions.Add("p.Category = $cat");
             parameters.Add(("$cat", category.Trim()));
         }

         if (onlyCritical)
         {
             conditions.Add($"({openingCol} + COALESCE(sm.NetHareket, 0)) <= p.MinStockLevel");
         }

         if (conditions.Count > 0)
         {
             sql += " AND " + string.Join(" AND ", conditions);
         }

         sql += " ORDER BY p.Name ASC";

         return Database.Query(sql, parameters.ToArray());
     }

     public static List<string> GetCategories()
     {
         var dt = Database.Query("SELECT DISTINCT Category FROM Products WHERE IsActive=1 AND Category IS NOT NULL AND Category != '' ORDER BY Category");
         var list = new List<string> { "Tümü" };
         foreach (DataRow row in dt.Rows)
         {
             list.Add(row[0].ToString()!);
         }
         return list;
     }

     public static Product? GetById(long id)
     {
         var dt = Database.Query("SELECT * FROM Products WHERE Id=$id", ("$id", id));
         if (dt.Rows.Count == 0) return null;
         var r = dt.Rows[0];
         return new Product
         {
             Id = Convert.ToInt64(r["Id"]),
             Code = r["Code"]?.ToString() ?? "",
             Barcode = r["Barcode"]?.ToString() ?? "",
             Name = r["Name"]?.ToString() ?? "",
             Category = r["Category"]?.ToString() ?? "Genel",
             Unit = r["Unit"]?.ToString() ?? "Adet",
             OpeningStock = Convert.ToDouble(r["OpeningStock"]),
             PurchasePrice = Convert.ToDouble(r["PurchasePrice"]),
             SalePrice = Convert.ToDouble(r["SalePrice"]),
             DiscountPercent = Convert.ToDouble(r["DiscountPercent"]),
             VatPercent = Convert.ToDouble(r["VatPercent"]),
             MinStockLevel = Convert.ToDouble(r["MinStockLevel"]),
             WholesalePrice = r.Table.Columns.Contains("WholesalePrice") && r["WholesalePrice"] != DBNull.Value ? Convert.ToDouble(r["WholesalePrice"]) : 0,
             SpecialPrice = r.Table.Columns.Contains("SpecialPrice") && r["SpecialPrice"] != DBNull.Value ? Convert.ToDouble(r["SpecialPrice"]) : 0,
             ExpiryDate = r.Table.Columns.Contains("ExpiryDate") ? r["ExpiryDate"]?.ToString() : null,
             BatchNumber = r.Table.Columns.Contains("BatchNumber") ? r["BatchNumber"]?.ToString() : null,
             Features = r.Table.Columns.Contains("Features") ? r["Features"]?.ToString() : null
         };
     }

     public static Product? GetByCode(string code)
     {
         if (string.IsNullOrWhiteSpace(code)) return null;
         var dt = Database.Query("SELECT * FROM Products WHERE (Code=$c OR Barcode=$c) AND IsActive=1", ("$c", code.Trim()));
         if (dt.Rows.Count == 0) return null;
         return GetById(Convert.ToInt64(dt.Rows[0]["Id"]));
     }

     public static double GetStock(long productId, long? warehouseId = null)
     {
         try
         {
             var opStock = Database.ExecuteScalar("SELECT OpeningStock FROM Products WHERE Id=@id", ("@id", productId));
             double opening = opStock != null && opStock != DBNull.Value ? Convert.ToDouble(opStock) : 0;

             string sql = @"
SELECT COALESCE(SUM(
     CASE 
         WHEN MovementType IN ('Gelen', 'İade Giriş', 'Transfer Giriş') THEN Quantity
         WHEN MovementType IN ('Satılan', 'Fire / Zayi', 'Çıkış', 'Transfer Çıkış') THEN -Quantity
         ELSE 0 
     END
), 0)
FROM StockMovements
WHERE ProductId = @id";

             if (warehouseId.HasValue && warehouseId.Value > 0)
             {
                 sql += " AND WarehouseId = @wId";
                 var net = Database.ExecuteScalar(sql, ("@id", productId), ("@wId", warehouseId.Value));
                 return opening + (net != null && net != DBNull.Value ? Convert.ToDouble(net) : 0);
             }
             else
             {
                 var net = Database.ExecuteScalar(sql, ("@id", productId));
                 return opening + (net != null && net != DBNull.Value ? Convert.ToDouble(net) : 0);
             }
         }
         catch
         {
             return 0;
         }
     }

     public static void RecordPriceHistory(long productId, string docNo, string supplier, double oldStock, double addedStock, double newStock, double oldBuy, double newBuy, double oldSale, double newSale, string note)
     {
         try
         {
             Database.Execute(@"
INSERT INTO ProductPriceHistory (ProductId, ChangeDate, DocumentNo, SupplierName, OldStock, AddedStock, NewStock, OldPurchasePrice, NewPurchasePrice, OldSalePrice, NewSalePrice, Note)
VALUES ($pid, $dt, $doc, $sup, $oldSt, $addSt, $newSt, $oldB, $newB, $oldS, $newS, $note)",
                 ("$pid", productId),
                 ("$dt", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
                 ("$doc", docNo ?? ""),
                 ("$sup", supplier ?? ""),
                 ("$oldSt", oldStock),
                 ("$addSt", addedStock),
                 ("$newSt", newStock),
                 ("$oldB", oldBuy),
                 ("$newB", newBuy),
                 ("$oldS", oldSale),
                 ("$newS", newSale),
                 ("$note", note ?? ""));
         }
         catch { }
     }

     public static DataTable GetProductPriceHistory(long productId)
     {
         return Database.Query(@"
SELECT 
     Id,
     ChangeDate AS [Tarih],
     COALESCE(DocumentNo, '') AS [Belge / Fatura No],
     COALESCE(SupplierName, '') AS [Tedarikçi],
     OldStock AS [Önceki Stok],
     AddedStock AS [Gelen Adet],
     NewStock AS [Yeni Toplam Stok],
     OldPurchasePrice AS [Önceki Alış (₺)],
     NewPurchasePrice AS [Yeni Alış (₺)],
     OldSalePrice AS [Önceki Satış (₺)],
     NewSalePrice AS [Yeni Satış (₺)],
     COALESCE(Note, '') AS [Açıklama]
FROM ProductPriceHistory
WHERE ProductId = $pid
ORDER BY Id DESC", ("$pid", productId));
     }

     public static (bool Success, string Message, int DeletedCount) DeleteProductsBulk(List<long> productIds)
     {
         if (productIds == null || productIds.Count == 0) return (false, "Silinecek ürün seçilmedi.", 0);
         try
         {
             int count = 0;
             using var conn = Database.Open();
             using var tran = conn.BeginTransaction();
             foreach (var id in productIds)
             {
                 using var cmd1 = conn.CreateCommand();
                 cmd1.Transaction = tran;
                 cmd1.CommandText = "UPDATE Products SET IsActive=0 WHERE Id=@id";
                 cmd1.Parameters.AddWithValue("@id", id);
                 count += cmd1.ExecuteNonQuery();
             }
             tran.Commit();
             return (true, $"{count} adet ürün başarıyla silindi (arşive alındı).", count);
         }
         catch (Exception ex)
         {
             return (false, "Toplu silme sırasında hata: " + ex.Message, 0);
         }
     }

     public static (bool Success, string Message, int DeletedCount) DeleteStockMovementsBulk(List<long> movementIds)
     {
         if (movementIds == null || movementIds.Count == 0) return (false, "Silinecek stok hareketi seçilmedi.", 0);
         try
         {
             int count = 0;
             using var conn = Database.Open();
             using var tran = conn.BeginTransaction();
             foreach (var id in movementIds)
             {
                 using var cmd = conn.CreateCommand();
                 cmd.Transaction = tran;
                 cmd.CommandText = "DELETE FROM StockMovements WHERE Id=@id";
                 cmd.Parameters.AddWithValue("@id", id);
                 count += cmd.ExecuteNonQuery();
             }
             tran.Commit();
             return (true, $"{count} adet stok hareketi kalıcı olarak silindi.", count);
         }
         catch (Exception ex)
         {
             return (false, "Stok hareketleri silinirken hata: " + ex.Message, 0);
         }
     }

     public static DataTable GetProductMovements(long productId)
     {
         return Database.Query(@"
SELECT 
     sm.Id,
     sm.MovementDate AS [Tarih],
     sm.MovementType AS [İşlem Türü],
     sm.Quantity AS [Miktar],
     sm.UnitPrice AS [Birim Fiyat],
     (sm.Quantity * sm.UnitPrice) AS [Toplam Tutar],
     COALESCE(a.Name, '') AS [İlgili Cari],
     COALESCE(sm.DocumentNo, '') AS [Belge No],
     COALESCE(inv.PdfPath, '') AS [PdfPath],
     COALESCE(sm.Note, '') AS [Açıklama]
FROM StockMovements sm
LEFT JOIN Accounts a ON a.Id = sm.AccountId
LEFT JOIN Invoices inv ON inv.InvoiceNumber = sm.DocumentNo
WHERE sm.ProductId = @pid
ORDER BY sm.MovementDate DESC, sm.Id DESC
", ("@pid", productId));
     }
 }

public class AccountService
{
    public static DataTable GetAllAccounts(string? search = null, string? type = null)
    {
        var sql = @"
SELECT 
    a.Id,
    COALESCE(a.IsBlacklisted, 0) AS [Kara Liste],
    a.Name AS [Cari Adı],
    a.Type AS [Cari Türü],
    COALESCE(a.Phone, '') AS [Telefon],
    CASE 
        WHEN a.Type = 'Müşteri' THEN 
            COALESCE((SELECT SUM(CASE WHEN TransactionType='Satış' THEN Amount WHEN TransactionType='Tahsilat' THEN -Amount ELSE 0 END) FROM AccountMovements m WHERE m.AccountId=a.Id), 0)
        ELSE 
            COALESCE((SELECT SUM(CASE WHEN TransactionType='Alış' THEN Amount WHEN TransactionType='Ödeme' THEN -Amount ELSE 0 END) FROM AccountMovements m WHERE m.AccountId=a.Id), 0)
    END AS [Bakiye (₺)],
    COALESCE(a.PriceGroup, 'Perakende') AS [Fiyat Tarifesi],
    COALESCE(a.DefaultDiscountPercent, 0) AS [Sabit İskonto %],
    COALESCE(a.BalanceLimit, 0) AS [Kredi Limiti],
    COALESCE(a.Email, '') AS [E-Posta],
    COALESCE(a.TaxOffice, '') AS [Vergi Dairesi],
    COALESCE(a.TaxNumber, '') AS [Vergi No],
    COALESCE(a.Address, '') AS [Adres],
    COALESCE(a.Description, '') AS [Açıklama]
FROM Accounts a
WHERE a.IsActive = 1
";
        var conditions = new List<string>();
        var parameters = new List<(string, object?)>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(a.Name LIKE $search OR a.Phone LIKE $search OR a.Email LIKE $search OR a.TaxNumber LIKE $search)");
            parameters.Add(("$search", $"%{search.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "Tümü")
        {
            conditions.Add("a.Type = $type");
            parameters.Add(("$type", type));
        }

        if (conditions.Count > 0)
        {
            sql += " AND " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY a.Name ASC";

        return Database.Query(sql, parameters.ToArray());
    }

    public static Account? GetById(long id)
    {
        var dt = Database.Query("SELECT * FROM Accounts WHERE Id=$id", ("$id", id));
        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return new Account
        {
            Id = Convert.ToInt64(r["Id"]),
            Name = r["Name"]?.ToString() ?? "",
            Type = r["Type"]?.ToString() ?? "Müşteri",
            Phone = r["Phone"]?.ToString(),
            Email = r["Email"]?.ToString(),
            Address = r["Address"]?.ToString(),
            TaxOffice = r["TaxOffice"]?.ToString(),
            TaxNumber = r["TaxNumber"]?.ToString(),
            Description = r["Description"]?.ToString(),
            BalanceLimit = Convert.ToDouble(r["BalanceLimit"]),
            PriceGroup = r.Table.Columns.Contains("PriceGroup") && r["PriceGroup"] != DBNull.Value ? r["PriceGroup"].ToString() ?? "Perakende" : "Perakende",
            DefaultDiscountPercent = r.Table.Columns.Contains("DefaultDiscountPercent") && r["DefaultDiscountPercent"] != DBNull.Value ? Convert.ToDouble(r["DefaultDiscountPercent"]) : 0,
            IsBlacklisted = r["IsBlacklisted"] != DBNull.Value && Convert.ToBoolean(r["IsBlacklisted"])
        };
    }

    public static DataTable GetAccountStatement(long accountId)
    {
        return Database.Query(@"
SELECT 
    m.Id,
    m.MovementDate AS [Tarih],
    m.TransactionType AS [İşlem],
    m.DocumentNo AS [Belge No],
    CASE WHEN m.TransactionType IN ('Satış', 'Alış') THEN m.Amount ELSE 0 END AS [Borç],
    CASE WHEN m.TransactionType IN ('Tahsilat', 'Ödeme') THEN m.Amount ELSE 0 END AS [Alacak],
    COALESCE(m.DueDate, '') AS [Vade Tarihi],
    m.Method AS [Ödeme Yöntemi],
    m.CashBank AS [Kasa/Banka],
    COALESCE(m.Note, '') AS [Açıklama]
FROM AccountMovements m
WHERE m.AccountId = $aid
ORDER BY m.MovementDate ASC, m.Id ASC
", ("$aid", accountId));
    }

    public static (bool Success, string Message, int DeletedCount) DeleteAccountsBulk(List<long> accountIds)
    {
        if (accountIds == null || accountIds.Count == 0) return (false, "Silinecek cari seçilmedi.", 0);
        try
        {
            int count = 0;
            using var conn = Database.Open();
            using var tran = conn.BeginTransaction();
            foreach (var id in accountIds)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tran;
                cmd.CommandText = "UPDATE Accounts SET IsActive=0 WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                count += cmd.ExecuteNonQuery();
            }
            tran.Commit();
            return (true, $"{count} adet cari kart arşive alındı / silindi.", count);
        }
        catch (Exception ex)
        {
            return (false, "Cari silinirken hata: " + ex.Message, 0);
        }
    }

    public static (bool Success, string Message, int DeletedCount) DeleteAccountMovementsBulk(List<long> movementIds)
    {
        if (movementIds == null || movementIds.Count == 0) return (false, "Silinecek hareket seçilmedi.", 0);
        try
        {
            int count = 0;
            using var conn = Database.Open();
            using var tran = conn.BeginTransaction();
            foreach (var id in movementIds)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tran;
                cmd.CommandText = "DELETE FROM AccountMovements WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id);
                count += cmd.ExecuteNonQuery();
            }
            tran.Commit();
            return (true, $"{count} adet kasa/finans hareketi kalıcı olarak silindi.", count);
        }
        catch (Exception ex)
        {
            return (false, "Kasa hareketleri silinirken hata: " + ex.Message, 0);
        }
    }
}

public class StockService
{
    public static DataTable GetAllStockMovements(string? search = null, string? type = null, DateTime? start = null, DateTime? end = null, long? warehouseId = null)
    {
        var sql = @"
SELECT 
    sm.Id,
    sm.MovementDate AS [Tarih],
    p.Code AS [Ürün Kodu],
    p.Name AS [Ürün Adı],
    COALESCE(w.Name, 'Merkez Depo') AS [Depo / Şube],
    sm.MovementType AS [Hareket Türü],
    sm.Quantity AS [Miktar],
    p.Unit AS [Birim],
    sm.UnitPrice AS [Birim Fiyat],
    (sm.Quantity * sm.UnitPrice) AS [Toplam Tutar],
    COALESCE(tw.Name, '') AS [Hedef Depo],
    COALESCE(a.Name, '-') AS [İlgili Cari],
    COALESCE(sm.DocumentNo, '-') AS [Belge No],
    COALESCE(sm.Note, '') AS [Not]
FROM StockMovements sm
JOIN Products p ON p.Id = sm.ProductId
LEFT JOIN Accounts a ON a.Id = sm.AccountId
LEFT JOIN Warehouses w ON w.Id = sm.WarehouseId
LEFT JOIN Warehouses tw ON tw.Id = sm.TargetWarehouseId
WHERE 1=1
";
        var conditions = new List<string>();
        var parameters = new List<(string, object?)>();

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            conditions.Add("(sm.WarehouseId = $wId OR sm.TargetWarehouseId = $wId)");
            parameters.Add(("$wId", warehouseId.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(p.Code LIKE $search OR p.Name LIKE $search OR sm.DocumentNo LIKE $search OR a.Name LIKE $search)");
            parameters.Add(("$search", $"%{search.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "Tümü")
        {
            conditions.Add("sm.MovementType = $type");
            parameters.Add(("$type", type));
        }

        if (start.HasValue)
        {
            conditions.Add("sm.MovementDate >= $start");
            parameters.Add(("$start", start.Value.ToString("yyyy-MM-dd")));
        }

        if (end.HasValue)
        {
            conditions.Add("sm.MovementDate <= $end");
            parameters.Add(("$end", end.Value.ToString("yyyy-MM-dd")));
        }

        if (conditions.Count > 0)
        {
            sql += " AND " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY sm.MovementDate DESC, sm.Id DESC";

        return Database.Query(sql, parameters.ToArray());
    }
}

public class TransactionService
{
    public static DataTable GetAllAccountMovements(string? search = null, string? transactionType = null, DateTime? start = null, DateTime? end = null)
    {
        var sql = @"
SELECT 
    m.Id,
    m.MovementDate AS [Tarih],
    a.Name AS [Cari Adı],
    a.Type AS [Cari Türü],
    m.TransactionType AS [İşlem Türü],
    m.DocumentNo AS [Belge/Fatura No],
    m.Amount AS [Tutar (₺)],
    COALESCE(m.DueDate, '') AS [Vade Tarihi],
    m.Method AS [Ödeme Yolu],
    m.CashBank AS [Kasa/Banka],
    COALESCE(m.Note, '') AS [Açıklama]
FROM AccountMovements m
JOIN Accounts a ON a.Id = m.AccountId
WHERE 1=1
";
        var conditions = new List<string>();
        var parameters = new List<(string, object?)>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(a.Name LIKE $search OR m.DocumentNo LIKE $search OR m.Note LIKE $search)");
            parameters.Add(("$search", $"%{search.Trim()}%"));
        }

        if (!string.IsNullOrWhiteSpace(transactionType) && transactionType != "Tümü")
        {
            conditions.Add("m.TransactionType = $tt");
            parameters.Add(("$tt", transactionType));
        }

        if (start.HasValue)
        {
            conditions.Add("m.MovementDate >= $start");
            parameters.Add(("$start", start.Value.ToString("yyyy-MM-dd")));
        }

        if (end.HasValue)
        {
            conditions.Add("m.MovementDate <= $end");
            parameters.Add(("$end", end.Value.ToString("yyyy-MM-dd")));
        }

        if (conditions.Count > 0)
        {
            sql += " AND " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY m.MovementDate DESC, m.Id DESC";

        return Database.Query(sql, parameters.ToArray());
    }

    /// <summary>
    /// Entegre Satış / Alış işlemi: Hem stok hareketi hem cari hareketi tek transaction'da güvenle kaydeder.
    /// </summary>
    public static void ProcessIntegratedSaleOrPurchase(
        DateTime date, 
        long accountId, 
        long productId, 
        string operationType, // "Satış" veya "Alış"
        double quantity, 
        double unitPrice, 
        double totalAmount, 
        string paymentMethod, // "Veresiye (Açık Hesap)", "Nakit", "Kredi Kartı", "Havale/EFT"
        string docNo, 
        string note,
        string? dueDate = null,
        long? warehouseId = null)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            long effectiveWarehouseId = warehouseId.HasValue && warehouseId.Value > 0
                ? warehouseId.Value
                : (WarehouseService.GetDefaultWarehouse()?.Id ?? 1);

            // 1. Stok Hareketi
            string stockMovementType = operationType == "Satış" ? "Satılan" : "Gelen";
            using var cmdStock = conn.CreateCommand();
            cmdStock.Transaction = tx;
            cmdStock.CommandText = @"
INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note)
VALUES(@d, @p, @t, @q, @up, @doc, @acc, @wId, @n);
";
            cmdStock.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
            cmdStock.Parameters.AddWithValue("@p", productId);
            cmdStock.Parameters.AddWithValue("@t", stockMovementType);
            cmdStock.Parameters.AddWithValue("@q", quantity);
            cmdStock.Parameters.AddWithValue("@up", unitPrice);
            cmdStock.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
            cmdStock.Parameters.AddWithValue("@acc", accountId > 0 ? accountId : (object)DBNull.Value);
            cmdStock.Parameters.AddWithValue("@wId", effectiveWarehouseId);
            cmdStock.Parameters.AddWithValue("@n", note ?? (object)DBNull.Value);
            cmdStock.ExecuteNonQuery();

            // 2. Cari Hareketi (Cari seçilmişse)
            if (accountId > 0)
            {
                using var cmdAcc = conn.CreateCommand();
                cmdAcc.Transaction = tx;
                cmdAcc.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note, DueDate)
VALUES(@d, @a, @t, @doc, @amt, @m, @cb, @n, @due);
";
                cmdAcc.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                cmdAcc.Parameters.AddWithValue("@a", accountId);
                cmdAcc.Parameters.AddWithValue("@t", operationType);
                cmdAcc.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                cmdAcc.Parameters.AddWithValue("@amt", totalAmount);
                cmdAcc.Parameters.AddWithValue("@m", paymentMethod);
                cmdAcc.Parameters.AddWithValue("@cb", "Merkez Kasa");
                cmdAcc.Parameters.AddWithValue("@n", note ?? (object)DBNull.Value);
                cmdAcc.Parameters.AddWithValue("@due", !string.IsNullOrWhiteSpace(dueDate) ? dueDate : (object)DBNull.Value);
                cmdAcc.ExecuteNonQuery();

                // Eğer ödeme 'Nakit', 'Kredi Kartı' veya 'Havale/EFT' ile peşin yapıldıysa anında tahsilat/ödeme kaydı da ekleyelim
                if (paymentMethod != "Veresiye (Açık Hesap)")
                {
                    string offsetType = operationType == "Satış" ? "Tahsilat" : "Ödeme";
                    using var cmdPay = conn.CreateCommand();
                    cmdPay.Transaction = tx;
                    cmdPay.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, @a, @t, @doc, @amt, @m, @cb, @n);
";
                    cmdPay.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdPay.Parameters.AddWithValue("@a", accountId);
                    cmdPay.Parameters.AddWithValue("@t", offsetType);
                    cmdPay.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdPay.Parameters.AddWithValue("@amt", totalAmount);
                    cmdPay.Parameters.AddWithValue("@m", paymentMethod);
                    cmdPay.Parameters.AddWithValue("@cb", paymentMethod == "Havale/EFT" ? "Banka Hesabı" : "Merkez Kasa");
                    cmdPay.Parameters.AddWithValue("@n", $"Peşin {offsetType} ({docNo})");
                    cmdPay.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Parçalı / Çoklu Tahsilat ile Entegre Satış / Alış: Nakit, Kart, Havale ve Veresiye tutarlarını tek işlemde böler.
    /// </summary>
    public static void ProcessIntegratedSaleOrPurchaseSplit(
        DateTime date,
        long accountId,
        long productId,
        string operationType,
        double quantity,
        double unitPrice,
        double totalAmount,
        double cashAmount,
        double cardAmount,
        double transferAmount,
        double creditAmount,
        string docNo,
        string note,
        string? dueDate = null,
        long? warehouseId = null)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            long effectiveWarehouseId = warehouseId.HasValue && warehouseId.Value > 0
                ? warehouseId.Value
                : (WarehouseService.GetDefaultWarehouse()?.Id ?? 1);

            // 1. Stok Hareketi
            string stockMovementType = operationType == "Satış" ? "Satılan" : "Gelen";
            using var cmdStock = conn.CreateCommand();
            cmdStock.Transaction = tx;
            cmdStock.CommandText = @"
INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note)
VALUES(@d, @p, @t, @q, @up, @doc, @acc, @wId, @n);";
            cmdStock.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
            cmdStock.Parameters.AddWithValue("@p", productId);
            cmdStock.Parameters.AddWithValue("@t", stockMovementType);
            cmdStock.Parameters.AddWithValue("@q", quantity);
            cmdStock.Parameters.AddWithValue("@up", unitPrice);
            cmdStock.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
            cmdStock.Parameters.AddWithValue("@acc", accountId > 0 ? accountId : (object)DBNull.Value);
            cmdStock.Parameters.AddWithValue("@wId", effectiveWarehouseId);
            cmdStock.Parameters.AddWithValue("@n", note ?? (object)DBNull.Value);
            cmdStock.ExecuteNonQuery();

            string offsetType = operationType == "Satış" ? "Tahsilat" : "Ödeme";

            // 2. Cari Hareketi (Müşteri/Tedarikçi Seçilmişse)
            if (accountId > 0)
            {
                // Ana Satış/Alış Kaydı
                using var cmdAcc = conn.CreateCommand();
                cmdAcc.Transaction = tx;
                cmdAcc.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note, DueDate)
VALUES(@d, @a, @t, @doc, @amt, @m, @cb, @n, @due);";
                cmdAcc.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                cmdAcc.Parameters.AddWithValue("@a", accountId);
                cmdAcc.Parameters.AddWithValue("@t", operationType);
                cmdAcc.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                cmdAcc.Parameters.AddWithValue("@amt", totalAmount);
                cmdAcc.Parameters.AddWithValue("@m", "Parçalı Ödeme");
                cmdAcc.Parameters.AddWithValue("@cb", "Merkez Kasa");
                cmdAcc.Parameters.AddWithValue("@n", note ?? (object)DBNull.Value);
                cmdAcc.Parameters.AddWithValue("@due", !string.IsNullOrWhiteSpace(dueDate) ? dueDate : (object)DBNull.Value);
                cmdAcc.ExecuteNonQuery();

                // Nakit Tahsilat
                if (cashAmount > 0)
                {
                    using var cmdCash = conn.CreateCommand();
                    cmdCash.Transaction = tx;
                    cmdCash.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, @a, @t, @doc, @amt, 'Nakit', 'Merkez Kasa', @n);";
                    cmdCash.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdCash.Parameters.AddWithValue("@a", accountId);
                    cmdCash.Parameters.AddWithValue("@t", offsetType);
                    cmdCash.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdCash.Parameters.AddWithValue("@amt", cashAmount);
                    cmdCash.Parameters.AddWithValue("@n", $"Parçalı {offsetType} (Nakit) - {docNo}");
                    cmdCash.ExecuteNonQuery();
                }

                // Kredi Kartı Tahsilat
                if (cardAmount > 0)
                {
                    using var cmdCard = conn.CreateCommand();
                    cmdCard.Transaction = tx;
                    cmdCard.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, @a, @t, @doc, @amt, 'Kredi Kartı', 'POS / Banka', @n);";
                    cmdCard.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdCard.Parameters.AddWithValue("@a", accountId);
                    cmdCard.Parameters.AddWithValue("@t", offsetType);
                    cmdCard.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdCard.Parameters.AddWithValue("@amt", cardAmount);
                    cmdCard.Parameters.AddWithValue("@n", $"Parçalı {offsetType} (Kredi Kartı) - {docNo}");
                    cmdCard.ExecuteNonQuery();
                }

                // Havale/EFT Tahsilat
                if (transferAmount > 0)
                {
                    using var cmdTr = conn.CreateCommand();
                    cmdTr.Transaction = tx;
                    cmdTr.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, @a, @t, @doc, @amt, 'Havale/EFT', 'Banka Hesabı', @n);";
                    cmdTr.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdTr.Parameters.AddWithValue("@a", accountId);
                    cmdTr.Parameters.AddWithValue("@t", offsetType);
                    cmdTr.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdTr.Parameters.AddWithValue("@amt", transferAmount);
                    cmdTr.Parameters.AddWithValue("@n", $"Parçalı {offsetType} (Havale/EFT) - {docNo}");
                    cmdTr.ExecuteNonQuery();
                }
            }
            else
            {
                // Perakende Müşteri Kasa Girişleri (Kasa Gün Sonu için)
                if (cashAmount > 0)
                {
                    using var cmdCash = conn.CreateCommand();
                    cmdCash.Transaction = tx;
                    cmdCash.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, 0, @t, @doc, @amt, 'Nakit', 'Merkez Kasa', @n);";
                    cmdCash.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdCash.Parameters.AddWithValue("@t", offsetType);
                    cmdCash.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdCash.Parameters.AddWithValue("@amt", cashAmount);
                    cmdCash.Parameters.AddWithValue("@n", $"Perakende {offsetType} (Nakit) - {docNo}");
                    cmdCash.ExecuteNonQuery();
                }

                if (cardAmount > 0)
                {
                    using var cmdCard = conn.CreateCommand();
                    cmdCard.Transaction = tx;
                    cmdCard.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, 0, @t, @doc, @amt, 'Kredi Kartı', 'POS / Banka', @n);";
                    cmdCard.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdCard.Parameters.AddWithValue("@t", offsetType);
                    cmdCard.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdCard.Parameters.AddWithValue("@amt", cardAmount);
                    cmdCard.Parameters.AddWithValue("@n", $"Perakende {offsetType} (Kredi Kartı) - {docNo}");
                    cmdCard.ExecuteNonQuery();
                }

                if (transferAmount > 0)
                {
                    using var cmdTr = conn.CreateCommand();
                    cmdTr.Transaction = tx;
                    cmdTr.CommandText = @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, 0, @t, @doc, @amt, 'Havale/EFT', 'Banka Hesabı', @n);";
                    cmdTr.Parameters.AddWithValue("@d", date.ToString("yyyy-MM-dd"));
                    cmdTr.Parameters.AddWithValue("@t", offsetType);
                    cmdTr.Parameters.AddWithValue("@doc", docNo ?? (object)DBNull.Value);
                    cmdTr.Parameters.AddWithValue("@amt", transferAmount);
                    cmdTr.Parameters.AddWithValue("@n", $"Perakende {offsetType} (Havale/EFT) - {docNo}");
                    cmdTr.ExecuteNonQuery();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Vadesi gelen, vadesi geçen veya tüm vadeli müşteri borçlarını listeler.
    /// </summary>
    public static DataTable GetDueReceivables(string filter = "Tümü")
    {
        var sql = @"
SELECT 
    m.Id,
    m.MovementDate AS [İşlem Tarihi],
    m.DueDate AS [Vade Tarihi],
    DATEDIFF(day, CONVERT(date, m.DueDate), CONVERT(date, GETDATE())) AS [Gecikme (Gün)],
    a.Id AS AccountId,
    a.Name AS [Müşteri],
    COALESCE(a.Phone, '') AS [Telefon],
    m.Amount AS [Veresiye Tutarı (₺)],
    COALESCE((SELECT SUM(CASE WHEN TransactionType='Satış' THEN Amount WHEN TransactionType='Tahsilat' THEN -Amount ELSE 0 END) FROM AccountMovements sm WHERE sm.AccountId=a.Id), 0) AS [Güncel Toplam Bakiye (₺)],
    COALESCE(m.Note, '') AS [Açıklama]
FROM AccountMovements m
JOIN Accounts a ON a.Id = m.AccountId
WHERE a.IsActive = 1 
  AND a.Type = 'Müşteri' 
  AND m.TransactionType = 'Satış' 
  AND m.DueDate IS NOT NULL 
  AND LTRIM(RTRIM(m.DueDate)) <> ''
  AND (SELECT SUM(CASE WHEN TransactionType='Satış' THEN Amount WHEN TransactionType='Tahsilat' THEN -Amount ELSE 0 END) FROM AccountMovements sm WHERE sm.AccountId=a.Id) > 0
";
        if (filter == "Geçmiş")
        {
            sql += " AND CONVERT(date, m.DueDate) < CONVERT(date, GETDATE())";
        }
        else if (filter == "Bugün")
        {
            sql += " AND CONVERT(date, m.DueDate) = CONVERT(date, GETDATE())";
        }
        else if (filter == "Gelecek")
        {
            sql += " AND CONVERT(date, m.DueDate) > CONVERT(date, GETDATE())";
        }

        sql += " ORDER BY m.DueDate ASC, m.Id DESC;";

        return Database.Query(sql);
    }

    /// <summary>
    /// Akıllı Açıklama Hafızası: Veritabanındaki geçmiş işlem açıklamaları ve hazır esnaf şablonlarını döndürür.
    /// </summary>
    public static List<string> GetFrequentDescriptions()
    {
        var result = new List<string>();

        // 1. Hazır Esnaf Şablonları
        var defaults = new[]
        {
            "Nakit Tahsilat",
            "Kredi Kartı ile Tahsilat",
            "Banka Havale / EFT ile Tahsilat",
            "Açık Hesap Veresiye Satış",
            "Haftalık Hesap Kapatma",
            "Aylık Mutabakat Ödemesi",
            "Elden Kısmi Tahsilat",
            "Tedarikçi Mal Alımı Ödemesi",
            "İade Bedeli Mahsubu",
            "Kasa Giriş / Mahsup",
            "Peşin Satış",
            "Veresiye Tahsilatı"
        };
        result.AddRange(defaults);

        // 2. Geçmişte sık kullanılan açıklamalar (Son 50 adet tekil)
        try
        {
            var dt = Database.Query(@"
SELECT DISTINCT TOP 50 Note 
FROM AccountMovements 
WHERE Note IS NOT NULL AND LTRIM(RTRIM(Note)) <> '' 
ORDER BY Note ASC");

            foreach (System.Data.DataRow r in dt.Rows)
            {
                string note = r["Note"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(note) && !result.Contains(note, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(note);
                }
            }
        }
        catch { }

        return result;
    }
}

public class DashboardService
{
    public static DashboardSummary GetSummary()
    {
        var summary = new DashboardSummary();

        // Stok Özeti
        var stockDt = Database.Query(@"
SELECT 
    COALESCE(SUM(p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)), 0) AS TotalStockQty,
    COALESCE(SUM((p.OpeningStock + COALESCE((SELECT SUM(CASE WHEN MovementType IN ('Gelen','İade Giriş') THEN Quantity ELSE -Quantity END) FROM StockMovements sm WHERE sm.ProductId=p.Id), 0)) * CASE WHEN p.SalePrice > 0 THEN p.SalePrice ELSE (p.PurchasePrice * (1 - p.DiscountPercent/100.0) * (1 + p.VatPercent/100.0)) END), 0) AS TotalStockVal,
    COUNT(p.Id) AS ActiveProducts
FROM Products p 
WHERE p.IsActive = 1
");
        if (stockDt.Rows.Count > 0)
        {
            summary.TotalStockQuantity = Convert.ToDouble(stockDt.Rows[0]["TotalStockQty"]);
            summary.TotalStockValue = Convert.ToDouble(stockDt.Rows[0]["TotalStockVal"]);
            summary.ActiveProductCount = Convert.ToInt32(stockDt.Rows[0]["ActiveProducts"]);
        }

        // Müşteri Alacakları (Veresiye)
        var custDt = Database.Query(@"
SELECT 
    COALESCE(SUM(CASE WHEN m.TransactionType='Satış' THEN m.Amount WHEN m.TransactionType='Tahsilat' THEN -m.Amount ELSE 0 END), 0) AS Receivable,
    COUNT(DISTINCT a.Id) AS ActiveCustomers
FROM Accounts a 
LEFT JOIN AccountMovements m ON m.AccountId = a.Id 
WHERE a.IsActive = 1 AND a.Type = 'Müşteri'
");
        if (custDt.Rows.Count > 0)
        {
            summary.TotalCustomerReceivable = Convert.ToDouble(custDt.Rows[0]["Receivable"]);
            summary.ActiveCustomerCount = Convert.ToInt32(custDt.Rows[0]["ActiveCustomers"]);
        }

        // Tedarikçi Borçları
        var suppDt = Database.Query(@"
SELECT 
    COALESCE(SUM(CASE WHEN m.TransactionType='Alış' THEN m.Amount WHEN m.TransactionType='Ödeme' THEN -m.Amount ELSE 0 END), 0) AS Payable,
    COUNT(DISTINCT a.Id) AS ActiveSuppliers
FROM Accounts a 
LEFT JOIN AccountMovements m ON m.AccountId = a.Id 
WHERE a.IsActive = 1 AND a.Type = 'Tedarikçi'
");
        if (suppDt.Rows.Count > 0)
        {
            summary.TotalSupplierPayable = Convert.ToDouble(suppDt.Rows[0]["Payable"]);
            summary.ActiveSupplierCount = Convert.ToInt32(suppDt.Rows[0]["ActiveSuppliers"]);
        }

        // Bugünkü Kasa Hareketi (Tahsilat - Ödeme)
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var cashDt = Database.Query(@"
SELECT 
    COALESCE(SUM(CASE WHEN TransactionType='Tahsilat' THEN Amount WHEN TransactionType='Ödeme' THEN -Amount ELSE 0 END), 0) AS TodayCash
FROM AccountMovements 
WHERE MovementDate >= $today
", ("$today", today));
        if (cashDt.Rows.Count > 0)
        {
            summary.NetCashToday = Convert.ToDouble(cashDt.Rows[0]["TodayCash"]);
        }

        // Kritik Stok Sayısı
        var critDt = Database.Query(@"
SELECT COUNT(*) FROM (
    SELECT p.Id 
    FROM Products p 
    LEFT JOIN StockMovements sm ON sm.ProductId = p.Id 
    WHERE p.IsActive = 1 
    GROUP BY p.Id, p.OpeningStock, p.MinStockLevel
    HAVING (p.OpeningStock + COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen','İade Giriş') THEN sm.Quantity ELSE -sm.Quantity END), 0)) <= p.MinStockLevel
) AS sub;
");
        if (critDt.Rows.Count > 0)
        {
            summary.CriticalStockCount = Convert.ToInt32(critDt.Rows[0][0]);
        }

        // Vadesi Geçen Alacaklar (Günü geçmiş ve halen borcu olan müşteriler)
        var overdueDt = Database.Query(@"
SELECT 
    COUNT(DISTINCT a.Id) AS OverdueCount,
    COALESCE(SUM(m.Amount), 0) AS OverdueTotal
FROM AccountMovements m
JOIN Accounts a ON a.Id = m.AccountId
WHERE a.IsActive = 1 
  AND a.Type = 'Müşteri' 
  AND m.TransactionType = 'Satış' 
  AND m.DueDate IS NOT NULL 
  AND LTRIM(RTRIM(m.DueDate)) <> ''
  AND CONVERT(date, m.DueDate) < CONVERT(date, GETDATE())
  AND (SELECT SUM(CASE WHEN TransactionType='Satış' THEN Amount WHEN TransactionType='Tahsilat' THEN -Amount ELSE 0 END) FROM AccountMovements sm WHERE sm.AccountId=a.Id) > 0;
");
        if (overdueDt.Rows.Count > 0)
        {
            summary.OverdueReceivableCount = Convert.ToInt32(overdueDt.Rows[0]["OverdueCount"]);
            summary.OverdueReceivableTotal = Convert.ToDouble(overdueDt.Rows[0]["OverdueTotal"]);
        }

        return summary;
    }

    public static DataTable GetCriticalStockProducts()
    {
        return Database.Query(@"
SELECT TOP 10
    p.Code AS [Ürün Kodu],
    p.Name AS [Ürün Adı],
    (p.OpeningStock + COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen','İade Giriş') THEN sm.Quantity ELSE -sm.Quantity END), 0)) AS [Kalan Stok],
    p.Unit AS [Birim],
    p.MinStockLevel AS [Kritik Seviye]
FROM Products p
LEFT JOIN StockMovements sm ON sm.ProductId = p.Id
WHERE p.IsActive = 1
GROUP BY p.Id, p.Code, p.Name, p.OpeningStock, p.Unit, p.MinStockLevel
HAVING (p.OpeningStock + COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen','İade Giriş') THEN sm.Quantity ELSE -sm.Quantity END), 0)) <= p.MinStockLevel
ORDER BY [Kalan Stok] ASC;
");
    }

    public static DataTable GetTopDebtorCustomers()
    {
        return Database.Query(@"
SELECT TOP 10
    a.Name AS [Müşteri],
    COALESCE(a.Phone, '-') AS [Telefon],
    COALESCE(SUM(CASE WHEN m.TransactionType='Satış' THEN m.Amount WHEN m.TransactionType='Tahsilat' THEN -m.Amount ELSE 0 END), 0) AS [Borç Bakiyesi (₺)]
FROM Accounts a
LEFT JOIN AccountMovements m ON m.AccountId = a.Id
WHERE a.IsActive = 1 AND a.Type = 'Müşteri'
GROUP BY a.Id, a.Name, a.Phone
HAVING COALESCE(SUM(CASE WHEN m.TransactionType='Satış' THEN m.Amount WHEN m.TransactionType='Tahsilat' THEN -m.Amount ELSE 0 END), 0) > 0
ORDER BY [Borç Bakiyesi (₺)] DESC;
");
    }

    public static DataTable GetRecentTransactions()
    {
        return Database.Query(@"
SELECT TOP 8
    m.MovementDate AS [Tarih],
    a.Name AS [Cari],
    m.TransactionType AS [İşlem],
    m.Amount AS [Tutar (₺)],
    m.Method AS [Yöntem]
FROM AccountMovements m
JOIN Accounts a ON a.Id = m.AccountId
ORDER BY m.MovementDate DESC, m.Id DESC;
");
    }
}

public class WarehouseService
{
    public static DataTable GetAllWarehouses(string? search = null)
    {
        var sql = @"
SELECT 
    w.Id,
    w.Code AS [Depo / Şube Kodu],
    w.Name AS [Depo / Şube / Araç Adı],
    w.Type AS [Türü],
    COALESCE(w.ResponsiblePerson, '') AS [Yetkili / Sorumlu],
    COALESCE(w.Phone, '') AS [İletişim / Tel],
    CASE WHEN w.IsDefault = 1 THEN 'Evet (Varsayılan)' ELSE 'Hayır' END AS [Varsayılan Depo],
    COALESCE(stk.TotalProducts, 0) AS [Kayıtlı Ürün Sayısı],
    COALESCE(stk.TotalStockQty, 0) AS [Mevcut Stok Adedi]
FROM Warehouses w
LEFT JOIN (
    SELECT 
        sm.WarehouseId,
        COUNT(DISTINCT sm.ProductId) AS TotalProducts,
        SUM(CASE WHEN sm.MovementType IN ('Gelen', 'İade Giriş', 'Transfer Giriş') THEN sm.Quantity 
                 WHEN sm.MovementType IN ('Satılan', 'Çıkış', 'Fire', 'Transfer Çıkış') THEN -sm.Quantity 
                 ELSE 0 END) AS TotalStockQty
    FROM StockMovements sm
    GROUP BY sm.WarehouseId
) stk ON stk.WarehouseId = w.Id
WHERE w.IsActive = 1
";
        var pars = new List<(string, object?)>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (w.Name LIKE $s OR w.Code LIKE $s OR w.ResponsiblePerson LIKE $s OR w.Type LIKE $s)";
            pars.Add(("$s", $"%{search.Trim()}%"));
        }
        sql += " ORDER BY w.IsDefault DESC, w.Name ASC";
        return Database.Query(sql, pars.ToArray());
    }

    public static List<Warehouse> GetActiveWarehouses()
    {
        var dt = Database.Query("SELECT * FROM Warehouses WHERE IsActive=1 ORDER BY IsDefault DESC, Name ASC");
        var list = new List<Warehouse>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new Warehouse
            {
                Id = Convert.ToInt64(r["Id"]),
                Name = r["Name"]?.ToString() ?? "",
                Code = r["Code"]?.ToString(),
                Type = r["Type"]?.ToString() ?? "Depo",
                ResponsiblePerson = r["ResponsiblePerson"]?.ToString(),
                Phone = r["Phone"]?.ToString(),
                IsDefault = r["IsDefault"] != DBNull.Value && Convert.ToBoolean(r["IsDefault"]),
                IsActive = r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"])
            });
        }
        return list;
    }

    public static Warehouse? GetById(long id)
    {
        var dt = Database.Query("SELECT * FROM Warehouses WHERE Id=$id", ("$id", id));
        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return new Warehouse
        {
            Id = Convert.ToInt64(r["Id"]),
            Name = r["Name"]?.ToString() ?? "",
            Code = r["Code"]?.ToString(),
            Type = r["Type"]?.ToString() ?? "Depo",
            ResponsiblePerson = r["ResponsiblePerson"]?.ToString(),
            Phone = r["Phone"]?.ToString(),
            IsDefault = r["IsDefault"] != DBNull.Value && Convert.ToBoolean(r["IsDefault"]),
            IsActive = r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"])
        };
    }

    public static Warehouse? GetDefaultWarehouse()
    {
        var dt = Database.Query("SELECT TOP 1 * FROM Warehouses WHERE IsActive=1 AND IsDefault=1");
        if (dt.Rows.Count == 0)
        {
            dt = Database.Query("SELECT TOP 1 * FROM Warehouses WHERE IsActive=1 ORDER BY Id ASC");
        }
        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return new Warehouse
        {
            Id = Convert.ToInt64(r["Id"]),
            Name = r["Name"]?.ToString() ?? "",
            Code = r["Code"]?.ToString(),
            Type = r["Type"]?.ToString() ?? "Depo",
            ResponsiblePerson = r["ResponsiblePerson"]?.ToString(),
            Phone = r["Phone"]?.ToString(),
            IsDefault = r["IsDefault"] != DBNull.Value && Convert.ToBoolean(r["IsDefault"]),
            IsActive = r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"])
        };
    }

    public static void SaveWarehouse(Warehouse w)
    {
        if (w.IsDefault)
        {
            Database.Execute("UPDATE Warehouses SET IsDefault=0 WHERE Id != $id", ("$id", w.Id));
        }

        if (w.Id <= 0)
        {
            Database.Execute(@"
INSERT INTO Warehouses (Name, Code, Type, ResponsiblePerson, Phone, IsDefault, IsActive)
VALUES ($n, $c, $t, $r, $p, $def, 1)",
                ("$n", w.Name.Trim()),
                ("$c", w.Code?.Trim()),
                ("$t", w.Type),
                ("$r", w.ResponsiblePerson?.Trim()),
                ("$p", w.Phone?.Trim()),
                ("$def", w.IsDefault ? 1 : 0));
        }
        else
        {
            Database.Execute(@"
UPDATE Warehouses 
SET Name=$n, Code=$c, Type=$t, ResponsiblePerson=$r, Phone=$p, IsDefault=$def
WHERE Id=$id",
                ("$id", w.Id),
                ("$n", w.Name.Trim()),
                ("$c", w.Code?.Trim()),
                ("$t", w.Type),
                ("$r", w.ResponsiblePerson?.Trim()),
                ("$p", w.Phone?.Trim()),
                ("$def", w.IsDefault ? 1 : 0));
        }
    }

    public static void DeleteWarehouse(long id)
    {
        var count = Convert.ToInt64(Database.ExecuteScalar("SELECT COUNT(*) FROM StockMovements WHERE WarehouseId=$id OR TargetWarehouseId=$id", ("$id", id)) ?? 0);
        if (count > 0)
        {
            Database.Execute("UPDATE Warehouses SET IsActive=0 WHERE Id=$id", ("$id", id));
        }
        else
        {
            Database.Execute("DELETE FROM Warehouses WHERE Id=$id", ("$id", id));
        }
    }

    public static double GetProductStockInWarehouse(long productId, long warehouseId)
    {
        var val = Database.ExecuteScalar(@"
SELECT 
    COALESCE(SUM(CASE 
        WHEN MovementType IN ('Gelen', 'İade Giriş') AND WarehouseId = $w THEN Quantity
        WHEN MovementType = 'Transfer Giriş' AND TargetWarehouseId = $w THEN Quantity
        WHEN MovementType IN ('Satılan', 'Çıkış', 'Fire') AND WarehouseId = $w THEN -Quantity
        WHEN MovementType = 'Transfer Çıkış' AND WarehouseId = $w THEN -Quantity
        ELSE 0 END), 0)
FROM StockMovements
WHERE ProductId = $p", ("$p", productId), ("$w", warehouseId));

        double mov = Convert.ToDouble(val ?? 0);
        var w = GetById(warehouseId);
        if (w != null && w.IsDefault)
        {
            var p = ProductService.GetById(productId);
            return (p?.OpeningStock ?? 0) + mov;
        }
        return mov;
    }

    public static void TransferStock(long sourceWarehouseId, long targetWarehouseId, long productId, double quantity, string? docNo, string? note)
    {
        if (sourceWarehouseId == targetWarehouseId)
            throw new InvalidOperationException("Kaynak depo ile hedef depo aynı olamaz!");

        if (quantity <= 0)
            throw new ArgumentException("Transfer miktarı sıfırdan büyük olmalıdır!");

        var prod = ProductService.GetById(productId) ?? throw new InvalidOperationException("Ürün bulunamadı!");
        var srcW = GetById(sourceWarehouseId) ?? throw new InvalidOperationException("Kaynak depo bulunamadı!");
        var tgtW = GetById(targetWarehouseId) ?? throw new InvalidOperationException("Hedef depo bulunamadı!");

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string autoDoc = string.IsNullOrWhiteSpace(docNo) ? $"TRF-{DateTime.Now:yyyyMMddHHmmss}" : docNo.Trim();
        string fullNote = string.IsNullOrWhiteSpace(note) ? $"Transfer: {srcW.Name} -> {tgtW.Name}" : $"{note} ({srcW.Name} -> {tgtW.Name})";

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Kaynak depodan Transfer Çıkış
            using var cmdOut = conn.CreateCommand();
            cmdOut.Transaction = tx;
            cmdOut.CommandText = @"
INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, WarehouseId, TargetWarehouseId, Note)
VALUES ($d, $p, 'Transfer Çıkış', $q, $up, $doc, $w1, $w2, $n);";
            cmdOut.Parameters.AddWithValue("$d", today);
            cmdOut.Parameters.AddWithValue("$p", productId);
            cmdOut.Parameters.AddWithValue("$q", quantity);
            cmdOut.Parameters.AddWithValue("$up", prod.PurchasePrice);
            cmdOut.Parameters.AddWithValue("$doc", autoDoc);
            cmdOut.Parameters.AddWithValue("$w1", sourceWarehouseId);
            cmdOut.Parameters.AddWithValue("$w2", targetWarehouseId);
            cmdOut.Parameters.AddWithValue("$n", fullNote);
            cmdOut.ExecuteNonQuery();

            // 2. Hedef depoya Transfer Giriş
            using var cmdIn = conn.CreateCommand();
            cmdIn.Transaction = tx;
            cmdIn.CommandText = @"
INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, WarehouseId, TargetWarehouseId, Note)
VALUES ($d, $p, 'Transfer Giriş', $q, $up, $doc, $w2, $w1, $n);";
            cmdIn.Parameters.AddWithValue("$d", today);
            cmdIn.Parameters.AddWithValue("$p", productId);
            cmdIn.Parameters.AddWithValue("$q", quantity);
            cmdIn.Parameters.AddWithValue("$up", prod.PurchasePrice);
            cmdIn.Parameters.AddWithValue("$doc", autoDoc);
            cmdIn.Parameters.AddWithValue("$w2", targetWarehouseId);
            cmdIn.Parameters.AddWithValue("$w1", sourceWarehouseId);
            cmdIn.Parameters.AddWithValue("$n", fullNote);
            cmdIn.ExecuteNonQuery();

            tx.Commit();

            AuditLogService.Log("DepoTransfer", "Transfer", productId, $"{prod.Code} - {prod.Name}", 
                $"{srcW.Name} -> {tgtW.Name}, Miktar: {quantity:N2} {prod.Unit}, Belge No: {autoDoc}", null, fullNote);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
