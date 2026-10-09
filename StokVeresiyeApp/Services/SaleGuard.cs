using Microsoft.Data.SqlClient;

namespace StokVeresiyeApp.Services;

public enum SaleBlockKind
{
    InsufficientStock,
    CreditLimit
}

/// <summary>Satış kaydedilmeden önce yakalanan, kullanıcı onayıyla aşılabilen engel.</summary>
public class SaleBlockedException : Exception
{
    public SaleBlockKind Kind { get; }
    public SaleBlockedException(SaleBlockKind kind, string message) : base(message) => Kind = kind;
}

/// <summary>Kullanıcı uyarıyı görüp "yine de devam" dediyse ilgili kontrol atlanır.</summary>
public class SaleGuardOptions
{
    public bool AllowNegativeStock { get; set; }
    public bool AllowOverCreditLimit { get; set; }
}

public static class SaleGuard
{
    private const string Veresiye = "Veresiye (Açık Hesap)";

    /// <summary>
    /// Satış işleminin transaction'ı içinde çalışır: stok yetersizliği ve kredi limiti kontrolü.
    /// Ürün satırı kilitlendiği için aynı ürüne eşzamanlı iki satış aynı stoğu iki kez harcayamaz.
    /// </summary>
    public static void Enforce(SqlConnection conn, SqlTransaction tx, string operationType, long accountId,
        IEnumerable<(long ProductId, double Quantity)> items, double creditAmount, SaleGuardOptions? guard)
    {
        if (operationType != "Satış") return;
        guard ??= new SaleGuardOptions();

        if (!guard.AllowNegativeStock)
        {
            foreach (var g in items.GroupBy(i => i.ProductId))
            {
                double wanted = g.Sum(i => i.Quantity);

                using (var lockCmd = conn.CreateCommand())
                {
                    lockCmd.Transaction = tx;
                    lockCmd.CommandText = "SELECT 1 FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = @p;";
                    lockCmd.Parameters.AddWithValue("@p", g.Key);
                    lockCmd.ExecuteScalar();
                }

                double available;
                string name;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT vs.CurrentStock, p.Name FROM vw_ProductStock vs INNER JOIN Products p ON p.Id = vs.ProductId WHERE vs.ProductId = @p;";
                    cmd.Parameters.AddWithValue("@p", g.Key);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read()) continue;
                    available = Convert.ToDouble(r.GetValue(0));
                    name = r.GetValue(1)?.ToString() ?? "Ürün";
                }

                if (wanted > available + 0.0001)
                {
                    throw new SaleBlockedException(SaleBlockKind.InsufficientStock,
                        $"'{name}' için stok yetersiz.\n\nMevcut stok: {available:N2}\nSatılmak istenen: {wanted:N2}");
                }
            }
        }

        if (!guard.AllowOverCreditLimit && accountId > 0 && creditAmount > 0)
        {
            double limit;
            double balance;
            string accName;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT COALESCE(a.BalanceLimit, 0), a.Name,
       COALESCE((SELECT SUM(CASE WHEN TransactionType = N'Satış' THEN Amount WHEN TransactionType = N'Tahsilat' THEN -Amount ELSE 0 END)
                 FROM AccountMovements m WHERE m.AccountId = a.Id), 0)
FROM Accounts a WHERE a.Id = @a;";
                cmd.Parameters.AddWithValue("@a", accountId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return;
                limit = Convert.ToDouble(r.GetValue(0));
                accName = r.GetValue(1)?.ToString() ?? "Cari";
                balance = Convert.ToDouble(r.GetValue(2));
            }

            // Limit 0 = limitsiz
            if (limit > 0 && balance + creditAmount > limit + 0.005)
            {
                throw new SaleBlockedException(SaleBlockKind.CreditLimit,
                    $"'{accName}' kredi limitini aşıyor.\n\nKredi limiti: {limit:N2} ₺\nMevcut borç: {balance:N2} ₺\nBu satışın veresiye tutarı: {creditAmount:N2} ₺\nYeni borç: {balance + creditAmount:N2} ₺");
            }
        }
    }

    public static double CreditPortion(string paymentMethod, double totalAmount, double splitCredit, bool isSplit)
        => isSplit ? splitCredit : (paymentMethod == Veresiye ? totalAmount : 0);
}
