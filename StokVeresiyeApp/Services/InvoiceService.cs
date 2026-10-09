using System.Data;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class InvoiceService
{
    /// <summary>
    /// Faturayı, kalemlerini, stok ve cari hareketlerini kaydeder.
    /// </summary>
    public static long SaveInvoice(Invoice invoice, bool updateStock = true, bool updateAccountBalance = true, long? replaceInvoiceId = null)
    {
        // PDF'in güvenli kopyası ÖNCE programın arşiv klasörüne alınır ve doğrulanır (veritabanına PDF gömülmez).
        // Kaynak dosya (e-posta eki, USB, indirilenler) sonradan silinse bile fatura belgesi kaybolmaz.
        // Kopyalama başarısız olursa fatura hiç kaydedilmez.
        string? archivedPdfPath = null;
        string? pdfSha256 = null;
        bool archiveCreatedNew = false;

        if (!string.IsNullOrWhiteSpace(invoice.PdfPath) && File.Exists(invoice.PdfPath))
        {
            var stored = PdfArchiveService.Store(invoice.PdfPath, invoice.InvoiceNumber, invoice.InvoiceDate);
            archivedPdfPath = stored.Path;
            pdfSha256 = stored.Sha256;
            archiveCreatedNew = stored.CreatedNew;
        }
        else if (invoice.PdfData != null && invoice.PdfData.Length > 0)
        {
            var stored = PdfArchiveService.StoreBytes(invoice.PdfData, ".pdf", invoice.InvoiceNumber, invoice.InvoiceDate);
            archivedPdfPath = stored.Path;
            pdfSha256 = stored.Sha256;
            archiveCreatedNew = stored.CreatedNew;
        }

        invoice.PdfData = null;
        if (archivedPdfPath != null) invoice.PdfPath = archivedPdfPath;

        // Fatura başlığı, kalemler, ürün kartları, stok ve cari hareketleri TEK transaction içinde yazılır.
        // Herhangi bir adım hata verirse hiçbir kayıt kalmaz (yarım fatura oluşmaz).
        long invoiceId;
        using (var conn = Database.Open())
        using (var tx = conn.BeginTransaction())
        {
            try
            {
                // Mevcut faturanın üzerine yazma: eski fatura ve hareketleri aynı transaction içinde geri alınır
                if (replaceInvoiceId.HasValue && replaceInvoiceId.Value > 0)
                {
                    RollbackInvoiceInTransaction(conn, tx, replaceInvoiceId.Value);
                }

                // Aynı tedarikçiden aynı numaralı fatura ikinci kez işlenemez (stok iki kez girmesin)
                var dup = TxScalar(conn, tx,
                    "SELECT TOP 1 Id FROM Invoices WHERE InvoiceNumber = @num AND COALESCE(AccountId, 0) = @acc;",
                    ("@num", invoice.InvoiceNumber), ("@acc", invoice.AccountId));
                if (dup != null && dup != DBNull.Value)
                    throw new InvalidOperationException($"'{invoice.InvoiceNumber}' numaralı fatura bu cari için zaten kayıtlı. Aynı fatura ikinci kez işlenmedi.");

                var invoiceIdObj = TxScalar(conn, tx, @"
INSERT INTO Invoices (InvoiceNumber, InvoiceDate, InvoiceType, AccountId, WarehouseId, SubTotal, VatTotal, GrandTotal, PdfPath, PdfData, PdfSha256, Note, CustomizationId, Scenario, InvoiceKind, OrderNumber, OrderDate, RelatedStore, CargoId, ReferenceNo, IssueTime, CreatedAt)
VALUES (@num, @date, @type, @accId, @wId, @sub, @vat, @grand, @pdf, @pdfData, @sha, @note, @cust, @scen, @kind, @ordNo, @ordDate, @store, @cargo, @ref, @time, @created);
SELECT SCOPE_IDENTITY();",
                    ("@num", invoice.InvoiceNumber),
                    ("@date", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                    ("@type", invoice.InvoiceType),
                    ("@accId", invoice.AccountId),
                    ("@wId", invoice.WarehouseId),
                    ("@sub", invoice.SubTotal),
                    ("@vat", invoice.VatTotal),
                    ("@grand", invoice.GrandTotal),
                    ("@pdf", (object?)archivedPdfPath ?? (object?)invoice.PdfPath ?? DBNull.Value),
                    ("@pdfData", DBNull.Value),
                    ("@sha", (object?)pdfSha256 ?? DBNull.Value),
                    ("@note", (object?)invoice.Note ?? DBNull.Value),
                    ("@cust", (object?)invoice.CustomizationId ?? DBNull.Value),
                    ("@scen", (object?)invoice.Scenario ?? DBNull.Value),
                    ("@kind", (object?)invoice.InvoiceKind ?? DBNull.Value),
                    ("@ordNo", (object?)invoice.OrderNumber ?? DBNull.Value),
                    ("@ordDate", (object?)invoice.OrderDate ?? DBNull.Value),
                    ("@store", (object?)invoice.RelatedStore ?? DBNull.Value),
                    ("@cargo", (object?)invoice.CargoId ?? DBNull.Value),
                    ("@ref", (object?)invoice.ReferenceNo ?? DBNull.Value),
                    ("@time", (object?)invoice.IssueTime ?? DBNull.Value),
                    ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

                invoiceId = Convert.ToInt64(invoiceIdObj);
                invoice.Id = invoiceId;

                string supplierName = "";
                if (invoice.AccountId > 0)
                {
                    var an = TxScalar(conn, tx, "SELECT Name FROM Accounts WHERE Id = @id;", ("@id", invoice.AccountId));
                    supplierName = an?.ToString() ?? "";
                }

                foreach (var item in invoice.Items)
                {
                    // Kullanıcı bu kalemi faturadan silmeyi / hariç tutmayı seçtiyse kaydetme
                    if (string.Equals(item.ActionDecision, "Faturadan Sil", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.ActionDecision, "Sil", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    item.InvoiceId = invoiceId;

                    // Sistemde kayıtlı ürün yoksa önce eşleştir, yoksa otomatik ürün kartı oluştur
                    if (!item.ProductId.HasValue || item.ProductId.Value <= 0)
                    {
                        string prodCode = !string.IsNullOrWhiteSpace(item.ItemCode)
                            ? item.ItemCode.Trim()
                            : "URN-" + DateTime.Now.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                        if (prodCode.Length > 90) prodCode = prodCode.Substring(0, 90);

                        string prodName = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName.Trim() : "Ürün " + prodCode;
                        if (prodName.Length > 240) prodName = prodName.Substring(0, 240);

                        double purPrice = item.UnitPrice > 0 ? item.UnitPrice : 0;
                        double salePrice = (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0) ? item.NewSalePrice.Value : purPrice * 1.30;
                        double vat = item.VatPercent > 0 ? item.VatPercent : 20;

                        long? existingId = FindProductIdInTransaction(conn, tx, prodName, item.ItemCode);
                        if (existingId.HasValue)
                        {
                            item.ProductId = existingId.Value;
                            if (!string.IsNullOrWhiteSpace(item.Barcode))
                            {
                                TxExec(conn, tx, "UPDATE Products SET Barcode = @b WHERE Id = @id AND (Barcode IS NULL OR Barcode = '');",
                                    ("@b", item.Barcode), ("@id", existingId.Value));
                            }
                        }
                        else
                        {
                            var newPid = TxScalar(conn, tx, @"
INSERT INTO Products (Code, Barcode, Name, Category, Unit, OpeningStock, PurchasePrice, SalePrice, WholesalePrice, SpecialPrice, DiscountPercent, VatPercent, MinStockLevel, IsActive)
VALUES (@c, @b, @n, 'Genel', @u, 0, @p, @sp, @wp, @xp, 0, @vat, 5, 1);
SELECT SCOPE_IDENTITY();",
                                ("@c", prodCode),
                                ("@b", (object?)item.Barcode ?? DBNull.Value),
                                ("@n", prodName),
                                ("@u", item.Unit),
                                ("@p", purPrice),
                                ("@sp", salePrice),
                                ("@wp", purPrice * 1.15),
                                ("@xp", purPrice * 1.20),
                                ("@vat", vat));
                            item.ProductId = Convert.ToInt64(newPid);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(item.Barcode))
                    {
                        TxExec(conn, tx, "UPDATE Products SET Barcode = @b WHERE Id = @id AND (Barcode IS NULL OR Barcode = '');",
                            ("@b", item.Barcode), ("@id", item.ProductId.Value));
                    }

                    string safeItemName = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName.Trim() : "Ürün";
                    if (safeItemName.Length > 490) safeItemName = safeItemName.Substring(0, 490);

                    TxExec(conn, tx, @"
INSERT INTO InvoiceItems (InvoiceId, ProductId, [LineNo], Barcode, ItemCode, ItemName, Quantity, Unit, UnitPrice, DiscountPercent, DiscountAmount, VatPercent, VatAmount, OtherTaxes, LineTotal)
VALUES (@invId, @pId, @lNo, @bar, @code, @name, @qty, @unit, @price, @discP, @discA, @vat, @vatAmt, @oth, @total);",
                        ("@invId", invoiceId),
                        ("@pId", (object?)item.ProductId ?? DBNull.Value),
                        ("@lNo", item.LineNo),
                        ("@bar", (object?)item.Barcode ?? DBNull.Value),
                        ("@code", (object?)item.ItemCode ?? DBNull.Value),
                        ("@name", safeItemName),
                        ("@qty", item.Quantity),
                        ("@unit", item.Unit),
                        ("@price", item.UnitPrice),
                        ("@discP", item.DiscountPercent),
                        ("@discA", item.DiscountAmount),
                        ("@vat", item.VatPercent),
                        ("@vatAmt", item.VatAmount),
                        ("@oth", item.OtherTaxes),
                        ("@total", item.LineTotal));

                    if (!updateStock || !item.ProductId.HasValue || item.ProductId.Value <= 0) continue;

                    long pid = item.ProductId.Value;

                    // Eski stok ve fiyatlar (aynı transaction içinde, aynı faturadaki önceki kalemler dahil)
                    var priceRow = TxQuery(conn, tx, "SELECT PurchasePrice, SalePrice, Unit, PackSize FROM Products WHERE Id = @id;", ("@id", pid));
                    double oldBuy = priceRow.Rows.Count > 0 ? Convert.ToDouble(priceRow.Rows[0]["PurchasePrice"]) : 0;
                    double oldSale = priceRow.Rows.Count > 0 ? Convert.ToDouble(priceRow.Rows[0]["SalePrice"]) : 0;
                    var stockObj = TxScalar(conn, tx, "SELECT CurrentStock FROM vw_ProductStock WHERE ProductId = @id;", ("@id", pid));
                    double oldStock = stockObj != null && stockObj != DBNull.Value ? Convert.ToDouble(stockObj) : 0;
                    double finalSalePrice = oldSale;

                    // Birim dönüşümü: faturada koli/paket/düzine yazıyorsa stoğa ürünün temel birimi (adet) olarak girilir
                    string? productUnit = priceRow.Rows.Count > 0 ? priceRow.Rows[0]["Unit"]?.ToString() : null;
                    double packSize = priceRow.Rows.Count > 0 && priceRow.Rows[0]["PackSize"] != DBNull.Value ? Convert.ToDouble(priceRow.Rows[0]["PackSize"]) : 1;
                    double factor = PackFactor(item.Unit, productUnit, packSize);
                    double stockQty = item.Quantity * factor;

                    // Maliyet: iskonto düşülmüş net birim fiyat
                    double netUnit = NetUnitCost(item) / factor;

                    TxExec(conn, tx, @"
INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note)
VALUES (@date, @pid, 'Gelen', @qty, @price, @doc, @accId, @wId, @note);",
                        ("@date", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                        ("@pid", pid),
                        ("@qty", stockQty),
                        ("@price", netUnit),
                        ("@doc", invoice.InvoiceNumber),
                        ("@accId", invoice.AccountId),
                        ("@wId", invoice.WarehouseId),
                        ("@note", factor != 1 ? $"Fatura Girişi: {invoice.InvoiceNumber} ({item.Quantity:0.##} {item.Unit} x {factor:0.##})" : $"Fatura Girişi: {invoice.InvoiceNumber}"));

                    // Kullanıcının kararı
                    if (string.Equals(item.ActionDecision, "Satış Fiyatına Zam Yap", StringComparison.OrdinalIgnoreCase) ||
                        (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0 && item.NewSalePrice.Value != oldSale))
                    {
                        finalSalePrice = (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0) ? item.NewSalePrice.Value : oldSale;
                        double buy = netUnit > 0 ? netUnit : oldBuy;
                        TxExec(conn, tx, "UPDATE Products SET PurchasePrice = @p, SalePrice = @sp, WholesalePrice = @wp, SpecialPrice = @xp WHERE Id = @id;",
                            ("@p", buy), ("@sp", finalSalePrice), ("@wp", buy * 1.15), ("@xp", buy * 1.20), ("@id", pid));
                    }
                    else if (string.Equals(item.ActionDecision, "Alış Fiyatını Güncelle", StringComparison.OrdinalIgnoreCase) ||
                             string.IsNullOrWhiteSpace(item.ActionDecision))
                    {
                        if (netUnit > 0)
                        {
                            TxExec(conn, tx, "UPDATE Products SET PurchasePrice = @p WHERE Id = @id;", ("@p", netUnit), ("@id", pid));
                        }
                    }
                    // "Olduğu Gibi Al": ürün kartının fiyatlarına dokunulmaz

                    TxExec(conn, tx, @"
INSERT INTO ProductPriceHistory (ProductId, ChangeDate, DocumentNo, SupplierName, OldStock, AddedStock, NewStock, OldPurchasePrice, NewPurchasePrice, OldSalePrice, NewSalePrice, Note)
VALUES (@pid, @dt, @doc, @sup, @oldSt, @addSt, @newSt, @oldB, @newB, @oldS, @newS, @note);",
                        ("@pid", pid),
                        ("@dt", DateTime.Now.ToString("yyyy-MM-dd HH:mm")),
                        ("@doc", invoice.InvoiceNumber),
                        ("@sup", supplierName),
                        ("@oldSt", oldStock),
                        ("@addSt", stockQty),
                        ("@newSt", oldStock + stockQty),
                        ("@oldB", oldBuy),
                        ("@newB", netUnit > 0 ? netUnit : oldBuy),
                        ("@oldS", oldSale),
                        ("@newS", finalSalePrice),
                        ("@note", $"Fatura Girişi: {invoice.InvoiceNumber} [{item.ActionDecision ?? "Normal"}]"));
                }

                // Cari hareketi (Alış faturası = tedarikçiye borçlanma)
                if (updateAccountBalance && invoice.AccountId > 0 && invoice.GrandTotal > 0)
                {
                    TxExec(conn, tx, @"
INSERT INTO AccountMovements (MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES (@date, @accId, 'Alış', @doc, @amt, 'Fatura', 'Fatura Cari Kaydı', @note);",
                        ("@date", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                        ("@accId", invoice.AccountId),
                        ("@doc", invoice.InvoiceNumber),
                        ("@amt", invoice.GrandTotal),
                        ("@note", $"E-Fatura / Alış Faturası Girişi (Toplam: {invoice.GrandTotal:N2} ₺)"));
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                // İşlem geri alındı: arşive kopyalanan PDF yetim kalmasın
                if (archivedPdfPath != null && archiveCreatedNew)
                {
                    try { File.Delete(archivedPdfPath); } catch { }
                }
                throw;
            }
        }

        AuditLogService.Log(
            "Fatura",
            "Fatura Girişi",
            invoiceId,
            invoice.InvoiceNumber,
            null,
            $"{invoice.InvoiceType} kaydedildi. Cari ID: {invoice.AccountId}, Tutar: {invoice.GrandTotal:N2} ₺, Kalem: {invoice.Items.Count}"
        );

        return invoiceId;
    }

    private static readonly HashSet<string> PackUnitCodes = new(StringComparer.OrdinalIgnoreCase)
        { "BX", "CT", "CS", "PK", "PA", "KOLI", "KOLİ", "PAKET", "KUTU", "KOLİ (BX)" };

    /// <summary>Faturadaki birimi ürünün temel birimine çeviren çarpan (koli → adet). Tanımsızsa 1.</summary>
    private static double PackFactor(string? invoiceUnit, string? productUnit, double packSize)
    {
        if (string.IsNullOrWhiteSpace(invoiceUnit)) return 1;
        string u = invoiceUnit.Trim();
        if (!string.IsNullOrWhiteSpace(productUnit) && string.Equals(productUnit.Trim(), u, StringComparison.OrdinalIgnoreCase)) return 1;
        if (u.Equals("DZN", StringComparison.OrdinalIgnoreCase) || u.Equals("DÜZİNE", StringComparison.OrdinalIgnoreCase) || u.Equals("DUZINE", StringComparison.OrdinalIgnoreCase)) return 12;
        if (PackUnitCodes.Contains(u) && packSize > 1) return packSize;
        return 1;
    }

    /// <summary>İskonto düşülmüş net birim maliyet (KDV hariç).</summary>
    private static double NetUnitCost(InvoiceItem item)
    {
        if (item.UnitPrice <= 0) return 0;
        if (item.DiscountPercent > 0) return item.UnitPrice * (1 - item.DiscountPercent / 100.0);
        if (item.DiscountAmount > 0 && item.Quantity > 0) return Math.Max(0, (item.UnitPrice * item.Quantity - item.DiscountAmount) / item.Quantity);
        return item.UnitPrice;
    }

    // ---- Transaction içi yardımcılar ----

    private static SqlCommand TxCmd(SqlConnection conn, SqlTransaction tx, string sql, (string Name, object? Value)[] p)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = Database.NormalizeSql(sql);
        foreach (var x in p)
        {
            if (x.Value is byte[] || string.Equals(x.Name, "@pdfData", StringComparison.OrdinalIgnoreCase))
            {
                // İkili alan: boş (NULL) bile olsa tür açıkça VarBinary verilmeli, yoksa SQL nvarchar sanıp hata verir
                cmd.Parameters.Add(new SqlParameter(x.Name, System.Data.SqlDbType.VarBinary, -1) { Value = x.Value ?? DBNull.Value });
            }
            else
            {
                cmd.Parameters.AddWithValue(x.Name, x.Value ?? DBNull.Value);
            }
        }
        return cmd;
    }

    private static int TxExec(SqlConnection conn, SqlTransaction tx, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = TxCmd(conn, tx, sql, p);
        return cmd.ExecuteNonQuery();
    }

    private static object? TxScalar(SqlConnection conn, SqlTransaction tx, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = TxCmd(conn, tx, sql, p);
        return cmd.ExecuteScalar();
    }

    private static DataTable TxQuery(SqlConnection conn, SqlTransaction tx, string sql, params (string Name, object? Value)[] p)
    {
        using var cmd = TxCmd(conn, tx, sql, p);
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        t.Load(r);
        return t;
    }

    /// <summary>
    /// Ürün eşleştirme: önce ürün kodu, sonra tam ad. Benzer ad (bulanık) eşleşmesi yalnızca tek aday varsa kullanılır;
    /// birden fazla aday varsa yanlış karta stok girmesin diye eşleşme yapılmaz ve yeni kart açılır.
    /// </summary>
    private static long? FindProductIdInTransaction(SqlConnection conn, SqlTransaction tx, string name, string? itemCode)
    {
        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            var byCode = TxQuery(conn, tx, "SELECT TOP 1 Id FROM Products WHERE Code = @c AND IsActive = 1 ORDER BY Id;", ("@c", itemCode.Trim()));
            if (byCode.Rows.Count > 0) return Convert.ToInt64(byCode.Rows[0]["Id"]);
        }

        if (string.IsNullOrWhiteSpace(name)) return null;
        string trimmed = name.Trim();

        var exact = TxQuery(conn, tx, "SELECT TOP 1 Id FROM Products WHERE Name = @n AND IsActive = 1 ORDER BY Id;", ("@n", trimmed));
        if (exact.Rows.Count > 0) return Convert.ToInt64(exact.Rows[0]["Id"]);

        var fuzzy = TxQuery(conn, tx, "SELECT TOP 2 Id FROM Products WHERE Name LIKE @like AND IsActive = 1 ORDER BY Id;",
            ("@like", $"%{trimmed.Substring(0, Math.Min(25, trimmed.Length))}%"));
        return fuzzy.Rows.Count == 1 ? Convert.ToInt64(fuzzy.Rows[0]["Id"]) : null;
    }

    /// <summary>Faturanın stok ve cari hareketlerini ve kendisini aynı transaction içinde geri alır.</summary>
    private static (string InvoiceNumber, int StockDeleted, int AccountDeleted)? RollbackInvoiceInTransaction(SqlConnection conn, SqlTransaction tx, long invoiceId)
    {
        var dt = TxQuery(conn, tx, "SELECT InvoiceNumber, AccountId FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
        if (dt.Rows.Count == 0) return null;

        string invNo = dt.Rows[0]["InvoiceNumber"]?.ToString() ?? "";
        long accId = dt.Rows[0]["AccountId"] == DBNull.Value ? 0 : Convert.ToInt64(dt.Rows[0]["AccountId"]);

        int stockDeleted = 0, accDeleted = 0;
        if (!string.IsNullOrWhiteSpace(invNo))
        {
            // Yalnızca BU faturanın yazdığı hareketler: aynı belge no + aynı cari + fatura kaynaklı kayıt
            stockDeleted = TxExec(conn, tx,
                "DELETE FROM StockMovements WHERE DocumentNo = @doc AND COALESCE(AccountId, 0) = @acc AND Note LIKE N'Fatura Girişi:%';",
                ("@doc", invNo), ("@acc", accId));
            accDeleted = TxExec(conn, tx,
                "DELETE FROM AccountMovements WHERE DocumentNo = @doc AND AccountId = @acc AND TransactionType IN (N'Alış', N'Ödeme');",
                ("@doc", invNo), ("@acc", accId));
        }

        TxExec(conn, tx, "DELETE FROM InvoicePayments WHERE InvoiceId = @id;", ("@id", invoiceId));
        TxExec(conn, tx, "DELETE FROM InvoiceItems WHERE InvoiceId = @id;", ("@id", invoiceId));
        TxExec(conn, tx, "DELETE FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
        return (invNo, stockDeleted, accDeleted);
    }

    /// <summary>
    /// VKN veya Cari Unvanına göre sistemdeki mevcut cariyi bulur.
    /// </summary>
    public static Account? FindAccountByTaxOrName(string? taxNumber, string? name)
    {
        if (!string.IsNullOrWhiteSpace(taxNumber))
        {
            var dt = Database.Query("SELECT TOP 1 * FROM Accounts WHERE TaxNumber = @tax AND IsActive = 1;", ("@tax", taxNumber.Trim()));
            if (dt.Rows.Count > 0)
                return AccountService.GetById(Convert.ToInt64(dt.Rows[0]["Id"]));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var dt = Database.Query("SELECT TOP 1 * FROM Accounts WHERE (Name = @n OR Name LIKE @nLike) AND IsActive = 1;",
                ("@n", name.Trim()),
                ("@nLike", $"%{name.Trim().Substring(0, Math.Min(12, name.Trim().Length))}%")
            );
            if (dt.Rows.Count > 0)
                return AccountService.GetById(Convert.ToInt64(dt.Rows[0]["Id"]));
        }

        return null;
    }

    /// <summary>
    /// Ürün Koduna veya Ürün Adına göre sistemdeki mevcut ürünü bulur.
    /// Gelen faturanın barkodu tüm ürünler için ortak olabileceğinden ürün ayrıştırma barkod üzerinden yapılmaz.
    /// </summary>
    public static Product? FindProductByNameOrCode(string name, string? itemCode)
    {
        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            var dt = Database.Query("SELECT TOP 1 * FROM Products WHERE Code = @c AND IsActive = 1;", ("@c", itemCode.Trim()));
            if (dt.Rows.Count > 0)
                return ProductService.GetById(Convert.ToInt64(dt.Rows[0]["Id"]));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var dt = Database.Query("SELECT TOP 1 * FROM Products WHERE (Name = @n OR Name LIKE @nLike) AND IsActive = 1;",
                ("@n", name.Trim()),
                ("@nLike", $"%{name.Trim().Substring(0, Math.Min(15, name.Trim().Length))}%")
            );
            if (dt.Rows.Count > 0)
                return ProductService.GetById(Convert.ToInt64(dt.Rows[0]["Id"]));
        }

        return null;
    }

    /// <summary>
    /// Fatura numarasına göre kayıtlı faturayı getirir.
    /// </summary>
    public static DataRow? GetInvoiceByNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;
        var dt = Database.Query(@"
SELECT TOP 1 inv.*, COALESCE(a.Name, '-') AS AccountName, COALESCE(w.Name, 'Merkez Depo') AS WarehouseName
FROM Invoices inv
LEFT JOIN Accounts a ON inv.AccountId = a.Id
LEFT JOIN Warehouses w ON inv.WarehouseId = w.Id
WHERE inv.InvoiceNumber = @num
ORDER BY inv.Id DESC;", ("@num", invoiceNumber.Trim()));
        return dt.Rows.Count > 0 ? dt.Rows[0] : null;
    }

    /// <summary>
    /// Sistemde mükerrer (aynı fatura numarasıyla birden fazla kez) kaydedilmiş faturaları tespit eder,
    /// en dolu ve en güncel kaydı koruyarak fazlalık mükerrer kayıtları temizler.
    /// </summary>
    public static (int RemovedCount, int UniqueCount) DeduplicateInvoices()
    {
        var dtDups = Database.Query(@"
SELECT InvoiceNumber, COUNT(*) AS Cnt
FROM Invoices
WHERE InvoiceNumber IS NOT NULL AND InvoiceNumber != ''
GROUP BY InvoiceNumber
HAVING COUNT(*) > 1;");

        int removedTotal = 0;

        foreach (DataRow r in dtDups.Rows)
        {
            string invNo = r["InvoiceNumber"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(invNo)) continue;

            var allDups = Database.Query(@"
SELECT i.Id, i.CreatedAt, 
       (SELECT COUNT(*) FROM InvoiceItems ii WHERE ii.InvoiceId = i.Id) AS ItemCount,
       CASE WHEN i.PdfData IS NOT NULL THEN 1 ELSE 0 END AS HasPdf
FROM Invoices i
WHERE i.InvoiceNumber = @num
ORDER BY ItemCount DESC, HasPdf DESC, i.Id DESC;", ("@num", invNo));

            if (allDups.Rows.Count <= 1) continue;

            // En iyi kaydı sakla (en çok kalemi olan veya en son yüklenen)
            for (int i = 1; i < allDups.Rows.Count; i++)
            {
                long removeId = Convert.ToInt64(allDups.Rows[i]["Id"]);
                Database.Execute("DELETE FROM InvoiceItems WHERE InvoiceId = @id;", ("@id", removeId));
                Database.Execute("DELETE FROM Invoices WHERE Id = @id;", ("@id", removeId));
                removedTotal++;
            }
        }

        var dtTotal = Database.Query("SELECT COUNT(*) AS Cnt FROM Invoices;");
        int uniqueCount = dtTotal.Rows.Count > 0 ? Convert.ToInt32(dtTotal.Rows[0]["Cnt"]) : 0;

        if (removedTotal > 0)
        {
            AuditLogService.Log("Fatura", "Mükerrer Temizleme", 0, "Faturalar", null, null, $"{removedTotal} adet mükerrer fatura kaydı temizlendi. Kalan tekil fatura: {uniqueCount}");
        }

        return (removedTotal, uniqueCount);
    }

    /// <summary>
    /// Bir faturayı iptal eder ve oluşturduğu tüm stok ve cari hareketlerini geri alır (Rollback).
    /// Geri dönüşü olmayan hata korkusunu ortadan kaldırır!
    /// </summary>
    public static (bool Success, string Message) CancelAndRollbackInvoice(long invoiceId, bool rollbackMovements = true)
    {
        try
        {
            string invNo; double total; string invType;
            int stockMovesDeleted = 0, accountMovesDeleted = 0;

            using (var conn = Database.Open())
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var dt = TxQuery(conn, tx, "SELECT InvoiceNumber, GrandTotal, InvoiceType FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
                    if (dt.Rows.Count == 0)
                        return (false, "Fatura bulunamadı.");

                    invNo = dt.Rows[0]["InvoiceNumber"]?.ToString() ?? "";
                    total = Convert.ToDouble(dt.Rows[0]["GrandTotal"] == DBNull.Value ? 0 : dt.Rows[0]["GrandTotal"]);
                    invType = dt.Rows[0]["InvoiceType"]?.ToString() ?? "Fatura";

                    if (rollbackMovements)
                    {
                        // Hareketler, fatura ve ödemeler tek transaction içinde ve yalnızca bu faturaya ait olanlar silinir
                        var res = RollbackInvoiceInTransaction(conn, tx, invoiceId);
                        stockMovesDeleted = res?.StockDeleted ?? 0;
                        accountMovesDeleted = res?.AccountDeleted ?? 0;
                    }
                    else
                    {
                        TxExec(conn, tx, "DELETE FROM InvoicePayments WHERE InvoiceId = @id;", ("@id", invoiceId));
                        TxExec(conn, tx, "DELETE FROM InvoiceItems WHERE InvoiceId = @id;", ("@id", invoiceId));
                        TxExec(conn, tx, "DELETE FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
                    }

                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); } catch { }
                    throw;
                }
            }

            AuditLogService.Log(
                "Fatura",
                "Fatura İptal & Geri Alma (Rollback)",
                invoiceId,
                invNo,
                null,
                null,
                $"{invNo} numaralı {invType} ({total:N2} ₺) tamamen iptal edildi. " +
                $"Geri alınan stok hareketi: {stockMovesDeleted}, geri alınan cari hareketi: {accountMovesDeleted}."
            );

            return (true, $"{invNo} faturası ve ilişkili {stockMovesDeleted} adet stok hareketi, {accountMovesDeleted} adet cari hareketi başarıyla geri alındı ve fatura iptal edildi.");
        }
        catch (Exception ex)
        {
            return (false, "Fatura geri alınırken hata oluştu: " + ex.Message);
        }
    }

    /// <summary>
    /// Bir faturayı ve kalemlerini veritabanından tamamen siler (Stok ve cari hareketlerini de geri alır).
    /// </summary>
    public static bool DeleteInvoice(long invoiceId)
    {
        var result = CancelAndRollbackInvoice(invoiceId, rollbackMovements: true);
        return result.Success;
    }

    /// <summary>
    /// Tüm faturaları listeler.
    /// </summary>
    public static DataTable GetAllInvoices(string? search = null)
    {
        string sql = @"
SELECT 
    i.Id,
    i.InvoiceNumber AS [Fatura No],
    i.InvoiceDate AS [Fatura Tarihi],
    i.InvoiceType AS [Tür],
    a.Name AS [Cari Ünvanı],
    w.Name AS [Depo],
    i.SubTotal AS [Matrah],
    i.VatTotal AS [KDV Toplamı],
    i.GrandTotal AS [Genel Toplam (₺)],
    COALESCE(i.PdfPath, '') AS [Belge Yolu],
    COALESCE(i.Note, '') AS [Açıklama],
    i.CreatedAt AS [Kayıt Tarihi]
FROM Invoices i
LEFT JOIN Accounts a ON a.Id = i.AccountId
LEFT JOIN Warehouses w ON w.Id = i.WarehouseId
WHERE 1=1
";
        var pars = new List<(string, object?)>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (i.InvoiceNumber LIKE @s OR a.Name LIKE @s OR i.Note LIKE @s)";
            pars.Add(("@s", $"%{search.Trim()}%"));
        }

        sql += " ORDER BY i.InvoiceDate DESC, i.Id DESC;";
        return Database.Query(sql, pars.ToArray());
    }

    /// <summary>
    /// Bir faturanın detay kalemlerini getirir.
    /// </summary>
    public static DataTable GetInvoiceItems(long invoiceId)
    {
        return Database.Query(@"
SELECT 
    ii.Id,
    ii.[LineNo] AS [Sıra No],
    COALESCE(ii.Barcode, '') AS [Barkod],
    COALESCE(p.Code, ii.ItemCode, '') AS [Ürün Kodu],
    ii.ItemName AS [Ürün Adı],
    ii.Quantity AS [Miktar],
    ii.Unit AS [Birim],
    ii.UnitPrice AS [Birim Fiyat (₺)],
    ii.DiscountPercent AS [İskonto %],
    ii.DiscountAmount AS [İskonto (₺)],
    ii.VatPercent AS [KDV %],
    ii.VatAmount AS [KDV Tutarı],
    ii.OtherTaxes AS [Diğer Vergiler],
    ii.LineTotal AS [Satır Toplamı (₺)]
FROM InvoiceItems ii
LEFT JOIN Products p ON p.Id = ii.ProductId
WHERE ii.InvoiceId = @id
ORDER BY ii.[LineNo] ASC, ii.Id ASC;",
            ("@id", invoiceId)
        );
    }

    /// <summary>
    /// Bir ürüne ait tüm alış faturalarını ve PDF yollarını getirir.
    /// </summary>
    public static DataTable GetProductInvoices(long productId)
    {
        return Database.Query(@"
SELECT 
    inv.Id,
    inv.InvoiceNumber AS [Fatura No],
    inv.InvoiceDate AS [Fatura Tarihi],
    COALESCE(a.Name, '-') AS [Tedarikçi],
    ii.Quantity AS [Alınan Miktar],
    ii.Unit AS [Birim],
    ii.UnitPrice AS [Alış Fiyatı (₺)],
    ii.LineTotal AS [Satır Tutarı (₺)],
    COALESCE(inv.PdfPath, '') AS [PdfPath]
FROM InvoiceItems ii
INNER JOIN Invoices inv ON ii.InvoiceId = inv.Id
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE ii.ProductId = @pid
ORDER BY inv.InvoiceDate DESC, inv.Id DESC;",
            ("@pid", productId)
        );
    }

    /// <summary>
    /// Bir faturanın PDF belgesini hem veritabanındaki (PdfData), hem de dosya yolundaki (PdfPath) veriden açar.
    /// </summary>
    public static bool OpenPdfDataOrPath(byte[]? pdfData, string? pdfPath, string invoiceNumber = "Fatura")
    {
        // 1. Önce veritabanına gömülmüş PdfData kontrol edilir
        if (pdfData != null && pdfData.Length > 0)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Bilensis_Invoices");
                Directory.CreateDirectory(tempDir);
                string cleanNo = string.Join("_", invoiceNumber.Split(Path.GetInvalidFileNameChars()));
                string targetPath = Path.Combine(tempDir, $"{cleanNo}.pdf");
                File.WriteAllBytes(targetPath, pdfData);

                return FileLauncherHelper.OpenDocument(targetPath, $"{invoiceNumber} Numaralı Fatura");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Veritabanından PDF açılırken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // 2. Veritabanında yoksa diskteki PdfPath açılır
        if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
        {
            return OpenPdfFile(pdfPath);
        }

        MessageBox.Show("Bu faturaya ait PDF belgesi veritabanında veya arşiv klasöründe bulunamadı.", "PDF Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>
    /// Ürüne ait en güncel alış faturası PDF'ini veritabanından (PdfData) veya diskten bulup açar.
    /// </summary>
    public static bool OpenLatestInvoicePdfForProduct(long productId)
    {
        var (invRow, _) = GetProductInvoiceMetaAndAllItems(productId);
        if (invRow != null)
        {
            string invNo = invRow["InvoiceNumber"]?.ToString() ?? "Fatura";
            string? path = invRow["PdfPath"] == DBNull.Value ? null : invRow["PdfPath"]?.ToString();
            byte[]? data = invRow["PdfData"] == DBNull.Value ? null : (byte[]?)invRow["PdfData"];

            return OpenPdfDataOrPath(data, path, invNo);
        }

        return false;
    }

    /// <summary>
    /// Fatura ID'sine göre PDF'i veritabanından (PdfData) veya diskten açar.
    /// </summary>
    public static bool OpenInvoicePdfById(long invoiceId)
    {
        var dt = Database.Query("SELECT TOP 1 InvoiceNumber, PdfPath, PdfData FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            string invNo = row["InvoiceNumber"]?.ToString() ?? "Fatura";
            string? path = row["PdfPath"] == DBNull.Value ? null : row["PdfPath"]?.ToString();
            byte[]? data = row["PdfData"] == DBNull.Value ? null : (byte[]?)row["PdfData"];
            return OpenPdfDataOrPath(data, path, invNo);
        }
        return false;
    }

    /// <summary>
    /// Fatura numarasına göre PDF'i veritabanından (PdfData) veya diskten açar.
    /// </summary>
    public static bool OpenInvoicePdfByNumber(string invoiceNumber)
    {
        var dt = Database.Query("SELECT TOP 1 InvoiceNumber, PdfPath, PdfData FROM Invoices WHERE InvoiceNumber = @num;", ("@num", invoiceNumber));
        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            string invNo = row["InvoiceNumber"]?.ToString() ?? invoiceNumber;
            string? path = row["PdfPath"] == DBNull.Value ? null : row["PdfPath"]?.ToString();
            byte[]? data = row["PdfData"] == DBNull.Value ? null : (byte[]?)row["PdfData"];
            return OpenPdfDataOrPath(data, path, invNo);
        }
        return false;
    }

    /// <summary>
    /// Eski kayıtlardaki PDF verisini (veritabanına gömülü veya arşiv dışı) PDF arşiv klasörüne taşır.
    /// </summary>
    public static void MigrateExistingPdfsToDatabase()
    {
        // Eski ad korunur (çağıran kodu bozmamak için): artık PDF'ler veritabanına GÖMÜLMEZ, arşiv klasörüne taşınır.
        PdfArchiveService.MigrateLegacy();
    }

    /// <summary>
    /// Bir faturanın PDF dosyasını sistemin varsayılan PDF görüntüleyicisinde açar.
    /// </summary>
    public static bool OpenPdfFile(string? filePath)
    {
        return FileLauncherHelper.OpenDocument(filePath, "Fatura PDF Belgesi");
    }

    /// <summary>
    /// Fatura ID'sine göre fatura meta bilgilerini ve o faturadaki tüm kalemleri getirir.
    /// </summary>
    public static (DataRow? InvoiceRow, DataTable Items) GetInvoiceMetaById(long invoiceId)
    {
        var dtInv = Database.Query(@"
SELECT TOP 1 
    inv.Id,
    inv.InvoiceNumber,
    inv.InvoiceDate,
    inv.InvoiceType,
    COALESCE(inv.CustomizationId, '') AS CustomizationId,
    COALESCE(inv.Scenario, '') AS Scenario,
    COALESCE(inv.InvoiceKind, '') AS InvoiceKind,
    COALESCE(inv.OrderNumber, '') AS OrderNumber,
    COALESCE(inv.OrderDate, '') AS OrderDate,
    COALESCE(inv.RelatedStore, '') AS RelatedStore,
    COALESCE(inv.CargoId, '') AS CargoId,
    COALESCE(inv.ReferenceNo, '') AS ReferenceNo,
    COALESCE(inv.IssueTime, '') AS IssueTime,
    inv.SubTotal,
    inv.VatTotal,
    inv.GrandTotal,
    COALESCE(inv.PdfPath, '') AS PdfPath,
    inv.PdfData,
    COALESCE(inv.Note, '') AS Note,
    COALESCE(a.Name, '-') AS AccountName
FROM Invoices inv
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE inv.Id = @id;", ("@id", invoiceId));

        if (dtInv.Rows.Count > 0)
        {
            return (dtInv.Rows[0], GetInvoiceItems(invoiceId));
        }
        return (null, new DataTable());
    }

    /// <summary>
    /// Fatura Numarasına göre fatura meta bilgilerini ve o faturadaki tüm kalemleri getirir.
    /// </summary>
    public static (DataRow? InvoiceRow, DataTable Items) GetInvoiceMetaByNumber(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return (null, new DataTable());

        var dtInv = Database.Query(@"
SELECT TOP 1 
    inv.Id,
    inv.InvoiceNumber,
    inv.InvoiceDate,
    inv.InvoiceType,
    COALESCE(inv.CustomizationId, '') AS CustomizationId,
    COALESCE(inv.Scenario, '') AS Scenario,
    COALESCE(inv.InvoiceKind, '') AS InvoiceKind,
    COALESCE(inv.OrderNumber, '') AS OrderNumber,
    COALESCE(inv.OrderDate, '') AS OrderDate,
    COALESCE(inv.RelatedStore, '') AS RelatedStore,
    COALESCE(inv.CargoId, '') AS CargoId,
    COALESCE(inv.ReferenceNo, '') AS ReferenceNo,
    COALESCE(inv.IssueTime, '') AS IssueTime,
    inv.SubTotal,
    inv.VatTotal,
    inv.GrandTotal,
    COALESCE(inv.PdfPath, '') AS PdfPath,
    inv.PdfData,
    COALESCE(inv.Note, '') AS Note,
    COALESCE(a.Name, '-') AS AccountName
FROM Invoices inv
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE inv.InvoiceNumber = @num
ORDER BY inv.InvoiceDate DESC, inv.Id DESC;", ("@num", invoiceNumber.Trim()));

        if (dtInv.Rows.Count > 0)
        {
            long invId = Convert.ToInt64(dtInv.Rows[0]["Id"]);
            return (dtInv.Rows[0], GetInvoiceItems(invId));
        }
        return (null, new DataTable());
    }

    /// <summary>
    /// Bir ürüne ait en son alış faturasını ve o faturadaki TÜM diğer ürünleri getirir.
    /// Ürün ID'si eşleşmezse stok hareketlerindeki Belge No ve Ürün Adı üzerinden akıllı ters arama yapar.
    /// </summary>
    public static (DataRow? InvoiceRow, DataTable Items) GetProductInvoiceMetaAndAllItems(long productId)
    {
        // 1. Doğrudan InvoiceItems.ProductId ile eşleşen faturayı ara
        var dtInv = Database.Query(@"
SELECT TOP 1 
    inv.Id,
    inv.InvoiceNumber,
    inv.InvoiceDate,
    inv.InvoiceType,
    COALESCE(inv.CustomizationId, '') AS CustomizationId,
    COALESCE(inv.Scenario, '') AS Scenario,
    COALESCE(inv.InvoiceKind, '') AS InvoiceKind,
    COALESCE(inv.OrderNumber, '') AS OrderNumber,
    COALESCE(inv.OrderDate, '') AS OrderDate,
    COALESCE(inv.RelatedStore, '') AS RelatedStore,
    COALESCE(inv.CargoId, '') AS CargoId,
    COALESCE(inv.ReferenceNo, '') AS ReferenceNo,
    COALESCE(inv.IssueTime, '') AS IssueTime,
    inv.SubTotal,
    inv.VatTotal,
    inv.GrandTotal,
    COALESCE(inv.PdfPath, '') AS PdfPath,
    inv.PdfData,
    COALESCE(inv.Note, '') AS Note,
    COALESCE(a.Name, '-') AS AccountName
FROM InvoiceItems ii
INNER JOIN Invoices inv ON ii.InvoiceId = inv.Id
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE ii.ProductId = @pid
ORDER BY inv.InvoiceDate DESC, inv.Id DESC;",
            ("@pid", productId)
        );

        // 2. Ürünün stok hareketlerindeki DocumentNo (Fatura No) üzerinden Invoices'ta ara
        if (dtInv.Rows.Count == 0)
        {
            var dtDoc = Database.Query(@"
SELECT TOP 1 DocumentNo 
FROM StockMovements 
WHERE ProductId = @pid AND DocumentNo IS NOT NULL AND DocumentNo != '' 
ORDER BY MovementDate DESC, Id DESC;", ("@pid", productId));

            if (dtDoc.Rows.Count > 0)
            {
                string docNo = dtDoc.Rows[0]["DocumentNo"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    dtInv = Database.Query(@"
SELECT TOP 1 
    inv.Id, inv.InvoiceNumber, inv.InvoiceDate, inv.InvoiceType,
    COALESCE(inv.CustomizationId, '') AS CustomizationId,
    COALESCE(inv.Scenario, '') AS Scenario,
    COALESCE(inv.InvoiceKind, '') AS InvoiceKind,
    COALESCE(inv.OrderNumber, '') AS OrderNumber,
    COALESCE(inv.OrderDate, '') AS OrderDate,
    COALESCE(inv.RelatedStore, '') AS RelatedStore,
    COALESCE(inv.CargoId, '') AS CargoId,
    COALESCE(inv.ReferenceNo, '') AS ReferenceNo,
    COALESCE(inv.IssueTime, '') AS IssueTime,
    inv.SubTotal, inv.VatTotal, inv.GrandTotal,
    COALESCE(inv.PdfPath, '') AS PdfPath, inv.PdfData,
    COALESCE(inv.Note, '') AS Note,
    COALESCE(a.Name, '-') AS AccountName
FROM Invoices inv
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE inv.InvoiceNumber = @doc
ORDER BY inv.InvoiceDate DESC, inv.Id DESC;", ("@doc", docNo));
                }
            }
        }

        // 3. Barkod, Kod veya Ürün Adı eşleşmesiyle Invoices'ta ara
        if (dtInv.Rows.Count == 0)
        {
            var p = ProductService.GetById(productId);
            if (p != null)
            {
                string namePart = p.Name.Length > 5 ? p.Name.Substring(0, 5) : p.Name;
                dtInv = Database.Query(@"
SELECT TOP 1 
    inv.Id, inv.InvoiceNumber, inv.InvoiceDate, inv.InvoiceType,
    COALESCE(inv.CustomizationId, '') AS CustomizationId,
    COALESCE(inv.Scenario, '') AS Scenario,
    COALESCE(inv.InvoiceKind, '') AS InvoiceKind,
    COALESCE(inv.OrderNumber, '') AS OrderNumber,
    COALESCE(inv.OrderDate, '') AS OrderDate,
    COALESCE(inv.RelatedStore, '') AS RelatedStore,
    COALESCE(inv.CargoId, '') AS CargoId,
    COALESCE(inv.ReferenceNo, '') AS ReferenceNo,
    COALESCE(inv.IssueTime, '') AS IssueTime,
    inv.SubTotal, inv.VatTotal, inv.GrandTotal,
    COALESCE(inv.PdfPath, '') AS PdfPath, inv.PdfData,
    COALESCE(inv.Note, '') AS Note,
    COALESCE(a.Name, '-') AS AccountName
FROM InvoiceItems ii
INNER JOIN Invoices inv ON ii.InvoiceId = inv.Id
LEFT JOIN Accounts a ON inv.AccountId = a.Id
WHERE ((ii.Barcode = @b AND @b != '') OR (ii.ItemCode = @c AND @c != '') OR (ii.ItemName LIKE @n AND @n != ''))
ORDER BY inv.InvoiceDate DESC, inv.Id DESC;",
                    ("@b", p.Barcode ?? ""),
                    ("@c", p.Code ?? ""),
                    ("@n", $"%{namePart}%")
                );
            }
        }

        if (dtInv.Rows.Count > 0)
        {
            var row = dtInv.Rows[0];
            long invId = Convert.ToInt64(row["Id"]);
            var itemsDt = GetInvoiceItems(invId);
            return (row, itemsDt);
        }

        return (null, new DataTable());
    }

    /// <summary>
    /// Faturaları ödeme durumu, ödenen tutar ve kalan tutar bilgileriyle birlikte listeler.
    /// </summary>
    public static DataTable GetAllInvoicesWithPaymentStatus(
        string? type = null, 
        string? search = null, 
        string? paymentStatus = null, 
        DateTime? start = null, 
        DateTime? end = null)
    {
        var sql = @"
SELECT 
    inv.Id,
    inv.InvoiceNumber AS [Fatura No],
    inv.InvoiceDate AS [Tarih],
    inv.InvoiceType AS [Fatura Türü],
    COALESCE(a.Name, '(Tanımsız Cari)') AS [Cari Ünvanı],
    inv.GrandTotal AS [Fatura Tutarı],
    COALESCE(inv.PaidAmount, 0) AS [Ödenen Tutar],
    (inv.GrandTotal - COALESCE(inv.PaidAmount, 0)) AS [Kalan Tutar],
    CASE 
        WHEN COALESCE(inv.PaidAmount, 0) >= (inv.GrandTotal - 0.01) THEN 'Ödendi'
        WHEN COALESCE(inv.PaidAmount, 0) > 0 THEN 'Kısmi Ödendi'
        ELSE 'Ödenmedi'
    END AS [Ödeme Durumu],
    COALESCE(w.Name, 'Merkez Depo') AS [Depo / Şube],
    COALESCE(inv.Note, '') AS [Açıklama],
    inv.AccountId,
    inv.WarehouseId
FROM Invoices inv
LEFT JOIN Accounts a ON inv.AccountId = a.Id
LEFT JOIN Warehouses w ON inv.WarehouseId = w.Id
WHERE 1=1
";
        var p = new List<(string Name, object? Value)>();

        if (!string.IsNullOrWhiteSpace(type) && type != "Tümü" && type != "Tüm Faturalar")
        {
            sql += " AND inv.InvoiceType = @type";
            p.Add(("@type", type));
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) && paymentStatus != "Tümü")
        {
            if (paymentStatus == "Ödendi")
            {
                sql += " AND COALESCE(inv.PaidAmount, 0) >= (inv.GrandTotal - 0.01)";
            }
            else if (paymentStatus == "Kısmi Ödendi")
            {
                sql += " AND COALESCE(inv.PaidAmount, 0) > 0 AND COALESCE(inv.PaidAmount, 0) < (inv.GrandTotal - 0.01)";
            }
            else if (paymentStatus == "Ödenmedi")
            {
                sql += " AND COALESCE(inv.PaidAmount, 0) = 0";
            }
        }

        if (start.HasValue)
        {
            sql += " AND inv.InvoiceDate >= @start";
            p.Add(("@start", start.Value.ToString("yyyy-MM-dd")));
        }

        if (end.HasValue)
        {
            sql += " AND inv.InvoiceDate <= @end";
            p.Add(("@end", end.Value.ToString("yyyy-MM-dd")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            sql += " AND (inv.InvoiceNumber LIKE @s OR a.Name LIKE @s OR inv.Note LIKE @s)";
            p.Add(("@s", $"%{search.Trim()}%"));
        }

        sql += " ORDER BY inv.InvoiceDate DESC, inv.Id DESC;";

        return Database.Query(sql, p.ToArray());
    }

    /// <summary>
    /// Bir faturaya kısmi veya tam ödeme yapar. Kasa ve cari hareketini tek işlemde oluşturur.
    /// </summary>
    public static (bool Success, string Message, double NewRemaining) AddInvoicePayment(
        long invoiceId, 
        double paymentAmount, 
        string paymentMethod, 
        string note, 
        DateTime paymentDate)
    {
        if (paymentAmount <= 0)
        {
            return (false, "Ödeme tutarı 0'dan büyük olmalıdır.", 0);
        }

        var dt = Database.Query("SELECT Id, InvoiceNumber, InvoiceType, AccountId, GrandTotal, COALESCE(PaidAmount, 0) AS PaidAmount FROM Invoices WHERE Id = @id", ("@id", invoiceId));
        if (dt.Rows.Count == 0)
        {
            return (false, "Fatura bulunamadı.", 0);
        }

        var row = dt.Rows[0];
        string invNo = row["InvoiceNumber"]?.ToString() ?? "";
        string invType = row["InvoiceType"]?.ToString() ?? "Alış Faturası";
        long accountId = Convert.ToInt64(row["AccountId"]);
        double grandTotal = Convert.ToDouble(row["GrandTotal"]);
        double currentPaid = Convert.ToDouble(row["PaidAmount"]);
        double remaining = grandTotal - currentPaid;

        if (paymentAmount > remaining + 0.01)
        {
            return (false, $"Ödeme tutarı kalan tutardan ({remaining:N2} ₺) fazla olamaz.", remaining);
        }

        double newPaid = Math.Min(grandTotal, currentPaid + paymentAmount);
        double newRemaining = Math.Max(0, grandTotal - newPaid);
        string newStatus = newPaid >= (grandTotal - 0.01) ? "Ödendi" : (newPaid > 0 ? "Kısmi Ödendi" : "Ödenmedi");

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            long? accountMovementId = null;

            // 1. Kasa / Finansal Hareket (Cari Hareketi) Ekle
            if (accountId > 0)
            {
                string transType = invType.Contains("Satış") ? "Tahsilat" : "Ödeme";
                string fullNote = $"Fatura No: {invNo} ({(newRemaining == 0 ? "Tam Ödeme" : "Kısmi Ödeme")}). {note}".Trim();

                using var cmdAcc = conn.CreateCommand();
                cmdAcc.Transaction = tx;
                cmdAcc.CommandText = Database.NormalizeSql(@"
INSERT INTO AccountMovements(MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES(@d, @a, @t, @doc, @amt, @m, @cb, @n);
SELECT SCOPE_IDENTITY();");
                cmdAcc.Parameters.AddWithValue("@d", paymentDate.ToString("yyyy-MM-dd"));
                cmdAcc.Parameters.AddWithValue("@a", accountId);
                cmdAcc.Parameters.AddWithValue("@t", transType);
                cmdAcc.Parameters.AddWithValue("@doc", invNo);
                cmdAcc.Parameters.AddWithValue("@amt", paymentAmount);
                cmdAcc.Parameters.AddWithValue("@m", paymentMethod);
                cmdAcc.Parameters.AddWithValue("@cb", "Merkez Kasa");
                cmdAcc.Parameters.AddWithValue("@n", fullNote);

                var movIdObj = cmdAcc.ExecuteScalar();
                if (movIdObj != null && long.TryParse(movIdObj.ToString(), out long mId))
                {
                    accountMovementId = mId;
                }
            }

            // 2. InvoicePayments tablosuna kayıt
            using var cmdPay = conn.CreateCommand();
            cmdPay.Transaction = tx;
            cmdPay.CommandText = Database.NormalizeSql(@"
INSERT INTO InvoicePayments(InvoiceId, PaymentDate, Amount, PaymentMethod, AccountMovementId, Note, CreatedAt)
VALUES(@invId, @pDate, @amt, @m, @movId, @n, @created);");
            cmdPay.Parameters.AddWithValue("@invId", invoiceId);
            cmdPay.Parameters.AddWithValue("@pDate", paymentDate.ToString("yyyy-MM-dd"));
            cmdPay.Parameters.AddWithValue("@amt", paymentAmount);
            cmdPay.Parameters.AddWithValue("@m", paymentMethod);
            cmdPay.Parameters.AddWithValue("@movId", accountMovementId.HasValue ? (object)accountMovementId.Value : DBNull.Value);
            cmdPay.Parameters.AddWithValue("@n", note ?? "");
            cmdPay.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmdPay.ExecuteNonQuery();

            // 3. Invoices tablosunu güncelle
            using var cmdInv = conn.CreateCommand();
            cmdInv.Transaction = tx;
            cmdInv.CommandText = Database.NormalizeSql(@"
UPDATE Invoices 
SET PaidAmount = @paid, PaymentStatus = @st 
WHERE Id = @id;");
            cmdInv.Parameters.AddWithValue("@paid", newPaid);
            cmdInv.Parameters.AddWithValue("@st", newStatus);
            cmdInv.Parameters.AddWithValue("@id", invoiceId);
            cmdInv.ExecuteNonQuery();

            tx.Commit();
            return (true, $"Ödeme başarıyla işlendi. Faturadan {paymentAmount:N2} ₺ düşüldü, Kalan: {newRemaining:N2} ₺.", newRemaining);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return (false, "Ödeme işlenirken veritabanı hatası oluştu: " + ex.Message, remaining);
        }
    }

    /// <summary>
    /// Bir faturaya ait tüm ödeme geçmişini (kısmi/tam) listeler.
    /// </summary>
    public static DataTable GetInvoicePayments(long invoiceId)
    {
        return Database.Query(@"
SELECT 
    ip.Id,
    ip.PaymentDate AS [Ödeme Tarihi],
    ip.Amount AS [Ödenen Tutar],
    ip.PaymentMethod AS [Ödeme Yöntemi],
    COALESCE(ip.Note, '') AS [Açıklama],
    ip.CreatedAt AS [Kayıt Zamanı]
FROM InvoicePayments ip
WHERE ip.InvoiceId = @id
ORDER BY ip.PaymentDate DESC, ip.Id DESC;",
            ("@id", invoiceId)
        );
    }
}

