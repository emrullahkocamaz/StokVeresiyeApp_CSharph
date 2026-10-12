using System.Data;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

/// <summary>Cari silinmeden önce kullanıcıya gösterilen etki özeti.</summary>
public class AccountDeletionImpact
{
    public long AccountId { get; set; }
    public string Name { get; set; } = "";
    public int InvoiceCount { get; set; }
    public double InvoiceTotal { get; set; }
    /// <summary>Bu cariden gelen (alış faturası / alış) stok miktarı: silinince stoktan düşer.</summary>
    public double PurchasedQty { get; set; }
    /// <summary>Bu cariye yapılan satış miktarı: silinince stoğa geri döner.</summary>
    public double SoldQty { get; set; }
    public int StockMovementCount { get; set; }
    public int AccountMovementCount { get; set; }
    public double Balance { get; set; }
    /// <summary>Bu cariden gelen mal zaten satılmışsa, silindiğinde stoğu eksiye düşecek ürün sayısı.</summary>
    public int ProductsGoingNegative { get; set; }

    public bool HasRecords => InvoiceCount > 0 || StockMovementCount > 0 || AccountMovementCount > 0;
}

/// <summary>
/// Cari silme prosedürü (piyasadaki ön muhasebe programları gibi):
/// • Hareketi olmayan cari doğrudan kalıcı silinir.
/// • Hareketi olan cari ya pasife alınır (arşiv, kayıtlar saklanır) ya da TÜM kayıtlarıyla kalıcı silinir:
///   faturaları geri alınır (bu cariden gelen stok girişleri düşer), bu cariye yapılan satış/iade stok hareketleri
///   silinir (ürünler stoğa geri döner), cari hareketleri ve arşivlenmiş fatura PDF'leri silinir, en son cari kartı silinir.
///   Ürün kartları silinmez. Hepsi tek transaction'dadır: bir adım hata verirse hiçbir şey silinmez.
/// </summary>
public static class AccountDeletionService
{
    public static AccountDeletionImpact GetImpact(long accountId)
    {
        var impact = new AccountDeletionImpact { AccountId = accountId };
        impact.Name = Database.ExecuteScalar("SELECT Name FROM Accounts WHERE Id = @id;", ("@id", accountId))?.ToString() ?? $"Cari #{accountId}";

        var inv = Database.Query("SELECT COUNT(*) AS N, COALESCE(SUM(GrandTotal), 0) AS T FROM Invoices WHERE AccountId = @id;", ("@id", accountId));
        impact.InvoiceCount = Convert.ToInt32(inv.Rows[0]["N"]);
        impact.InvoiceTotal = Convert.ToDouble(inv.Rows[0]["T"]);

        var sm = Database.Query(@"
SELECT COUNT(*) AS N,
       COALESCE(SUM(CASE WHEN MovementType IN (N'Gelen') THEN Quantity ELSE 0 END), 0) AS InQty,
       COALESCE(SUM(CASE WHEN MovementType IN (N'Satılan', N'Satış', N'Giden', N'Çıkış', N'Fire', N'Fire / Zayi') THEN Quantity
                         WHEN MovementType IN (N'İade Giriş') THEN -Quantity ELSE 0 END), 0) AS OutQty
FROM StockMovements WHERE AccountId = @id;", ("@id", accountId));
        impact.StockMovementCount = Convert.ToInt32(sm.Rows[0]["N"]);
        impact.PurchasedQty = Convert.ToDouble(sm.Rows[0]["InQty"]);
        impact.SoldQty = Convert.ToDouble(sm.Rows[0]["OutQty"]);

        var am = Database.Query(@"
SELECT COUNT(*) AS N,
       COALESCE(SUM(CASE WHEN TransactionType IN (N'Satış', N'Alış') THEN Amount
                         WHEN TransactionType IN (N'Tahsilat', N'Ödeme') THEN -Amount ELSE 0 END), 0) AS Bal
FROM AccountMovements WHERE AccountId = @id;", ("@id", accountId));
        impact.AccountMovementCount = Convert.ToInt32(am.Rows[0]["N"]);
        impact.Balance = Convert.ToDouble(am.Rows[0]["Bal"]);

        // Silinince: bu carinin giriş hareketleri stoktan düşer, çıkış hareketleri stoğa döner
        impact.ProductsGoingNegative = Convert.ToInt32(Database.ExecuteScalar(@"
SELECT COUNT(*) FROM (
    SELECT v.ProductId
    FROM StockMovements sm
    JOIN vw_ProductStock v ON v.ProductId = sm.ProductId
    WHERE sm.AccountId = @id
    GROUP BY v.ProductId, v.CurrentStock
    HAVING v.CurrentStock
         - SUM(CASE WHEN sm.MovementType IN (N'Gelen', N'İade Giriş') THEN sm.Quantity ELSE 0 END)
         + SUM(CASE WHEN sm.MovementType IN (N'Satılan', N'Satış', N'Giden', N'Çıkış', N'Fire', N'Fire / Zayi') THEN sm.Quantity ELSE 0 END) < -0.0001
) x;", ("@id", accountId)) ?? 0);

        return impact;
    }

    /// <summary>Cariyi kayıtları saklanarak arşive alır (listelerde görünmez, eski raporlarda adı korunur).</summary>
    public static void Archive(long accountId)
    {
        var name = Database.ExecuteScalar("SELECT Name FROM Accounts WHERE Id = @id;", ("@id", accountId))?.ToString();
        Database.Execute("UPDATE Accounts SET IsActive = 0 WHERE Id = @id;", ("@id", accountId));
        AuditLogService.Log("Cari", "Arşive Alındı (Pasif)", accountId, name, null, null, "Cari kayıtları saklanarak pasife alındı");
    }

    /// <summary>
    /// Cariyi tüm faturaları, stok ve cari hareketleriyle birlikte kalıcı olarak siler (tek transaction).
    /// Dönen PDF yolları transaction başarıyla bittikten sonra arşivden silinir.
    /// </summary>
    public static (int Invoices, int StockMovements, int AccountMovements, int PdfFiles) DeletePermanently(long accountId)
    {
        var impact = GetImpact(accountId);
        var pdfPaths = new List<string>();
        int invoices = 0, stockDeleted = 0, accDeleted = 0;

        using (var conn = Database.Open())
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                // 1. Faturalar: InvoiceService'in geri alma prosedürüyle (stok girişleri + cari hareketleri + kalemler + ödemeler)
                var invRows = Query(conn, tx, "SELECT Id, InvoiceNumber, PdfPath FROM Invoices WHERE AccountId = @id;", ("@id", accountId));
                foreach (DataRow r in invRows.Rows)
                {
                    string? pdf = r["PdfPath"] == DBNull.Value ? null : r["PdfPath"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(pdf)) pdfPaths.Add(pdf);

                    Exec(conn, tx, "DELETE FROM ProductPriceHistory WHERE DocumentNo = @doc AND SupplierName = @sup;",
                        ("@doc", r["InvoiceNumber"]?.ToString() ?? ""), ("@sup", impact.Name));

                    var res = InvoiceService.RollbackInvoiceInTransaction(conn, tx, Convert.ToInt64(r["Id"]));
                    if (res.HasValue)
                    {
                        invoices++;
                        stockDeleted += res.Value.StockDeleted;
                        accDeleted += res.Value.AccountDeleted;
                    }
                }

                // 2. Kalan stok hareketleri (satış, iade, elle alış): silinince stok bu cariyle hiç işlem yapılmamış haline döner
                stockDeleted += Exec(conn, tx, "DELETE FROM StockMovements WHERE AccountId = @id;", ("@id", accountId));

                // 3. Kalan cari hareketleri (satış, tahsilat, ödeme, devir)
                Exec(conn, tx, "DELETE FROM InvoicePayments WHERE AccountMovementId IN (SELECT Id FROM AccountMovements WHERE AccountId = @id);", ("@id", accountId));
                accDeleted += Exec(conn, tx, "DELETE FROM AccountMovements WHERE AccountId = @id;", ("@id", accountId));

                // 4. Cari kartı
                Exec(conn, tx, "DELETE FROM Accounts WHERE Id = @id;", ("@id", accountId));

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        int pdfDeleted = pdfPaths.Count(PdfArchiveService.DeleteArchivedPdf);

        AuditLogService.Log("Cari", "Kalıcı Silindi", accountId, impact.Name,
            $"Fatura: {impact.InvoiceCount} ({impact.InvoiceTotal:N2} ₺), Stok hareketi: {impact.StockMovementCount}, Cari hareketi: {impact.AccountMovementCount}, Bakiye: {impact.Balance:N2} ₺",
            null, "Cari tüm kayıtlarıyla kalıcı olarak silindi (öncesinde yedek alındı)");

        return (invoices, stockDeleted, accDeleted, pdfDeleted);
    }

    private static SqlCommand Cmd(SqlConnection conn, SqlTransaction tx, string sql, (string Name, object? Value)[] p)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = Database.NormalizeSql(sql);
        foreach (var x in p) cmd.Parameters.AddWithValue(x.Name, x.Value ?? DBNull.Value);
        return cmd;
    }

    private static int Exec(SqlConnection conn, SqlTransaction tx, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = Cmd(conn, tx, sql, p);
        return cmd.ExecuteNonQuery();
    }

    private static DataTable Query(SqlConnection conn, SqlTransaction tx, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = Cmd(conn, tx, sql, p);
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        t.Load(r);
        return t;
    }
}
