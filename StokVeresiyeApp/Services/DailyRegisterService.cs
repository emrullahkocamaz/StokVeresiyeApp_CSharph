using System.Data;
using System.Text;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class DailyRegisterService
{
    public static (double OpeningCash, double CashSales, double CardSales, double TransferSales, double CashExpenses, double ExpectedCash) GetDailyStats(DateTime date)
    {
        string dateStr = date.ToString("yyyy-MM-dd");

        // 1. Dünün Kapanış Kasası (Bugünün Açılış Kasası)
        double openingCash = 0;
        try
        {
            var dtLastClosing = Database.Query(@"
SELECT TOP 1 CountedCash 
FROM DailyRegisterClosings 
WHERE ClosingDate < @d 
ORDER BY ClosingDate DESC, Id DESC", Database.Param("@d", dateStr));

            if (dtLastClosing.Rows.Count > 0 && dtLastClosing.Rows[0]["CountedCash"] != DBNull.Value)
            {
                openingCash = Convert.ToDouble(dtLastClosing.Rows[0]["CountedCash"]);
            }
        }
        catch { }

        // 2. Bugünün Kasa / Tahsilat Hareketleri
        double cashSales = 0;
        double cardSales = 0;
        double transferSales = 0;
        double cashExpenses = 0;

        try
        {
            var dtMov = Database.Query(@"
SELECT TransactionType, Method, Amount, CashBank
FROM AccountMovements
WHERE MovementDate = @d", Database.Param("@d", dateStr));

            foreach (DataRow r in dtMov.Rows)
            {
                string type = r["TransactionType"]?.ToString() ?? "";
                string method = r["Method"]?.ToString() ?? "Nakit";
                double amt = Convert.ToDouble(r["Amount"]);

                // Peşin satışta hem Satış hem Tahsilat satırı yazılır; gerçek para girişi yalnızca Tahsilat satırıdır
                if (type == "Tahsilat")
                {
                    if (method.Equals("Kredi Kartı", StringComparison.OrdinalIgnoreCase))
                    {
                        cardSales += amt;
                    }
                    else if (method.Contains("Havale") || method.Contains("EFT"))
                    {
                        transferSales += amt;
                    }
                    else
                    {
                        // Nakit
                        cashSales += amt;
                    }
                }
                else if (type == "Ödeme" || type == "Gider")
                {
                    if (method.Equals("Nakit", StringComparison.OrdinalIgnoreCase))
                    {
                        cashExpenses += amt;
                    }
                }
            }
        }
        catch { }

        // Gider ekranından girilen nakit giderler (Expenses tablosu) kasadan çıkar
        try
        {
            var nakitGider = Database.ExecuteScalar("SELECT COALESCE(SUM(Amount), 0) FROM Expenses WHERE ExpenseDate = @d AND PaymentMethod = N'Nakit'", Database.Param("@d", dateStr));
            if (nakitGider != null && nakitGider != DBNull.Value) cashExpenses += Convert.ToDouble(nakitGider);
        }
        catch { }

        double expectedCash = openingCash + cashSales - cashExpenses;
        return (openingCash, cashSales, cardSales, transferSales, cashExpenses, expectedCash);
    }

    public static long SaveClosing(DailyRegisterClosing closing)
    {
        return Database.ExecuteScalar<long>(@"
INSERT INTO DailyRegisterClosings 
(ClosingDate, ClosedBy, OpeningCash, CashSales, CardSales, TransferSales, CashExpenses, ExpectedCash, CountedCash, DifferenceCash, Notes, CreatedAt)
VALUES (@d, @u, @op, @cs, @crd, @tr, @exp, @exCash, @cnt, @diff, @n, @cr);
SELECT SCOPE_IDENTITY();",
            Database.Param("@d", closing.ClosingDate),
            Database.Param("@u", closing.ClosedBy),
            Database.Param("@op", closing.OpeningCash),
            Database.Param("@cs", closing.CashSales),
            Database.Param("@crd", closing.CardSales),
            Database.Param("@tr", closing.TransferSales),
            Database.Param("@exp", closing.CashExpenses),
            Database.Param("@exCash", closing.ExpectedCash),
            Database.Param("@cnt", closing.CountedCash),
            Database.Param("@diff", closing.DifferenceCash),
            Database.Param("@n", closing.Notes ?? (object)DBNull.Value),
            Database.Param("@cr", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
    }

    public static DataTable GetClosingsHistory()
    {
        return Database.Query(@"
SELECT 
    Id,
    ClosingDate AS [Kapanış Tarihi],
    ClosedBy AS [Kapatan Kullanıcı],
    OpeningCash AS [Devreden / Açılış (₺)],
    CashSales AS [Nakit Giriş (₺)],
    CardSales AS [Kredi Kartı (₺)],
    TransferSales AS [Havale / EFT (₺)],
    CashExpenses AS [Nakit Çıkış / Masraf (₺)],
    ExpectedCash AS [Sistem Kasası (₺)],
    CountedCash AS [Sayılan Kasa (₺)],
    DifferenceCash AS [Fark (₺)],
    CreatedAt AS [Kayıt Zamanı],
    Notes AS [Notlar]
FROM DailyRegisterClosings
ORDER BY ClosingDate DESC, Id DESC");
    }

    public static string GenerateZReportText(DailyRegisterClosing c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine("            BİLENSİS YAZILIM            ");
        sb.AppendLine("         GÜNLÜK KASA Z RAPORU           ");
        sb.AppendLine("========================================");
        sb.AppendLine($"Rapor Tarihi  : {c.ClosingDate}");
        sb.AppendLine($"Kapatan Yetkili: {c.ClosedBy}");
        sb.AppendLine($"Düzenleme Anı : {c.CreatedAt}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Sabah Devir (Açılış)   : {c.OpeningCash,12:N2} ₺");
        sb.AppendLine($"Günlük Nakit Girişi    : {c.CashSales,12:N2} ₺");
        sb.AppendLine($"Günlük Kart Satışı     : {c.CardSales,12:N2} ₺");
        sb.AppendLine($"Günlük Havale/EFT      : {c.TransferSales,12:N2} ₺");
        sb.AppendLine($"Günlük Kasa Çıkış/Gider: {c.CashExpenses,12:N2} ₺");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"TOPLAM CİRO (Tüm Ödem.): {(c.CashSales + c.CardSales + c.TransferSales),12:N2} ₺");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Kasada Beklenen Nakit  : {c.ExpectedCash,12:N2} ₺");
        sb.AppendLine($"Fiziki Sayılan Nakit   : {c.CountedCash,12:N2} ₺");
        string diffLabel = c.DifferenceCash >= 0 ? "KASA FAZLASI (+)" : "KASA EKSİĞİ (-)";
        sb.AppendLine($"{diffLabel,-23}: {c.DifferenceCash,12:N2} ₺");
        sb.AppendLine("========================================");
        if (!string.IsNullOrWhiteSpace(c.Notes))
        {
            sb.AppendLine($"Notlar: {c.Notes}");
            sb.AppendLine("----------------------------------------");
        }
        sb.AppendLine("Kasa mutabakatı yapılmış ve onaylanmıştır.");
        sb.AppendLine("İmza: _______________________");
        return sb.ToString();
    }
}
