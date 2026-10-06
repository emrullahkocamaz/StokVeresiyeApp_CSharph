using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using System.Data;

namespace StokVeresiyeApp.Services;

public static class ExcelService
{
    public static (int products, int stock, int accounts, int accountMovements) ImportFromExcel(string path, bool clearExisting)
    {
        using var wb = new XLWorkbook(path);
        using var c = Database.Open();
        using var tx = c.BeginTransaction();

        if (clearExisting)
        {
            using var clear = c.CreateCommand();
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM AccountMovements; DELETE FROM StockMovements; DELETE FROM Accounts; DELETE FROM Products;";
            clear.ExecuteNonQuery();
        }

        int products = 0, stock = 0, accounts = 0, accountMovements = 0;
        var productIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var accountIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // 1. Ürünler Sayfası
        if (wb.TryGetWorksheet("Ürünler", out var wsProducts))
        {
            foreach (var row in wsProducts.RowsUsed().Where(r => r.RowNumber() >= 5))
            {
                var code = row.Cell(1).GetString().Trim();
                var name = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) continue;

                var unit = row.Cell(3).GetString().Trim();
                if (string.IsNullOrEmpty(unit)) unit = "Adet";

                double opening = Num(row.Cell(4));
                double price = Num(row.Cell(5));
                double discount = Num(row.Cell(10));
                double vat = Num(row.Cell(11));

                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM Products WHERE Code = @code)
BEGIN
    UPDATE Products SET 
        Name = @name, 
        Unit = @unit, 
        OpeningStock = @opening, 
        PurchasePrice = @price, 
        DiscountPercent = @discount, 
        VatPercent = @vat 
    WHERE Code = @code;
    SELECT Id FROM Products WHERE Code = @code;
END
ELSE
BEGIN
    INSERT INTO Products(Code, Name, Unit, OpeningStock, PurchasePrice, DiscountPercent, VatPercent) 
    VALUES(@code, @name, @unit, @opening, @price, @discount, @vat);
    SELECT SCOPE_IDENTITY();
END";

                cmd.Parameters.AddWithValue("@code", code);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@unit", unit);
                cmd.Parameters.AddWithValue("@opening", opening);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@discount", discount);
                cmd.Parameters.AddWithValue("@vat", vat);

                var scalarVal = cmd.ExecuteScalar();
                if (scalarVal != null && scalarVal != DBNull.Value)
                {
                    long id = Convert.ToInt64(scalarVal);
                    productIds[code] = id;
                    products++;
                }
            }
        }

        // 2. Stok Hareketi Sayfası
        if (wb.TryGetWorksheet("Stok Hareketi", out var wsStock))
        {
            foreach (var row in wsStock.RowsUsed().Where(r => r.RowNumber() >= 5))
            {
                var code = row.Cell(2).GetString().Trim();
                var type = row.Cell(3).GetString().Trim();
                var qty = Num(row.Cell(4));
                if (!productIds.TryGetValue(code, out var pid) || qty == 0 || string.IsNullOrWhiteSpace(type)) continue;

                Insert(c, tx, "INSERT INTO StockMovements(MovementDate, ProductId, MovementType, Quantity, Note) VALUES(@d, @p, @t, @q, @n)",
                    ("@d", Date(row.Cell(1))),
                    ("@p", pid),
                    ("@t", type),
                    ("@q", qty),
                    ("@n", row.Cell(5).GetString().Trim()));
                stock++;
            }
        }

        // 3. Cari Listesi Sayfası
        if (wb.TryGetWorksheet("Cari Listesi", out var wsAccounts))
        {
            foreach (var row in wsAccounts.RowsUsed().Where(r => r.RowNumber() >= 5))
            {
                var name = row.Cell(1).GetString().Trim();
                var type = row.Cell(2).GetString().Trim();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type)) continue;

                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM Accounts WHERE Name = @n)
BEGIN
    UPDATE Accounts SET 
        Type = @t, 
        Description = @d 
    WHERE Name = @n;
    SELECT Id FROM Accounts WHERE Name = @n;
END
ELSE
BEGIN
    INSERT INTO Accounts(Name, Type, Description) 
    VALUES(@n, @t, @d);
    SELECT SCOPE_IDENTITY();
END";

                cmd.Parameters.AddWithValue("@n", name);
                cmd.Parameters.AddWithValue("@t", type);
                cmd.Parameters.AddWithValue("@d", row.Cell(4).GetString().Trim());
                var scalarVal = cmd.ExecuteScalar();
                if (scalarVal != null && scalarVal != DBNull.Value)
                {
                    accountIds[name] = Convert.ToInt64(scalarVal);
                    accounts++;
                }
            }
        }

        // 4. Cari Hareket Sayfası
        if (wb.TryGetWorksheet("Cari Hareket", out var wsAccMov))
        {
            foreach (var row in wsAccMov.RowsUsed().Where(r => r.RowNumber() >= 5))
            {
                var name = row.Cell(2).GetString().Trim();
                var type = row.Cell(3).GetString().Trim();
                var amount = Num(row.Cell(5));
                if (!accountIds.TryGetValue(name, out var aid) || amount == 0 || string.IsNullOrWhiteSpace(type)) continue;

                Insert(c, tx, @"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note) 
VALUES(@d, @a, @t, @doc, @amt, @m, @cb, @n)",
                    ("@d", Date(row.Cell(1))),
                    ("@a", aid),
                    ("@t", type),
                    ("@doc", row.Cell(4).GetString().Trim()),
                    ("@amt", amount),
                    ("@m", row.Cell(6).GetString().Trim()),
                    ("@cb", row.Cell(7).GetString().Trim()),
                    ("@n", row.Cell(8).GetString().Trim()));
                accountMovements++;
            }
        }

        tx.Commit();
        return (products, stock, accounts, accountMovements);
    }

    public static void ExportDataTableToExcel(DataTable table, string sheetTitle, string filePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetTitle);

        // Başlık
        ws.Cell(1, 1).Value = sheetTitle.ToUpper();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, table.Columns.Count).Merge().Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
        ws.Range(1, 1, 1, table.Columns.Count).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Height = 28;

        // Kolon Başlıkları
        for (int c = 0; c < table.Columns.Count; c++)
        {
            var cell = ws.Cell(2, c + 1);
            cell.Value = table.Columns[c].ColumnName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#14B8C4");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(2).Height = 22;

        // Satırlar
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var dataRow = table.Rows[r];
            int rowIdx = r + 3;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var val = dataRow[c];
                var cell = ws.Cell(rowIdx, c + 1);

                if (val is double or decimal or float or int or long)
                {
                    cell.Value = Convert.ToDouble(val);
                    string colName = table.Columns[c].ColumnName.ToLower();
                    if (colName.Contains("fiyat") || colName.Contains("tutar") || colName.Contains("bakiye") || colName.Contains("borç") || colName.Contains("alacak"))
                    {
                        cell.Style.NumberFormat.Format = "#,##0.00 ₺";
                    }
                    else if (colName.Contains("miktar") || colName.Contains("stok"))
                    {
                        cell.Style.NumberFormat.Format = "#,##0.##";
                    }
                }
                else
                {
                    cell.Value = val?.ToString() ?? "";
                }

                if (r % 2 == 1)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                }
            }
        }

        // Tablo sınırları ve otomatik genişlik
        var dataRange = ws.Range(2, 1, table.Rows.Count + 2, table.Columns.Count);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#CBD5E1");
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94A3B8");

        ws.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }

    private static void Insert(SqlConnection c, SqlTransaction tx, string sql, params (string, object?)[] ps)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = Database.NormalizeSql(sql);
        foreach (var p in ps)
        {
            string pName = p.Item1.StartsWith("@") ? p.Item1 : (p.Item1.StartsWith("$") ? "@" + p.Item1[1..] : "@" + p.Item1);
            cmd.Parameters.AddWithValue(pName, p.Item2 ?? DBNull.Value);
        }
        cmd.ExecuteNonQuery();
    }

    private static double Num(IXLCell c) => c.TryGetValue<double>(out var x) ? x : 0;
    private static string Date(IXLCell c) => c.TryGetValue<DateTime>(out var d) ? d.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");
}
