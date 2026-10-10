using System.Data;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

/// <summary>Cari ekstresini çok sayfalı PDF olarak üretir; WhatsApp ve e-posta ile göndermeye hazırlar.</summary>
public static class AccountStatementService
{
    private static readonly System.Globalization.CultureInfo Tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
    private static string Money(double v) => v.ToString("N2", Tr);

    private static string StatementFolder
    {
        get
        {
            string dir = Path.Combine(PdfArchiveService.ArchiveRoot, "CariEkstreler", DateTime.Now.ToString("yyyy-MM"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Cari bakiyesini (müşteri: borç-alacak, tedarikçi: alacak-borç) hesaplar.</summary>
    public static double GetBalance(Account acc)
    {
        double bal = 0;
        foreach (DataRow r in AccountService.GetAccountStatement(acc.Id).Rows)
        {
            double debit = Convert.ToDouble(r["Borç"]);
            double credit = Convert.ToDouble(r["Alacak"]);
            bal += acc.Type == "Müşteri" ? debit - credit : credit - debit;
        }
        return bal;
    }

    public static string CreatePdf(long accountId, DateTime? from = null)
    {
        var acc = AccountService.GetById(accountId) ?? throw new InvalidOperationException("Cari bulunamadı.");
        var raw = AccountService.GetAccountStatement(accountId);

        // Yürüyen bakiye: dönem öncesi hareketler "devir" satırında toplanır
        var rows = new List<(string Date, string Type, string Doc, double Debit, double Credit, double Balance, string Note)>();
        double running = 0;
        double carry = 0;
        foreach (DataRow r in raw.Rows)
        {
            double debit = Convert.ToDouble(r["Borç"]);
            double credit = Convert.ToDouble(r["Alacak"]);
            running += acc.Type == "Müşteri" ? debit - credit : credit - debit;

            string dateText = r["Tarih"]?.ToString() ?? "";
            if (from.HasValue && DateTime.TryParse(dateText, out var d) && d.Date < from.Value.Date)
            {
                carry = running;
                continue;
            }
            rows.Add((dateText.Length > 16 ? dateText[..16] : dateText, r["İşlem"]?.ToString() ?? "", r["Belge No"]?.ToString() ?? "",
                debit, credit, running, r["Açıklama"]?.ToString() ?? ""));
        }

        var doc = new PdfDocument();
        doc.Info.Title = $"Cari Ekstre - {acc.Name}";
        doc.Info.Author = ThermalReceiptService.Config.StoreHeader;

        var fTitle = new XFont("Segoe UI", 18, XFontStyleEx.Bold);
        var fH = new XFont("Segoe UI", 11, XFontStyleEx.Bold);
        var fB = new XFont("Segoe UI", 9, XFontStyleEx.Regular);
        var fBold = new XFont("Segoe UI", 9, XFontStyleEx.Bold);
        var fSmall = new XFont("Segoe UI", 8, XFontStyleEx.Regular);
        var ink = XBrushes.Black;
        var grey = new XSolidBrush(XColor.FromArgb(100, 116, 139));
        var accent = new XSolidBrush(XColor.FromArgb(15, 118, 110));
        var red = new XSolidBrush(XColor.FromArgb(185, 28, 28));
        var accentPen = new XPen(XColor.FromArgb(15, 118, 110), 1.2);
        var linePen = new XPen(XColor.FromArgb(226, 232, 240), 0.7);

        PdfPage page = null!;
        XGraphics gfx = null!;
        double L = 36, R = 0, W = 0, y = 0;
        double[] cols = Array.Empty<double>();
        int pageNo = 0;

        void NewPage()
        {
            gfx?.Dispose();
            page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            gfx = XGraphics.FromPdfPage(page);
            pageNo++;
            R = page.Width.Point - 36;
            W = R - L;
            y = 36;
            cols = new[] { L, L + W * 0.15, L + W * 0.27, L + W * 0.40, L + W * 0.54, L + W * 0.68, L + W * 0.82 };

            if (pageNo == 1)
            {
                gfx.DrawString(ThermalReceiptService.Config.StoreHeader, fTitle, accent, L, y + 16);
                gfx.DrawString("CARİ HESAP EKSTRESİ", fH, grey, R, y + 16, XStringFormats.TopRight);
                y += 30;
                gfx.DrawLine(accentPen, L, y, R, y);
                y += 12;
                gfx.DrawString(acc.Name, fH, ink, L, y + 11);
                gfx.DrawString($"Düzenlenme: {DateTime.Now:dd.MM.yyyy HH:mm}", fSmall, grey, R, y + 11, XStringFormats.TopRight);
                y += 16;
                string sub = $"{acc.Type}   Tel: {(string.IsNullOrWhiteSpace(acc.Phone) ? "-" : acc.Phone)}   Vergi No: {(string.IsNullOrWhiteSpace(acc.TaxNumber) ? "-" : acc.TaxNumber)}";
                gfx.DrawString(sub, fSmall, grey, L, y + 9);
                if (from.HasValue) gfx.DrawString($"Dönem: {from:dd.MM.yyyy} - {DateTime.Now:dd.MM.yyyy}", fSmall, grey, R, y + 9, XStringFormats.TopRight);
                y += 20;
            }
            else
            {
                gfx.DrawString($"{acc.Name} - Cari Ekstre (devam)", fSmall, grey, L, y + 8);
                y += 18;
            }

            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 253, 250)), L, y, W, 18);
            gfx.DrawString("Tarih", fBold, ink, cols[0] + 4, y + 13);
            gfx.DrawString("İşlem", fBold, ink, cols[1] + 4, y + 13);
            gfx.DrawString("Belge No", fBold, ink, cols[2] + 4, y + 13);
            gfx.DrawString("Borç", fBold, ink, cols[4] - 4, y + 13, XStringFormats.BottomRight);
            gfx.DrawString("Alacak", fBold, ink, cols[5] - 4, y + 13, XStringFormats.BottomRight);
            gfx.DrawString("Bakiye", fBold, ink, cols[6] - 4, y + 13, XStringFormats.BottomRight);
            gfx.DrawString("Açıklama", fBold, ink, cols[6] + 4, y + 13);
            y += 18;
        }

        NewPage();

        if (from.HasValue)
        {
            gfx.DrawString("Önceki dönemden devir", fB, grey, cols[0] + 4, y + 13);
            gfx.DrawString(Money(carry), fBold, ink, cols[6] - 4, y + 13, XStringFormats.BottomRight);
            y += 18;
            gfx.DrawLine(linePen, L, y, R, y);
        }

        double totalDebit = 0, totalCredit = 0;
        foreach (var row in rows)
        {
            if (y > page.Height.Point - 70) NewPage();

            string note = row.Note.Length > 22 ? row.Note[..22] + "…" : row.Note;
            string type = row.Type.Length > 12 ? row.Type[..12] : row.Type;
            string docNo = row.Doc.Length > 14 ? row.Doc[..14] : row.Doc;

            gfx.DrawString(row.Date, fB, ink, cols[0] + 4, y + 13);
            gfx.DrawString(type, fB, ink, cols[1] + 4, y + 13);
            gfx.DrawString(docNo, fB, ink, cols[2] + 4, y + 13);
            if (row.Debit > 0) gfx.DrawString(Money(row.Debit), fB, ink, cols[4] - 4, y + 13, XStringFormats.BottomRight);
            if (row.Credit > 0) gfx.DrawString(Money(row.Credit), fB, ink, cols[5] - 4, y + 13, XStringFormats.BottomRight);
            gfx.DrawString(Money(row.Balance), fBold, row.Balance > 0.005 ? red : accent, cols[6] - 4, y + 13, XStringFormats.BottomRight);
            gfx.DrawString(note, fSmall, grey, cols[6] + 4, y + 13);
            y += 18;
            gfx.DrawLine(linePen, L, y, R, y);
            totalDebit += row.Debit;
            totalCredit += row.Credit;
        }

        if (y > page.Height.Point - 110) NewPage();
        y += 14;
        gfx.DrawString("Toplam Borç", fB, grey, R - 230, y + 11);
        gfx.DrawString(Money(totalDebit) + " ₺", fBold, ink, R, y + 11, XStringFormats.TopRight);
        y += 16;
        gfx.DrawString("Toplam Alacak", fB, grey, R - 230, y + 11);
        gfx.DrawString(Money(totalCredit) + " ₺", fBold, ink, R, y + 11, XStringFormats.TopRight);
        y += 20;

        double finalBal = running;
        string label = acc.Type == "Müşteri"
            ? (finalBal > 0.005 ? "GÜNCEL KALAN BORCUNUZ" : finalBal < -0.005 ? "ALACAĞINIZ (FAZLA ÖDEME)" : "GÜNCEL BAKİYE")
            : (finalBal > 0.005 ? "TEDARİKÇİYE BORCUMUZ" : finalBal < -0.005 ? "TEDARİKÇİDEN ALACAĞIMIZ" : "GÜNCEL BAKİYE");
        gfx.DrawRoundedRectangle(new XPen(XColor.FromArgb(203, 213, 225), 0.8), new XSolidBrush(XColor.FromArgb(248, 250, 252)), R - 270, y, 270, 34, 8, 8);
        gfx.DrawString(label, fSmall, grey, R - 260, y + 13);
        gfx.DrawString(Money(Math.Abs(finalBal)) + " ₺", new XFont("Segoe UI", 13, XFontStyleEx.Bold),
            finalBal > 0.005 && acc.Type == "Müşteri" ? red : accent, R - 10, y + 12, XStringFormats.TopRight);

        string footer = ThermalReceiptService.Config.FooterNote ?? "";
        if (!string.IsNullOrWhiteSpace(footer))
            gfx.DrawString(footer, fSmall, grey, new XRect(L, page.Height.Point - 44, W, 12), XStringFormats.TopCenter);
        gfx.DrawString("Bu ekstre bilgi amaçlıdır, mali değeri yoktur.", fSmall, grey, new XRect(L, page.Height.Point - 30, W, 12), XStringFormats.TopCenter);
        gfx.Dispose();

        string safe = string.Join("_", acc.Name.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(StatementFolder, $"Ekstre_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        doc.Save(path);
        return path;
    }

    /// <summary>Ekstre PDF'ini panoya koyup müşterinin WhatsApp sohbetini açar.</summary>
    public static (bool Success, string Message) PrepareForWhatsApp(Account acc, string pdfPath, double balance)
    {
        if (string.IsNullOrWhiteSpace(acc.Phone) || !acc.Phone.Any(char.IsDigit))
            return (false, "Telefon numarası yok.");
        try
        {
            Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { pdfPath });
            string durum = acc.Type == "Müşteri" && balance > 0.005 ? $"Güncel bakiyeniz {Money(balance)} ₺'dir." : "Hesap ekstreniz ekte yer almaktadır.";
            string text = $"Sayın {acc.Name}, {DateTime.Now:dd.MM.yyyy} tarihli cari hesap ekstreniz ektedir. {durum} İyi günler dileriz.";
            return WhatsAppService.OpenWhatsAppUrl(acc.Phone, text)
                ? (true, "WhatsApp açıldı, PDF panoya kopyalandı.")
                : (false, "WhatsApp açılamadı.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Success, string Message) SendByEmail(Account acc, string pdfPath, double balance)
    {
        if (string.IsNullOrWhiteSpace(acc.Email)) return (false, "E-posta adresi yok.");
        string durum = acc.Type == "Müşteri" && balance > 0.005 ? $"Güncel bakiyeniz {Money(balance)} ₺'dir.\n\n" : "";
        string body = $"Sayın {acc.Name},\n\n{DateTime.Now:dd.MM.yyyy} tarihli cari hesap ekstreniz ektedir.\n{durum}İyi günler dileriz.\n\n{ThermalReceiptService.Config.StoreHeader}";
        return CloudBackupService.SendMail(acc.Email!, $"Cari Hesap Ekstresi - {DateTime.Now:dd.MM.yyyy}", body, pdfPath);
    }
}
