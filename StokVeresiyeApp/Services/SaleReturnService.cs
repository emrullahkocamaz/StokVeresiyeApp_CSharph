using System.Data;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public class ReturnLine
{
    public long ProductId { get; set; }
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double Total => Math.Round(Quantity * UnitPrice, 2);
}

public class SaleReturnRequest
{
    public string OriginalDocNo { get; set; } = "";
    public long AccountId { get; set; }
    public long WarehouseId { get; set; }
    public List<ReturnLine> Returned { get; } = new();
    public List<ReturnLine> NewItems { get; } = new();

    /// <summary>Fark ödemesi / iade yöntemi: Nakit, Kredi Kartı, Havale/EFT veya "Cari Hesaba" (mahsup / veresiye).</summary>
    public string Method { get; set; } = "Nakit";
    public string Note { get; set; } = "";

    public double ReturnTotal => Math.Round(Returned.Sum(l => l.Total), 2);
    public double NewTotal => Math.Round(NewItems.Sum(l => l.Total), 2);
    /// <summary>Pozitif: müşteri öder. Negatif: müşteriye geri ödenir.</summary>
    public double Net => Math.Round(NewTotal - ReturnTotal, 2);
}

/// <summary>
/// Satış iadesi ve değişimi. Muhasebe mantığı, mevcut bakiye sorgularının (Satış - Tahsilat) bozulmaması için
/// negatif tutarlı hareketlerle yazılır:
///   iade       → Satış (-R)          bakiyeyi düşürür
///   nakit iade → Tahsilat (-tutar)   kasadan para çıkışı olarak görünür
///   yeni ürün  → Satış (+N)
/// </summary>
public static class SaleReturnService
{
    public const string ToCurrentAccount = "Cari Hesaba";

    private static string Tag(string origDoc) => $"(Orijinal: {origDoc})";

    /// <summary>Hızlı satış / satış belgelerini (Satılan stok hareketleri) belge numarasına göre listeler.</summary>
    public static DataTable FindSales(string? search, DateTime from, DateTime to)
    {
        string sql = @"
SELECT sm.DocumentNo AS [Belge No],
       MIN(sm.MovementDate) AS [Tarih],
       COALESCE(MAX(a.Name), N'Perakende Müşteri') AS [Cari],
       COALESCE(MAX(sm.AccountId), 0) AS AccountId,
       SUM(sm.Quantity * sm.UnitPrice) AS [Tutar],
       COUNT(*) AS [Kalem]
FROM StockMovements sm
LEFT JOIN Accounts a ON a.Id = sm.AccountId
WHERE sm.MovementType = N'Satılan' AND sm.DocumentNo IS NOT NULL AND LTRIM(RTRIM(sm.DocumentNo)) <> ''
  AND sm.MovementDate >= @from AND sm.MovementDate <= @to";
        var ps = new List<(string, object?)>
        {
            ("@from", from.ToString("yyyy-MM-dd")),
            ("@to", to.ToString("yyyy-MM-dd"))
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += @" AND (sm.DocumentNo LIKE @s OR a.Name LIKE @s
                       OR EXISTS (SELECT 1 FROM Products p WHERE p.Id = sm.ProductId AND (p.Name LIKE @s OR p.Barcode LIKE @s)))";
            ps.Add(("@s", "%" + search.Trim() + "%"));
        }
        sql += " GROUP BY sm.DocumentNo ORDER BY MIN(sm.MovementDate) DESC, sm.DocumentNo DESC;";
        return Database.Query(sql, ps.ToArray());
    }

    /// <summary>Bir satış belgesindeki ürünleri; satılan, daha önce iade edilen ve iade edilebilir miktarlarıyla getirir.</summary>
    public static DataTable GetSaleLines(string docNo)
    {
        return Database.Query(@"
SELECT sm.ProductId,
       p.Name AS [Ürün],
       p.Unit AS [Birim],
       SUM(sm.Quantity) AS [Satılan],
       CASE WHEN SUM(sm.Quantity) = 0 THEN 0 ELSE SUM(sm.Quantity * sm.UnitPrice) / SUM(sm.Quantity) END AS [Birim Fiyat],
       COALESCE((SELECT SUM(r.Quantity) FROM StockMovements r
                 WHERE r.MovementType = N'İade Giriş' AND r.ProductId = sm.ProductId
                   AND CHARINDEX(@tag, COALESCE(r.Note, N'')) > 0), 0) AS [İade Edilmiş],
       MAX(sm.WarehouseId) AS WarehouseId
FROM StockMovements sm
JOIN Products p ON p.Id = sm.ProductId
WHERE sm.MovementType = N'Satılan' AND sm.DocumentNo = @doc
GROUP BY sm.ProductId, p.Name, p.Unit
ORDER BY p.Name;", ("@doc", docNo), ("@tag", Tag(docNo)));
    }

    /// <summary>İade (ve varsa değişim) işlemini tek transaction içinde kaydeder. Belge numarasını döndürür.</summary>
    public static string Process(SaleReturnRequest req, SaleGuardOptions? guard = null)
    {
        if (req.Returned.Count == 0 || req.Returned.Any(l => l.Quantity <= 0))
            throw new ArgumentException("İade edilecek ürün ve miktar seçilmedi.");
        if (req.Method == ToCurrentAccount && req.AccountId <= 0)
            throw new ArgumentException("Perakende satışta cari hesaba mahsup yapılamaz. Nakit, kart veya havale seçin.");

        string docNo = "IADE-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        string tag = Tag(req.OriginalDocNo);

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // Fazla iade engeli: orijinal satış - daha önce iade edilen
            foreach (var l in req.Returned)
            {
                double sold = ScalarD(conn, tx, "SELECT COALESCE(SUM(Quantity),0) FROM StockMovements WHERE MovementType=N'Satılan' AND DocumentNo=@d AND ProductId=@p;",
                    ("@d", req.OriginalDocNo), ("@p", l.ProductId));
                double done = ScalarD(conn, tx, "SELECT COALESCE(SUM(Quantity),0) FROM StockMovements WHERE MovementType=N'İade Giriş' AND ProductId=@p AND CHARINDEX(@t, COALESCE(Note,N''))>0;",
                    ("@p", l.ProductId), ("@t", tag));
                if (l.Quantity > sold - done + 0.0001)
                    throw new InvalidOperationException($"Bir ürün için iade miktarı satılandan fazla olamaz (iade edilebilir: {sold - done:N2}).");
            }

            string retNote = $"Satış iadesi {tag}" + (string.IsNullOrWhiteSpace(req.Note) ? "" : " - " + req.Note);
            foreach (var l in req.Returned)
                InsertStock(conn, tx, date, l.ProductId, "İade Giriş", l.Quantity, l.UnitPrice, docNo, req.AccountId, req.WarehouseId, retNote);

            // Yeni (değişim) ürünler: iade stoğu girildikten sonra kontrol edilir
            if (req.NewItems.Count > 0)
            {
                double credit = req.Method == ToCurrentAccount && req.Net > 0 ? req.Net : 0;
                SaleGuard.Enforce(conn, tx, "Satış", req.AccountId, req.NewItems.Select(i => (i.ProductId, i.Quantity)), credit, guard);

                string newNote = $"Değişim - yeni ürün {tag}";
                foreach (var l in req.NewItems)
                    InsertStock(conn, tx, date, l.ProductId, "Satılan", l.Quantity, l.UnitPrice, docNo, req.AccountId, req.WarehouseId, newNote);
            }

            if (req.AccountId > 0)
            {
                InsertAccount(conn, tx, date, req.AccountId, "Satış", docNo, -req.ReturnTotal, "Satış İadesi", "İade", retNote);
                if (req.NewTotal > 0)
                    InsertAccount(conn, tx, date, req.AccountId, "Satış", docNo, req.NewTotal, req.Method == ToCurrentAccount ? "Veresiye (Açık Hesap)" : req.Method, "Merkez Kasa", $"Değişim - yeni ürünler {tag}");
            }

            // Kasa / banka hareketi: fark müşteriden alınır (+) veya müşteriye geri ödenir (-)
            if (Math.Abs(req.Net) > 0.004 && req.Method != ToCurrentAccount)
            {
                string cashBank = req.Method == "Havale/EFT" ? "Banka Hesabı" : req.Method == "Kredi Kartı" ? "POS / Banka" : "Merkez Kasa";
                string txt = req.Net > 0 ? $"Değişim fark tahsilatı {tag}" : $"Satış iadesi geri ödemesi {tag}";
                InsertAccount(conn, tx, date, req.AccountId, "Tahsilat", docNo, req.Net, req.Method, cashBank, txt);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        AuditLogService.Log("StokHareket", req.NewItems.Count > 0 ? "Satış Değişimi" : "Satış İadesi", null, docNo, null, null,
            $"Orijinal belge: {req.OriginalDocNo} · İade: {req.ReturnTotal:N2} ₺ · Yeni: {req.NewTotal:N2} ₺ · Fark: {req.Net:N2} ₺ · Yöntem: {req.Method}");
        return docNo;
    }

    private static void InsertStock(SqlConnection c, SqlTransaction tx, string date, long productId, string type, double qty, double price,
        string docNo, long accountId, long warehouseId, string note)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note)
VALUES(@d,@p,@t,@q,@up,@doc,@acc,@w,@n);";
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@p", productId);
        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@q", qty);
        cmd.Parameters.AddWithValue("@up", price);
        cmd.Parameters.AddWithValue("@doc", docNo);
        cmd.Parameters.AddWithValue("@acc", accountId > 0 ? accountId : DBNull.Value);
        cmd.Parameters.AddWithValue("@w", warehouseId);
        cmd.Parameters.AddWithValue("@n", note);
        cmd.ExecuteNonQuery();
    }

    private static void InsertAccount(SqlConnection c, SqlTransaction tx, string date, long accountId, string type, string docNo, double amount,
        string method, string cashBank, string note)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d,@a,@t,@doc,@amt,@m,@cb,@n);";
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@a", accountId);
        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@doc", docNo);
        cmd.Parameters.AddWithValue("@amt", amount);
        cmd.Parameters.AddWithValue("@m", method);
        cmd.Parameters.AddWithValue("@cb", cashBank);
        cmd.Parameters.AddWithValue("@n", note);
        cmd.ExecuteNonQuery();
    }

    private static double ScalarD(SqlConnection c, SqlTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in ps) cmd.Parameters.AddWithValue(p.Item1, p.Item2);
        var o = cmd.ExecuteScalar();
        return o == null || o == DBNull.Value ? 0 : Convert.ToDouble(o);
    }
}
