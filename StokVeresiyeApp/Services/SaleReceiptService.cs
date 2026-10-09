using System.Data;
using System.Diagnostics;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public class SaleReceiptLine
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "Adet";
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double DiscountPercent { get; set; }
    public double VatPercent { get; set; }
    public double Total { get; set; }
}

/// <summary>Satış sonrası fiş ve PDF belgesi için gereken tüm bilgiler.</summary>
public class SaleReceiptData
{
    public string DocNo { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public string PaymentMethod { get; set; } = "";
    public string? Note { get; set; }

    public long AccountId { get; set; }
    public string CustomerName { get; set; } = "Perakende Müşteri";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxOffice { get; set; }
    public string? TaxNumber { get; set; }
    public double? BalanceAfterSale { get; set; }   // güncel cari bakiye (borç)
    public double? CreditLimit { get; set; }

    public List<SaleReceiptLine> Lines { get; } = new();

    public bool HasAccount => AccountId > 0;
    public double GrossTotal => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public double NetTotal => Lines.Sum(l => l.Total);
    public double DiscountTotal => Math.Max(0, GrossTotal - Lines.Sum(l => l.Quantity * l.UnitPrice * (1 - l.DiscountPercent / 100.0)));

    /// <summary>Satış kaydedildikten sonra cari bilgilerini ve güncel bakiyeyi veritabanından okur.</summary>
    public void LoadAccountInfo()
    {
        if (AccountId <= 0) return;
        var dt = Database.Query(@"
SELECT a.Name, a.Phone, a.Address, a.TaxOffice, a.TaxNumber, COALESCE(a.BalanceLimit, 0) AS BalanceLimit, a.Type,
       COALESCE((SELECT SUM(CASE WHEN TransactionType = N'Satış' THEN Amount WHEN TransactionType = N'Tahsilat' THEN -Amount ELSE 0 END)
                 FROM AccountMovements m WHERE m.AccountId = a.Id), 0) AS Balance
FROM Accounts a WHERE a.Id = @id;", ("@id", AccountId));
        if (dt.Rows.Count == 0) return;
        var r = dt.Rows[0];
        CustomerName = r["Name"]?.ToString() ?? CustomerName;
        Phone = r["Phone"]?.ToString();
        Address = r["Address"]?.ToString();
        TaxOffice = r["TaxOffice"]?.ToString();
        TaxNumber = r["TaxNumber"]?.ToString();
        CreditLimit = Convert.ToDouble(r["BalanceLimit"]);
        BalanceAfterSale = Convert.ToDouble(r["Balance"]);
    }
}

public static class SaleReceiptService
{
    private static string PdfFolder
    {
        get
        {
            string dir = Path.Combine(PdfArchiveService.ArchiveRoot, "SatisBelgeleri", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    // ---- Fiş yazdırma ----

    /// <summary>Fişi seçilen yazıcıdan basar ve seçimi hatırlar.</summary>
    public static (bool Success, string Message) PrintReceipt(SaleReceiptData d, string? printerName)
    {
        if (!string.IsNullOrWhiteSpace(printerName) && ThermalReceiptService.Config.PrinterName != printerName)
        {
            ThermalReceiptService.Config.PrinterName = printerName;
            ThermalReceiptService.SaveConfig();
        }

        var items = d.Lines.Select(l => new ReceiptPrintItem { Name = l.Name, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Total = l.Total }).ToList();
        double remaining = d.BalanceAfterSale.HasValue && d.BalanceAfterSale.Value > 0 ? d.BalanceAfterSale.Value : 0;
        return ThermalReceiptService.PrintSale(d.HasAccount ? d.CustomerName : "", d.DocNo, d.PaymentMethod, items, d.GrossTotal, d.DiscountTotal, d.NetTotal, remaining);
    }

    // ---- PDF belge ----

    private static string Money(double v) => v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("tr-TR")) + " ₺";

    /// <summary>Müşteri bilgilerini, satılan ürünleri, ödeme ve güncel bakiyeyi içeren A4 PDF üretir.</summary>
    public static string CreatePdf(SaleReceiptData d)
    {
        var doc = new PdfDocument();
        doc.Info.Title = $"Satış Belgesi {d.DocNo}";
        doc.Info.Author = ThermalReceiptService.Config.StoreHeader;
        var page = doc.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var gfx = XGraphics.FromPdfPage(page);

        var fTitle = new XFont("Segoe UI", 20, XFontStyleEx.Bold);
        var fH = new XFont("Segoe UI", 11, XFontStyleEx.Bold);
        var fB = new XFont("Segoe UI", 10, XFontStyleEx.Regular);
        var fBold = new XFont("Segoe UI", 10, XFontStyleEx.Bold);
        var fSmall = new XFont("Segoe UI", 8.5, XFontStyleEx.Regular);
        var ink = XBrushes.Black;
        var grey = new XSolidBrush(XColor.FromArgb(100, 116, 139));
        var accent = new XSolidBrush(XColor.FromArgb(15, 118, 110));
        var accentPen = new XPen(XColor.FromArgb(15, 118, 110), 1.2);
        var linePen = new XPen(XColor.FromArgb(203, 213, 225), 0.8);

        double L = 40, R = page.Width.Point - 40, W = R - L;
        double y = 40;

        // Başlık
        gfx.DrawString(ThermalReceiptService.Config.StoreHeader, fTitle, accent, L, y + 18);
        gfx.DrawString("SATIŞ BELGESİ", fH, grey, R, y + 18, XStringFormats.TopRight);
        y += 30;
        if (!string.IsNullOrWhiteSpace(ThermalReceiptService.Config.StoreSubHeader))
        {
            gfx.DrawString(ThermalReceiptService.Config.StoreSubHeader, fSmall, grey, L, y + 8);
            y += 10;
        }
        y += 8;
        gfx.DrawLine(accentPen, L, y, R, y);
        y += 14;

        // Belge bilgileri (sol) ve cari bilgileri (sağ)
        gfx.DrawString("Belge Bilgileri", fH, ink, L, y + 10);
        gfx.DrawString(d.HasAccount ? "Cari Bilgileri" : "Müşteri", fH, ink, L + W / 2 + 10, y + 10);
        y += 18;
        double yLeft = y, yRight = y;

        void Left(string label, string value)
        {
            gfx.DrawString(label, fSmall, grey, L, yLeft + 9);
            gfx.DrawString(value, fB, ink, L + 85, yLeft + 10);
            yLeft += 15;
        }
        void Right(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            double x = L + W / 2 + 10;
            gfx.DrawString(label, fSmall, grey, x, yRight + 9);
            var rect = new XRect(x + 70, yRight, W / 2 - 80, 28);
            var tf = new PdfSharp.Drawing.Layout.XTextFormatter(gfx);
            tf.DrawString(value, fB, ink, rect);
            yRight += value.Length > 34 ? 28 : 15;
        }

        Left("Belge No", string.IsNullOrWhiteSpace(d.DocNo) ? "-" : d.DocNo);
        Left("Tarih", d.Date.ToString("dd.MM.yyyy HH:mm"));
        Left("Ödeme", d.PaymentMethod);
        if (!string.IsNullOrWhiteSpace(d.Note)) Left("Not", d.Note!.Length > 40 ? d.Note[..40] + "…" : d.Note);

        Right("Ünvan", d.CustomerName);
        if (d.HasAccount)
        {
            Right("Telefon", d.Phone ?? "");
            Right("Adres", d.Address ?? "");
            if (!string.IsNullOrWhiteSpace(d.TaxNumber))
                Right("Vergi", $"{d.TaxOffice} V.D. {d.TaxNumber}".Trim());
        }

        y = Math.Max(yLeft, yRight) + 12;

        // Kalem tablosu
        double[] cols = { L, L + W * 0.46, L + W * 0.58, L + W * 0.72, L + W * 0.82 };
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 253, 250)), L, y, W, 20);
        gfx.DrawString("Ürün", fBold, ink, cols[0] + 6, y + 14);
        gfx.DrawString("Miktar", fBold, ink, cols[2] - 6, y + 14, XStringFormats.BottomRight);
        gfx.DrawString("Birim Fiyat", fBold, ink, cols[3] - 6, y + 14, XStringFormats.BottomRight);
        gfx.DrawString("İsk. %", fBold, ink, cols[4] - 6, y + 14, XStringFormats.BottomRight);
        gfx.DrawString("Tutar", fBold, ink, R - 6, y + 14, XStringFormats.BottomRight);
        y += 20;

        foreach (var l in d.Lines)
        {
            if (y > page.Height.Point - 190) break; // tek sayfalık belge; çok uzun sepetlerde devamı sığmaz
            string name = l.Name.Length > 46 ? l.Name[..46] + "…" : l.Name;
            gfx.DrawString(name, fB, ink, cols[0] + 6, y + 14);
            gfx.DrawString($"{l.Quantity:0.##} {l.Unit}", fB, ink, cols[2] - 6, y + 14, XStringFormats.BottomRight);
            gfx.DrawString(Money(l.UnitPrice), fB, ink, cols[3] - 6, y + 14, XStringFormats.BottomRight);
            gfx.DrawString(l.DiscountPercent > 0 ? $"%{l.DiscountPercent:0.##}" : "-", fB, ink, cols[4] - 6, y + 14, XStringFormats.BottomRight);
            gfx.DrawString(Money(l.Total), fBold, ink, R - 6, y + 14, XStringFormats.BottomRight);
            y += 20;
            gfx.DrawLine(linePen, L, y, R, y);
        }

        y += 10;
        // Toplamlar
        double tx = R - 230;
        if (d.DiscountTotal > 0.005)
        {
            gfx.DrawString("Ara Toplam", fB, grey, tx, y + 11);
            gfx.DrawString(Money(d.GrossTotal), fB, ink, R, y + 11, XStringFormats.TopRight);
            y += 16;
            gfx.DrawString("İskonto", fB, grey, tx, y + 11);
            gfx.DrawString("-" + Money(d.DiscountTotal), fB, ink, R, y + 11, XStringFormats.TopRight);
            y += 16;
        }
        gfx.DrawString("GENEL TOPLAM (KDV dahil)", fBold, accent, tx - 30, y + 12);
        gfx.DrawString(Money(d.NetTotal), new XFont("Segoe UI", 13, XFontStyleEx.Bold), accent, R, y + 12, XStringFormats.TopRight);
        y += 32;

        // Cari özet kutusu
        if (d.HasAccount && d.BalanceAfterSale.HasValue)
        {
            double boxH = d.CreditLimit is > 0 ? 58 : 44;
            gfx.DrawRoundedRectangle(new XPen(XColor.FromArgb(203, 213, 225), 0.8), new XSolidBrush(XColor.FromArgb(248, 250, 252)), L, y, W, boxH, 8, 8);
            gfx.DrawString("Cari Hesap Durumu", fH, ink, L + 12, y + 18);
            double bal = d.BalanceAfterSale.Value;
            string balText = bal > 0.005 ? $"Güncel kalan borcunuz: {Money(bal)}" : "Güncel bakiyeniz: 0,00 ₺ (borcunuz yoktur)";
            gfx.DrawString(balText, fBold, bal > 0.005 ? new XSolidBrush(XColor.FromArgb(185, 28, 28)) : accent, L + 12, y + 35);
            if (d.CreditLimit is > 0)
                gfx.DrawString($"Kredi limitiniz: {Money(d.CreditLimit.Value)}   Kullanılabilir: {Money(Math.Max(0, d.CreditLimit.Value - Math.Max(0, bal)))}", fSmall, grey, L + 12, y + 50);
            y += boxH + 14;
        }

        // Alt not
        string footer = ThermalReceiptService.Config.FooterNote ?? "";
        var tf2 = new PdfSharp.Drawing.Layout.XTextFormatter(gfx) { Alignment = PdfSharp.Drawing.Layout.XParagraphAlignment.Center };
        tf2.DrawString(footer, fSmall, grey, new XRect(L, page.Height.Point - 70, W, 40));
        gfx.DrawString($"Belge {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde oluşturulmuştur. Mali değeri yoktur, bilgi amaçlıdır.", fSmall, grey, new XRect(L, page.Height.Point - 36, W, 12), XStringFormats.TopCenter);

        string safeDoc = string.Join("_", (string.IsNullOrWhiteSpace(d.DocNo) ? "SATIS" : d.DocNo).Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(PdfFolder, $"{safeDoc}_{DateTime.Now:HHmmss}.pdf");
        doc.Save(path);
        return path;
    }

    // ---- WhatsApp ----

    /// <summary>
    /// WhatsApp, dışarıdan dosyayı doğrudan göndermeye izin vermez (bunun için WhatsApp Business API gerekir).
    /// Bu yüzden müşterinin sohbeti açılır ve PDF panoya kopyalanır: kullanıcı mesaj kutusuna Ctrl+V yapıp gönderir.
    /// </summary>
    public static (bool Success, string Message) PreparePdfForWhatsApp(string pdfPath, string phone, SaleReceiptData d)
    {
        try
        {
            var files = new System.Collections.Specialized.StringCollection { pdfPath };
            Clipboard.SetFileDropList(files);

            string text = $"Sayın {d.CustomerName}, {d.Date:dd.MM.yyyy} tarihli {d.NetTotal:N2} ₺ tutarındaki alışverişinizin belgesi ekte yer almaktadır. Bizi tercih ettiğiniz için teşekkür ederiz.";
            bool opened = WhatsAppService.OpenWhatsAppUrl(phone, text);
            if (!opened) return (false, "WhatsApp açılamadı. PDF panoya kopyalandı; WhatsApp'ı elle açıp Ctrl+V ile yapıştırabilirsiniz.");

            return (true, "WhatsApp sohbeti açıldı ve PDF panoya kopyalandı.\nSohbetin mesaj kutusuna tıklayıp Ctrl+V ile yapıştırın, sonra Gönder'e basın.");
        }
        catch (Exception ex)
        {
            return (false, "PDF WhatsApp için hazırlanamadı: " + ex.Message);
        }
    }

    public static void ShowInExplorer(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }
}
