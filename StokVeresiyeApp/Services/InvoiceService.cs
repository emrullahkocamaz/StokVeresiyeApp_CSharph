using System.Data;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Helpers;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class InvoiceService
{
    private static readonly string InvoicesArchiveDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp",
        "Invoices"
    );

    /// <summary>
    /// Faturayı, kalemlerini, stok ve cari hareketlerini kaydeder.
    /// </summary>
    public static long SaveInvoice(Invoice invoice, bool updateStock = true, bool updateAccountBalance = true)
    {
        // PDF dosyasını hem arşiv klasörüne kopyala hem de veritabanına gömmek için oku
        string? archivedPdfPath = null;
        byte[]? pdfBytes = invoice.PdfData;

        if (!string.IsNullOrWhiteSpace(invoice.PdfPath) && File.Exists(invoice.PdfPath))
        {
            try
            {
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    pdfBytes = File.ReadAllBytes(invoice.PdfPath);
                    invoice.PdfData = pdfBytes;
                }

                string ext = Path.GetExtension(invoice.PdfPath);
                string cleanNo = string.Join("_", invoice.InvoiceNumber.Split(Path.GetInvalidFileNameChars()));
                string targetFile = Path.Combine(InvoicesArchiveDir, $"{cleanNo}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
                File.Copy(invoice.PdfPath, targetFile, true);
                archivedPdfPath = targetFile;
            }
            catch { }
        }

        // 1. Invoices tablosuna ekle (PdfData VARBINARY olarak doğrudan veritabanına gömülür)
        var invoiceIdObj = Database.ExecuteScalar(@"
INSERT INTO Invoices (InvoiceNumber, InvoiceDate, InvoiceType, AccountId, WarehouseId, SubTotal, VatTotal, GrandTotal, PdfPath, PdfData, Note, CustomizationId, Scenario, InvoiceKind, OrderNumber, OrderDate, RelatedStore, CargoId, ReferenceNo, IssueTime, CreatedAt)
VALUES (@num, @date, @type, @accId, @wId, @sub, @vat, @grand, @pdf, @pdfData, @note, @cust, @scen, @kind, @ordNo, @ordDate, @store, @cargo, @ref, @time, @created);
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
            ("@pdfData", (object?)pdfBytes ?? DBNull.Value),
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
            ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        );

        long invoiceId = Convert.ToInt64(invoiceIdObj);
        invoice.Id = invoiceId;

        // 2. Kalemleri ekle ve stok hareketlerini oluştur
        foreach (var item in invoice.Items)
        {
            // Eğer kullanıcı bu kalemi faturadan silmeyi / hariç tutmayı seçtiyse kaydetme
            if (string.Equals(item.ActionDecision, "Faturadan Sil", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ActionDecision, "Sil", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            item.InvoiceId = invoiceId;

            // Eğer sistemde kayıtlı bir ürün ID'si yoksa otomatik ürün kartı oluştur
            if (!item.ProductId.HasValue || item.ProductId.Value <= 0)
            {
                string prodCode = !string.IsNullOrWhiteSpace(item.ItemCode) 
                    ? item.ItemCode.Trim()
                    : "URN-" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(100, 999);
                if (prodCode.Length > 140) prodCode = prodCode.Substring(0, 140);

                string prodName = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName.Trim() : "Ürün " + prodCode;
                if (prodName.Length > 490) prodName = prodName.Substring(0, 490);

                double purPrice = item.UnitPrice > 0 ? item.UnitPrice : 0;
                // Yeni satış fiyatı belirlenmişse onu kullan, yoksa %30 kâr marjı
                double salePrice = (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0) ? item.NewSalePrice.Value : purPrice * 1.30;
                double vat = item.VatPercent > 0 ? item.VatPercent : 20;

                var existing = FindProductByNameOrCode(prodName, item.ItemCode);
                if (existing != null)
                {
                    item.ProductId = existing.Id;
                    if (string.IsNullOrWhiteSpace(existing.Barcode) && !string.IsNullOrWhiteSpace(item.Barcode))
                    {
                        Database.Execute("UPDATE Products SET Barcode = @b WHERE Id = @id;", ("@b", item.Barcode), ("@id", existing.Id));
                    }
                }
                else
                {
                    var newPid = Database.ExecuteScalar(@"
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
                        ("@vat", vat)
                    );
                    item.ProductId = Convert.ToInt64(newPid);
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                // Mevcut ürünün barkodu boşsa faturadaki barkodla güncelle
                Database.Execute("UPDATE Products SET Barcode = @b WHERE Id = @id AND (Barcode IS NULL OR Barcode = '');",
                    ("@b", item.Barcode),
                    ("@id", item.ProductId.Value)
                );
            }

            string safeItemName = !string.IsNullOrWhiteSpace(item.ItemName) ? item.ItemName.Trim() : "Ürün";
            if (safeItemName.Length > 490) safeItemName = safeItemName.Substring(0, 490);

            Database.Execute(@"
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
                ("@total", item.LineTotal)
            );

            // Stok hareketi oluştur ve kullanıcının kararına göre ürün fiyatlarını güncelle
            if (updateStock && item.ProductId.HasValue && item.ProductId.Value > 0)
            {
                // Eski stok ve fiyat bilgilerini oku
                var existingProd = ProductService.GetById(item.ProductId.Value);
                double oldStock = existingProd != null ? ProductService.GetStock(item.ProductId.Value) : 0;
                double oldBuy = existingProd?.PurchasePrice ?? 0;
                double oldSale = existingProd?.SalePrice ?? 0;
                double finalSalePrice = oldSale;

                Database.Execute(@"
INSERT INTO StockMovements (MovementDate, ProductId, MovementType, Quantity, UnitPrice, DocumentNo, AccountId, WarehouseId, Note)
VALUES (@date, @pid, 'Gelen', @qty, @price, @doc, @accId, @wId, @note);",
                    ("@date", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                    ("@pid", item.ProductId.Value),
                    ("@qty", item.Quantity),
                    ("@price", item.UnitPrice),
                    ("@doc", invoice.InvoiceNumber),
                    ("@accId", invoice.AccountId),
                    ("@wId", invoice.WarehouseId),
                    ("@note", $"Fatura Girişi: {invoice.InvoiceNumber}")
                );

                // Kullanıcının kararı:
                // 1) "Satış Fiyatına Zam Yap": Hem Alış Fiyatı hem de Yeni Satış Fiyatı güncellenir
                if (string.Equals(item.ActionDecision, "Satış Fiyatına Zam Yap", StringComparison.OrdinalIgnoreCase) ||
                    (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0 && item.NewSalePrice.Value != oldSale))
                {
                    finalSalePrice = (item.NewSalePrice.HasValue && item.NewSalePrice.Value > 0) ? item.NewSalePrice.Value : oldSale;
                    Database.Execute("UPDATE Products SET PurchasePrice = @p, SalePrice = @sp, WholesalePrice = @wp, SpecialPrice = @xp WHERE Id = @id;",
                        ("@p", item.UnitPrice > 0 ? item.UnitPrice : oldBuy),
                        ("@sp", finalSalePrice),
                        ("@wp", (item.UnitPrice > 0 ? item.UnitPrice : oldBuy) * 1.15),
                        ("@xp", (item.UnitPrice > 0 ? item.UnitPrice : oldBuy) * 1.20),
                        ("@id", item.ProductId.Value)
                    );
                }
                // 2) "Alış Fiyatını Güncelle": Sadece alış fiyatı güncellenir, satış fiyatı korunur
                else if (string.Equals(item.ActionDecision, "Alış Fiyatını Güncelle", StringComparison.OrdinalIgnoreCase) ||
                         string.IsNullOrWhiteSpace(item.ActionDecision)) // Varsayılan: alış fiyatını güncelle
                {
                    if (item.UnitPrice > 0)
                    {
                        Database.Execute("UPDATE Products SET PurchasePrice = @p WHERE Id = @id;",
                            ("@p", item.UnitPrice),
                            ("@id", item.ProductId.Value)
                        );
                    }
                }
                // 3) "Olduğu Gibi Al": Ürün kartının fiyatlarına dokunulmaz (eski alış ve satış korunur)

                // Fiyat ve Stok Geçmişi (Tarihçe) Kaydet
                double newStock = oldStock + item.Quantity;
                string supplierName = "";
                if (invoice.AccountId > 0)
                {
                    try
                    {
                        var acc = AccountService.GetById(invoice.AccountId);
                        supplierName = acc?.Name ?? "";
                    }
                    catch { }
                }

                ProductService.RecordPriceHistory(
                    productId: item.ProductId.Value,
                    docNo: invoice.InvoiceNumber,
                    supplier: supplierName,
                    oldStock: oldStock,
                    addedStock: item.Quantity,
                    newStock: newStock,
                    oldBuy: oldBuy,
                    newBuy: item.UnitPrice > 0 ? item.UnitPrice : oldBuy,
                    oldSale: oldSale,
                    newSale: finalSalePrice,
                    note: $"Fatura Girişi: {invoice.InvoiceNumber} [{item.ActionDecision ?? "Normal"}]"
                );
            }
        }

        // 3. Cari hareketini oluştur (Alış faturası = Tedarikçiye borçlanma / Cari Hareketi)
        if (updateAccountBalance && invoice.AccountId > 0 && invoice.GrandTotal > 0)
        {
            Database.Execute(@"
INSERT INTO AccountMovements (MovementDate, AccountId, TransactionType, DocumentNo, Amount, Method, CashBank, Note)
VALUES (@date, @accId, 'Alış', @doc, @amt, 'Fatura', 'Fatura Cari Kaydı', @note);",
                ("@date", invoice.InvoiceDate.ToString("yyyy-MM-dd")),
                ("@accId", invoice.AccountId),
                ("@doc", invoice.InvoiceNumber),
                ("@amt", invoice.GrandTotal),
                ("@note", $"E-Fatura / Alış Faturası Girişi (Toplam: {invoice.GrandTotal:N2} ₺)")
            );
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
            var dt = Database.Query("SELECT InvoiceNumber, GrandTotal, AccountId, InvoiceType FROM Invoices WHERE Id = @id;", ("@id", invoiceId));
            if (dt.Rows.Count == 0)
                return (false, "Fatura bulunamadı.");

            string invNo = dt.Rows[0]["InvoiceNumber"]?.ToString() ?? "";
            double total = Convert.ToDouble(dt.Rows[0]["GrandTotal"] == DBNull.Value ? 0 : dt.Rows[0]["GrandTotal"]);
            long accId = Convert.ToInt64(dt.Rows[0]["AccountId"] == DBNull.Value ? 0 : dt.Rows[0]["AccountId"]);
            string invType = dt.Rows[0]["InvoiceType"]?.ToString() ?? "Fatura";

            int stockMovesDeleted = 0;
            int accountMovesDeleted = 0;

            if (rollbackMovements && !string.IsNullOrWhiteSpace(invNo))
            {
                // 1. Faturanın soktuğu veya çıkardığı tüm stok hareketlerini geri al (sil)
                var dtSm = Database.Query("SELECT COUNT(*) AS Cnt FROM StockMovements WHERE DocumentNo = @doc;", ("@doc", invNo));
                stockMovesDeleted = dtSm.Rows.Count > 0 ? Convert.ToInt32(dtSm.Rows[0]["Cnt"]) : 0;
                Database.Execute("DELETE FROM StockMovements WHERE DocumentNo = @doc;", ("@doc", invNo));

                // 2. Faturanın cariye işlediği borç/alacak hareketlerini geri al (sil)
                var dtAm = Database.Query("SELECT COUNT(*) AS Cnt FROM AccountMovements WHERE DocumentNo = @doc;", ("@doc", invNo));
                accountMovesDeleted = dtAm.Rows.Count > 0 ? Convert.ToInt32(dtAm.Rows[0]["Cnt"]) : 0;
                Database.Execute("DELETE FROM AccountMovements WHERE DocumentNo = @doc;", ("@doc", invNo));
            }

            // 3. Fatura ödeme ve kalemlerini sil
            Database.Execute("DELETE FROM InvoicePayments WHERE InvoiceId = @id;", ("@id", invoiceId));
            Database.Execute("DELETE FROM InvoiceItems WHERE InvoiceId = @id;", ("@id", invoiceId));

            // 4. Faturanın kendisini sil
            Database.Execute("DELETE FROM Invoices WHERE Id = @id;", ("@id", invoiceId));

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
    /// Diskte olup veritabanında PdfData'sı henüz boş olan eski faturaları otomatik olarak veritabanına gömer.
    /// </summary>
    public static void MigrateExistingPdfsToDatabase()
    {
        try
        {
            var dt = Database.Query("SELECT Id, InvoiceNumber, PdfPath FROM Invoices WHERE PdfData IS NULL AND PdfPath IS NOT NULL AND PdfPath != '';");
            foreach (DataRow r in dt.Rows)
            {
                long id = Convert.ToInt64(r["Id"]);
                string? path = r["PdfPath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        Database.Execute("UPDATE Invoices SET PdfData = @data WHERE Id = @id;", ("@data", bytes), ("@id", id));
                    }
                    catch { }
                }
            }
        }
        catch { }
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

