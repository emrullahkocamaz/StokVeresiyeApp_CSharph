using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace StokVeresiyeApp.Helpers;

public static class RibbonIconFactory
{
    public static Image CreateIcon(string type, int size = 32)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        float s = size / 32f; // Ölçekleme katsayısı

        switch (type.ToLowerInvariant())
        {
            case "dashboard":
                DrawDashboard(g, size, s);
                break;
            case "quicksale":
                DrawQuickSale(g, size, s);
                break;
            case "quickbuy":
                DrawQuickBuy(g, size, s);
                break;
            case "parked":
                DrawParked(g, size, s);
                break;
            case "collect":
                DrawCollect(g, size, s);
                break;
            case "debt":
                DrawDebt(g, size, s);
                break;
            case "zreport":
                DrawZReport(g, size, s);
                break;
            case "products":
                DrawProducts(g, size, s);
                break;
            case "newproduct":
                DrawNewProduct(g, size, s);
                break;
            case "fastentry":
                DrawFastEntry(g, size, s);
                break;
            case "pricehistory":
                DrawPriceHistory(g, size, s);
                break;
            case "bulkdelete":
                DrawBulkDelete(g, size, s);
                break;
            case "barcode":
                DrawBarcode(g, size, s);
                break;
            case "warehouse":
                DrawWarehouse(g, size, s);
                break;
            case "stockmovement":
                DrawStockMovement(g, size, s);
                break;
            case "stockcount":
                DrawStockCount(g, size, s);
                break;
            case "accounts":
                DrawAccounts(g, size, s);
                break;
            case "newaccount":
                DrawNewAccount(g, size, s);
                break;
            case "statement":
                DrawStatement(g, size, s);
                break;
            case "due":
                DrawDue(g, size, s);
                break;
            case "invoices":
                DrawInvoices(g, size, s);
                break;
            case "invoiceentry":
                DrawInvoiceEntry(g, size, s);
                break;
            case "cash":
                DrawCash(g, size, s);
                break;
            case "notification":
                DrawNotification(g, size, s);
                break;
            case "mobilescanner":
                DrawMobileScanner(g, size, s);
                break;
            case "auditlogs":
                DrawAuditLogs(g, size, s);
                break;
            case "users":
                DrawUsers(g, size, s);
                break;
            case "license":
                DrawLicense(g, size, s);
                break;
            case "settings":
                DrawSettings(g, size, s);
                break;
            case "theme":
                DrawTheme(g, size, s);
                break;
            case "logout":
                DrawLogout(g, size, s);
                break;
            case "excel":
                DrawExcel(g, size, s);
                break;
            case "quickactions":
                DrawQuickActions(g, size, s);
                break;
            case "layout":
                DrawLayout(g, size, s);
                break;
            case "bilensis_logo":
                DrawBilensisLogo(g, size, s);
                break;
            case "stockinout":
                DrawStockInOut(g, size, s);
                break;
            case "refresh":
                DrawRefresh(g, size, s);
                break;
            case "whatsapp":
                DrawWhatsApp(g, size, s);
                break;
            case "batchinvoices":
                DrawBatchInvoices(g, size, s);
                break;
            default:
                DrawGeneric(g, size, s);
                break;
        }

        return bmp;
    }

    private static void DrawDashboard(Graphics g, int sz, float s)
    {
        // 3 Renkli Çubuk Grafik & Trend
        using var b1 = new SolidBrush(Color.FromArgb(14, 165, 233));
        using var b2 = new SolidBrush(Color.FromArgb(99, 102, 241));
        using var b3 = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillRectangle(b1, 4 * s, 16 * s, 6 * s, 12 * s);
        g.FillRectangle(b2, 13 * s, 10 * s, 6 * s, 18 * s);
        g.FillRectangle(b3, 22 * s, 4 * s, 6 * s, 24 * s);

        // Trend Oku
        using var pen = new Pen(Color.FromArgb(245, 158, 11), 2.5f * s);
        pen.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(pen, 5 * s, 14 * s, 26 * s, 3 * s);
    }

    private static void DrawQuickSale(Graphics g, int sz, float s)
    {
        // Alışveriş Sepeti
        using var pen = new Pen(Color.FromArgb(16, 185, 129), 2f * s);
        g.DrawLines(pen, new PointF[] {
            new(3 * s, 6 * s), new(7 * s, 6 * s), new(11 * s, 20 * s), new(25 * s, 20 * s), new(28 * s, 10 * s), new(8 * s, 10 * s)
        });
        using var bWheel = new SolidBrush(Color.FromArgb(15, 23, 42));
        g.FillEllipse(bWheel, 11 * s, 22 * s, 4 * s, 4 * s);
        g.FillEllipse(bWheel, 23 * s, 22 * s, 4 * s, 4 * s);

        // Şimşek Rozeti
        using var bBolt = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillPolygon(bBolt, new PointF[] {
            new(18 * s, 2 * s), new(14 * s, 11 * s), new(19 * s, 11 * s), new(15 * s, 19 * s), new(23 * s, 9 * s), new(18 * s, 9 * s)
        });
    }

    private static void DrawQuickBuy(Graphics g, int sz, float s)
    {
        // Kutu
        using var bBox = new SolidBrush(Color.FromArgb(59, 130, 246));
        g.FillRectangle(bBox, 5 * s, 10 * s, 22 * s, 18 * s);
        using var pen = new Pen(Color.FromArgb(30, 64, 175), 1.5f * s);
        g.DrawRectangle(pen, 5 * s, 10 * s, 22 * s, 18 * s);

        // İçe Doğru Ok (Giriş)
        using var penArrow = new Pen(Color.White, 3f * s);
        penArrow.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(penArrow, 16 * s, 2 * s, 16 * s, 18 * s);
    }

    private static void DrawParked(Graphics g, int sz, float s)
    {
        // Askıda Klasör
        using var bFld = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillRectangle(bFld, 4 * s, 8 * s, 24 * s, 18 * s);
        g.FillPolygon(bFld, new PointF[] { new(4 * s, 8 * s), new(12 * s, 8 * s), new(15 * s, 5 * s), new(4 * s, 5 * s) });

        // Saat/Bekleme Rozeti
        using var bClock = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillEllipse(bClock, 17 * s, 14 * s, 12 * s, 12 * s);
        using var penWhite = new Pen(Color.White, 1.5f * s);
        g.DrawLine(penWhite, 23 * s, 17 * s, 23 * s, 20 * s);
        g.DrawLine(penWhite, 23 * s, 20 * s, 26 * s, 20 * s);
    }

    private static void DrawCollect(Graphics g, int sz, float s)
    {
        // Para Destesi / Yeşil Nakit
        using var bCash = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillRectangle(bCash, 4 * s, 8 * s, 24 * s, 15 * s);
        using var penBorder = new Pen(Color.FromArgb(5, 150, 105), 1.5f * s);
        g.DrawRectangle(penBorder, 4 * s, 8 * s, 24 * s, 15 * s);

        using var bCircle = new SolidBrush(Color.FromArgb(5, 150, 105));
        g.FillEllipse(bCircle, 13 * s, 12 * s, 7 * s, 7 * s);

        // Artı Rozeti
        using var bPlus = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillEllipse(bPlus, 18 * s, 16 * s, 12 * s, 12 * s);
        using var penP = new Pen(Color.White, 2f * s);
        g.DrawLine(penP, 24 * s, 19 * s, 24 * s, 25 * s);
        g.DrawLine(penP, 21 * s, 22 * s, 27 * s, 22 * s);
    }

    private static void DrawDebt(Graphics g, int sz, float s)
    {
        // Kırmızı Cüzdan
        using var bW = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillRectangle(bW, 4 * s, 7 * s, 24 * s, 17 * s);
        using var penW = new Pen(Color.FromArgb(185, 28, 28), 1.5f * s);
        g.DrawRectangle(penW, 4 * s, 7 * s, 24 * s, 17 * s);

        // Eksi Rozeti
        using var bMin = new SolidBrush(Color.FromArgb(220, 38, 38));
        g.FillEllipse(bMin, 18 * s, 16 * s, 12 * s, 12 * s);
        using var penM = new Pen(Color.White, 2.5f * s);
        g.DrawLine(penM, 21 * s, 22 * s, 27 * s, 22 * s);
    }

    private static void DrawZReport(Graphics g, int sz, float s)
    {
        // Kasa Raporu Belgesi
        using var bPaper = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.FillRectangle(bPaper, 6 * s, 4 * s, 20 * s, 24 * s);
        using var penBorder = new Pen(Color.FromArgb(100, 116, 139), 1.5f * s);
        g.DrawRectangle(penBorder, 6 * s, 4 * s, 20 * s, 24 * s);

        // Kilit Rozeti (Z Raporu)
        using var bGold = new SolidBrush(Color.FromArgb(217, 119, 6));
        g.FillEllipse(bGold, 16 * s, 14 * s, 13 * s, 13 * s);
        using var f = new Font("Segoe UI", 8 * s, FontStyle.Bold);
        using var bText = new SolidBrush(Color.White);
        g.DrawString("Z", f, bText, 19 * s, 15 * s);
    }

    private static void DrawProducts(Graphics g, int sz, float s)
    {
        // 3D Koli
        using var bTop = new SolidBrush(Color.FromArgb(217, 119, 6));
        using var bLeft = new SolidBrush(Color.FromArgb(180, 83, 9));
        using var bRight = new SolidBrush(Color.FromArgb(245, 158, 11));

        g.FillPolygon(bTop, new PointF[] { new(16 * s, 4 * s), new(28 * s, 10 * s), new(16 * s, 16 * s), new(4 * s, 10 * s) });
        g.FillPolygon(bLeft, new PointF[] { new(4 * s, 10 * s), new(16 * s, 16 * s), new(16 * s, 28 * s), new(4 * s, 22 * s) });
        g.FillPolygon(bRight, new PointF[] { new(16 * s, 16 * s), new(28 * s, 10 * s), new(28 * s, 22 * s), new(16 * s, 28 * s) });
    }

    private static void DrawNewProduct(Graphics g, int sz, float s)
    {
        DrawProducts(g, sz, s);
        // Artı Rozeti
        using var bGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillEllipse(bGreen, 17 * s, 17 * s, 13 * s, 13 * s);
        using var penW = new Pen(Color.White, 2f * s);
        g.DrawLine(penW, 23.5f * s, 20 * s, 23.5f * s, 27 * s);
        g.DrawLine(penW, 20 * s, 23.5f * s, 27 * s, 23.5f * s);
    }

    private static void DrawFastEntry(Graphics g, int sz, float s)
    {
        // Barkod Çizgileri
        using var bBar = new SolidBrush(Color.FromArgb(30, 41, 59));
        g.FillRectangle(bBar, 4 * s, 8 * s, 3 * s, 16 * s);
        g.FillRectangle(bBar, 9 * s, 8 * s, 2 * s, 16 * s);
        g.FillRectangle(bBar, 13 * s, 8 * s, 4 * s, 16 * s);
        g.FillRectangle(bBar, 19 * s, 8 * s, 2 * s, 16 * s);

        // Şimşek
        using var bBolt = new SolidBrush(Color.FromArgb(234, 88, 12));
        g.FillPolygon(bBolt, new PointF[] {
            new(23 * s, 4 * s), new(18 * s, 15 * s), new(24 * s, 15 * s), new(19 * s, 28 * s), new(28 * s, 13 * s), new(23 * s, 13 * s)
        });
    }

    private static void DrawPriceHistory(Graphics g, int sz, float s)
    {
        // Parşömen / Tarihçe
        using var bScroll = new SolidBrush(Color.FromArgb(254, 243, 199));
        g.FillRectangle(bScroll, 6 * s, 4 * s, 18 * s, 24 * s);
        using var penBorder = new Pen(Color.FromArgb(217, 119, 6), 1.5f * s);
        g.DrawRectangle(penBorder, 6 * s, 4 * s, 18 * s, 24 * s);

        // Satırlar
        using var penLine = new Pen(Color.FromArgb(202, 138, 4), 1.5f * s);
        g.DrawLine(penLine, 9 * s, 9 * s, 20 * s, 9 * s);
        g.DrawLine(penLine, 9 * s, 14 * s, 18 * s, 14 * s);
        g.DrawLine(penLine, 9 * s, 19 * s, 16 * s, 19 * s);

        // Saat İkonu
        using var bClock = new SolidBrush(Color.FromArgb(14, 165, 233));
        g.FillEllipse(bClock, 17 * s, 16 * s, 13 * s, 13 * s);
        using var penW = new Pen(Color.White, 1.5f * s);
        g.DrawLine(penW, 23.5f * s, 19 * s, 23.5f * s, 22.5f * s);
        g.DrawLine(penW, 23.5f * s, 22.5f * s, 26 * s, 22.5f * s);
    }

    private static void DrawBulkDelete(Graphics g, int sz, float s)
    {
        // Kırmızı Çöp Kutusu
        using var bTrash = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillRectangle(bTrash, 8 * s, 11 * s, 16 * s, 16 * s);
        g.FillRectangle(bTrash, 6 * s, 8 * s, 20 * s, 3 * s);
        g.FillRectangle(bTrash, 12 * s, 5 * s, 8 * s, 3 * s);

        // Çoklu Yaprak Rozeti
        using var penW = new Pen(Color.White, 2f * s);
        g.DrawLine(penW, 12 * s, 14 * s, 12 * s, 24 * s);
        g.DrawLine(penW, 16 * s, 14 * s, 16 * s, 24 * s);
        g.DrawLine(penW, 20 * s, 14 * s, 20 * s, 24 * s);
    }

    private static void DrawBarcode(Graphics g, int sz, float s)
    {
        // Fiyat Etiketi
        using var bTag = new SolidBrush(Color.FromArgb(139, 92, 246));
        g.FillPolygon(bTag, new PointF[] {
            new(5 * s, 14 * s), new(14 * s, 5 * s), new(27 * s, 5 * s), new(27 * s, 18 * s), new(18 * s, 27 * s)
        });
        using var bHole = new SolidBrush(Color.White);
        g.FillEllipse(bHole, 22 * s, 9 * s, 3.5f * s, 3.5f * s);

        // Barkod Çizgileri
        using var penB = new Pen(Color.White, 1.2f * s);
        g.DrawLine(penB, 11 * s, 15 * s, 15 * s, 11 * s);
        g.DrawLine(penB, 14 * s, 18 * s, 18 * s, 14 * s);
        g.DrawLine(penB, 17 * s, 21 * s, 21 * s, 17 * s);
    }

    private static void DrawWarehouse(Graphics g, int sz, float s)
    {
        // Depo Binası
        using var bBuilding = new SolidBrush(Color.FromArgb(79, 70, 229));
        g.FillRectangle(bBuilding, 4 * s, 10 * s, 24 * s, 18 * s);
        using var bRoof = new SolidBrush(Color.FromArgb(67, 56, 202));
        g.FillPolygon(bRoof, new PointF[] { new(2 * s, 10 * s), new(16 * s, 3 * s), new(30 * s, 10 * s) });

        // Garaj Kapısı
        using var bDoor = new SolidBrush(Color.FromArgb(224, 231, 255));
        g.FillRectangle(bDoor, 11 * s, 16 * s, 10 * s, 12 * s);
        using var penLine = new Pen(Color.FromArgb(99, 102, 241), 1f * s);
        g.DrawLine(penLine, 11 * s, 20 * s, 21 * s, 20 * s);
        g.DrawLine(penLine, 11 * s, 24 * s, 21 * s, 24 * s);
    }

    private static void DrawStockMovement(Graphics g, int sz, float s)
    {
        // Çift Yönlü Dairesel Oklar
        using var penGreen = new Pen(Color.FromArgb(16, 185, 129), 3f * s);
        penGreen.EndCap = LineCap.ArrowAnchor;
        g.DrawArc(penGreen, 5 * s, 5 * s, 22 * s, 22 * s, -45, 150);

        using var penOrange = new Pen(Color.FromArgb(234, 88, 12), 3f * s);
        penOrange.EndCap = LineCap.ArrowAnchor;
        g.DrawArc(penOrange, 5 * s, 5 * s, 22 * s, 22 * s, 135, 150);
    }

    private static void DrawStockCount(Graphics g, int sz, float s)
    {
        // Sayım Panosu
        using var bClip = new SolidBrush(Color.FromArgb(13, 148, 136));
        g.FillRectangle(bClip, 6 * s, 6 * s, 20 * s, 22 * s);
        using var bTop = new SolidBrush(Color.FromArgb(20, 184, 166));
        g.FillRectangle(bTop, 11 * s, 3 * s, 10 * s, 5 * s);

        // Onay Tiki
        using var penCheck = new Pen(Color.White, 2.5f * s);
        g.DrawLines(penCheck, new PointF[] { new(10 * s, 17 * s), new(14 * s, 22 * s), new(22 * s, 13 * s) });
    }

    private static void DrawAccounts(Graphics g, int sz, float s)
    {
        // 2 Müşteri Silüeti
        using var b1 = new SolidBrush(Color.FromArgb(124, 58, 237));
        g.FillEllipse(b1, 8 * s, 6 * s, 8 * s, 8 * s);
        g.FillPie(b1, 3 * s, 16 * s, 18 * s, 18 * s, 180, 180);

        using var b2 = new SolidBrush(Color.FromArgb(167, 139, 250));
        g.FillEllipse(b2, 18 * s, 8 * s, 7 * s, 7 * s);
        g.FillPie(b2, 14 * s, 17 * s, 15 * s, 15 * s, 180, 180);
    }

    private static void DrawNewAccount(Graphics g, int sz, float s)
    {
        DrawAccounts(g, sz, s);
        using var bGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillEllipse(bGreen, 18 * s, 17 * s, 12 * s, 12 * s);
        using var penW = new Pen(Color.White, 2f * s);
        g.DrawLine(penW, 24 * s, 20 * s, 24 * s, 26 * s);
        g.DrawLine(penW, 21 * s, 23 * s, 27 * s, 23 * s);
    }

    private static void DrawStatement(Graphics g, int sz, float s)
    {
        // Ekstre Belgesi
        using var bPaper = new SolidBrush(Color.FromArgb(240, 249, 255));
        g.FillRectangle(bPaper, 6 * s, 3 * s, 20 * s, 26 * s);
        using var penBorder = new Pen(Color.FromArgb(2, 132, 199), 1.5f * s);
        g.DrawRectangle(penBorder, 6 * s, 3 * s, 20 * s, 26 * s);

        // Tablo Çizgileri
        using var penGrid = new Pen(Color.FromArgb(56, 189, 248), 1.2f * s);
        g.DrawLine(penGrid, 8 * s, 9 * s, 24 * s, 9 * s);
        g.DrawLine(penGrid, 8 * s, 14 * s, 24 * s, 14 * s);
        g.DrawLine(penGrid, 8 * s, 19 * s, 24 * s, 19 * s);
        g.DrawLine(penGrid, 8 * s, 24 * s, 24 * s, 24 * s);
    }

    private static void DrawDue(Graphics g, int sz, float s)
    {
        // Takvim & Ünlem
        using var bCal = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillRectangle(bCal, 5 * s, 6 * s, 22 * s, 22 * s);
        using var bTop = new SolidBrush(Color.FromArgb(185, 28, 28));
        g.FillRectangle(bTop, 5 * s, 6 * s, 22 * s, 6 * s);

        // Ünlem
        using var penW = new Pen(Color.White, 2.5f * s);
        g.DrawLine(penW, 16 * s, 15 * s, 16 * s, 20 * s);
        using var bDot = new SolidBrush(Color.White);
        g.FillEllipse(bDot, 14.5f * s, 22 * s, 3 * s, 3 * s);
    }

    private static void DrawInvoices(Graphics g, int sz, float s)
    {
        // Fatura Belgesi & Mühür
        using var bInv = new SolidBrush(Color.FromArgb(248, 250, 252));
        g.FillRectangle(bInv, 6 * s, 3 * s, 20 * s, 26 * s);
        using var penBorder = new Pen(Color.FromArgb(15, 118, 110), 1.5f * s);
        g.DrawRectangle(penBorder, 6 * s, 3 * s, 20 * s, 26 * s);

        // Mavi Damga
        using var penStamp = new Pen(Color.FromArgb(13, 148, 136), 1.8f * s);
        g.DrawEllipse(penStamp, 13 * s, 12 * s, 11 * s, 11 * s);
    }

    private static void DrawInvoiceEntry(Graphics g, int sz, float s)
    {
        DrawInvoices(g, sz, s);
        // İçe Aktarma Rozeti
        using var bGreen = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillEllipse(bGreen, 17 * s, 17 * s, 13 * s, 13 * s);
        using var penArrow = new Pen(Color.White, 2f * s);
        penArrow.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(penArrow, 23.5f * s, 19 * s, 23.5f * s, 26 * s);
    }

    private static void DrawCash(Graphics g, int sz, float s)
    {
        // Kasa & Finans Çantası
        using var bBag = new SolidBrush(Color.FromArgb(16, 185, 129));
        g.FillRectangle(bBag, 5 * s, 11 * s, 22 * s, 17 * s);
        using var penHandle = new Pen(Color.FromArgb(5, 150, 105), 2f * s);
        g.DrawArc(penHandle, 11 * s, 5 * s, 10 * s, 10 * s, 180, 180);

        // ₺ Sembolü
        using var f = new Font("Segoe UI", 9 * s, FontStyle.Bold);
        using var bTxt = new SolidBrush(Color.White);
        g.DrawString("₺", f, bTxt, 11.5f * s, 13 * s);
    }

    private static void DrawNotification(Graphics g, int sz, float s)
    {
        // Sarı/Turuncu Çan
        using var bBell = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillPie(bBell, 7 * s, 6 * s, 18 * s, 20 * s, 180, 180);
        g.FillRectangle(bBell, 7 * s, 16 * s, 18 * s, 5 * s);
        g.FillRectangle(bBell, 5 * s, 21 * s, 22 * s, 3 * s);
        using var bClapper = new SolidBrush(Color.FromArgb(217, 119, 6));
        g.FillEllipse(bClapper, 13 * s, 23 * s, 6 * s, 5 * s);
    }

    private static void DrawMobileScanner(Graphics g, int sz, float s)
    {
        // Akıllı Telefon
        using var bPhone = new SolidBrush(Color.FromArgb(99, 102, 241));
        g.FillRectangle(bPhone, 8 * s, 3 * s, 16 * s, 26 * s);
        using var bScreen = new SolidBrush(Color.FromArgb(224, 231, 255));
        g.FillRectangle(bScreen, 10 * s, 6 * s, 12 * s, 19 * s);

        // QR / Tarayıcı Işığı
        using var penLaser = new Pen(Color.FromArgb(239, 68, 68), 1.8f * s);
        g.DrawLine(penLaser, 8 * s, 15 * s, 24 * s, 15 * s);
    }

    private static void DrawAuditLogs(Graphics g, int sz, float s)
    {
        // Güvenlik Kalkanı
        using var bShield = new SolidBrush(Color.FromArgb(71, 85, 105));
        g.FillPolygon(bShield, new PointF[] {
            new(16 * s, 3 * s), new(26 * s, 7 * s), new(26 * s, 17 * s), new(16 * s, 28 * s), new(6 * s, 17 * s), new(6 * s, 7 * s)
        });

        // Büyüteç
        using var penMag = new Pen(Color.FromArgb(56, 189, 248), 2f * s);
        g.DrawEllipse(penMag, 11 * s, 9 * s, 8 * s, 8 * s);
        g.DrawLine(penMag, 17 * s, 15 * s, 21 * s, 19 * s);
    }

    private static void DrawUsers(Graphics g, int sz, float s)
    {
        // Mor Kullanıcı & Anahtar
        using var bUser = new SolidBrush(Color.FromArgb(147, 51, 234));
        g.FillEllipse(bUser, 11 * s, 4 * s, 10 * s, 10 * s);
        g.FillPie(bUser, 6 * s, 15 * s, 20 * s, 20 * s, 180, 180);

        // Yıldız / Rozet
        using var bStar = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillEllipse(bStar, 19 * s, 16 * s, 10 * s, 10 * s);
    }

    private static void DrawLicense(Graphics g, int sz, float s)
    {
        // Altın Lisans Anahtarı
        using var bGold = new SolidBrush(Color.FromArgb(245, 158, 11));
        g.FillEllipse(bGold, 5 * s, 6 * s, 12 * s, 12 * s);
        using var bHole = new SolidBrush(Color.White);
        g.FillEllipse(bHole, 8 * s, 9 * s, 6 * s, 6 * s);

        using var penKey = new Pen(Color.FromArgb(217, 119, 6), 3.5f * s);
        g.DrawLine(penKey, 15 * s, 12 * s, 27 * s, 24 * s);
        g.DrawLine(penKey, 23 * s, 20 * s, 27 * s, 16 * s);
        g.DrawLine(penKey, 26 * s, 23 * s, 29 * s, 20 * s);
    }

    private static void DrawSettings(Graphics g, int sz, float s)
    {
        // Dişli Çark
        using var bGear = new SolidBrush(Color.FromArgb(100, 116, 139));
        g.FillEllipse(bGear, 6 * s, 6 * s, 20 * s, 20 * s);
        using var penTeeth = new Pen(Color.FromArgb(71, 85, 105), 3f * s);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4;
            float x1 = 16 * s + (float)(Math.Cos(angle) * 10 * s);
            float y1 = 16 * s + (float)(Math.Sin(angle) * 10 * s);
            float x2 = 16 * s + (float)(Math.Cos(angle) * 14 * s);
            float y2 = 16 * s + (float)(Math.Sin(angle) * 14 * s);
            g.DrawLine(penTeeth, x1, y1, x2, y2);
        }
        using var bHole = new SolidBrush(Color.White);
        g.FillEllipse(bHole, 12 * s, 12 * s, 8 * s, 8 * s);
    }

    private static void DrawTheme(Graphics g, int sz, float s)
    {
        // Ressam Paleti
        using var bPal = new SolidBrush(Color.FromArgb(236, 72, 153));
        g.FillEllipse(bPal, 5 * s, 5 * s, 22 * s, 22 * s);
        // Renk Noktaları
        using var b1 = new SolidBrush(Color.Yellow);
        using var b2 = new SolidBrush(Color.Cyan);
        using var b3 = new SolidBrush(Color.LimeGreen);
        g.FillEllipse(b1, 9 * s, 9 * s, 4 * s, 4 * s);
        g.FillEllipse(b2, 17 * s, 8 * s, 4 * s, 4 * s);
        g.FillEllipse(b3, 10 * s, 16 * s, 4 * s, 4 * s);
    }

    private static void DrawLogout(Graphics g, int sz, float s)
    {
        // Kapı & Çıkış Oku
        using var bDoor = new SolidBrush(Color.FromArgb(239, 68, 68));
        g.FillRectangle(bDoor, 5 * s, 4 * s, 12 * s, 24 * s);

        using var penArrow = new Pen(Color.FromArgb(220, 38, 38), 3f * s);
        penArrow.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(penArrow, 14 * s, 16 * s, 27 * s, 16 * s);
    }

    private static void DrawExcel(Graphics g, int sz, float s)
    {
        // Excel Yeşil Belge
        using var bDoc = new SolidBrush(Color.FromArgb(22, 163, 74));
        g.FillRectangle(bDoc, 6 * s, 4 * s, 20 * s, 24 * s);

        using var f = new Font("Segoe UI", 11 * s, FontStyle.Bold);
        using var bTxt = new SolidBrush(Color.White);
        g.DrawString("X", f, bTxt, 10 * s, 8 * s);
    }

    private static void DrawQuickActions(Graphics g, int sz, float s)
    {
        // Roket / Şimşekli Hızlı Aksiyon
        using var bRocket = new SolidBrush(Color.FromArgb(245, 158, 11)); // Canlı Kehribar Sarı/Turuncu
        PointF[] bolt = new PointF[]
        {
            new(17 * s, 3 * s),
            new(9 * s, 17 * s),
            new(15 * s, 17 * s),
            new(12 * s, 29 * s),
            new(23 * s, 13 * s),
            new(17 * s, 13 * s),
            new(20 * s, 3 * s)
        };
        g.FillPolygon(bRocket, bolt);

        using var penGlow = new Pen(Color.FromArgb(217, 119, 6), 1.5f * s);
        g.DrawPolygon(penGlow, bolt);
    }

    private static void DrawLayout(Graphics g, int sz, float s)
    {
        // 4 Parçalı Pencereler / Izgara Düzeni
        using var b1 = new SolidBrush(Color.FromArgb(59, 130, 246));
        using var b2 = new SolidBrush(Color.FromArgb(14, 165, 233));
        using var b3 = new SolidBrush(Color.FromArgb(99, 102, 241));
        using var b4 = new SolidBrush(Color.FromArgb(168, 85, 247));

        g.FillRectangle(b1, 4 * s, 4 * s, 11 * s, 11 * s);
        g.FillRectangle(b2, 17 * s, 4 * s, 11 * s, 11 * s);
        g.FillRectangle(b3, 4 * s, 17 * s, 11 * s, 11 * s);
        g.FillRectangle(b4, 17 * s, 17 * s, 11 * s, 11 * s);
    }

    private static void DrawBilensisLogo(Graphics g, int sz, float s)
    {
        // Yuvarlatılmış Modern Mavi Gradyan Kart
        var rect = new RectangleF(2 * s, 2 * s, 28 * s, 28 * s);
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(30, 58, 138), Color.FromArgb(37, 99, 235), LinearGradientMode.ForwardDiagonal);
        
        using var path = new GraphicsPath();
        float radius = 7 * s;
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);

        // Beyaz 'B' Harfi
        using var font = new Font("Segoe UI", 16 * s, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("B", font, textBrush, new RectangleF(0, 0, sz, sz), sf);

        // Sağ altta yeşil aktiflik / büyüme noktası
        using var bDot = new SolidBrush(Color.FromArgb(52, 211, 153));
        g.FillEllipse(bDot, 21 * s, 21 * s, 6 * s, 6 * s);
    }

    private static void DrawStockInOut(Graphics g, int sz, float s)
    {
        // Kutu ve Çift Yönlü Giriş/Çıkış Okları
        using var penBox = new Pen(Color.FromArgb(217, 119, 6), 2f * s);
        g.DrawRectangle(penBox, 6 * s, 8 * s, 20 * s, 16 * s);

        using var penIn = new Pen(Color.FromArgb(16, 185, 129), 2.5f * s);
        penIn.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(penIn, 12 * s, 3 * s, 12 * s, 13 * s);

        using var penOut = new Pen(Color.FromArgb(239, 68, 68), 2.5f * s);
        penOut.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(penOut, 20 * s, 21 * s, 20 * s, 29 * s);
    }

    private static void DrawRefresh(Graphics g, int sz, float s)
    {
        using var pen = new Pen(Color.FromArgb(14, 165, 233), 2.5f * s);
        g.DrawArc(pen, 6 * s, 6 * s, 20 * s, 20 * s, 30, 280);
        pen.EndCap = LineCap.ArrowAnchor;
        g.DrawLine(pen, 20 * s, 5 * s, 26 * s, 7 * s);
    }

    private static void DrawGeneric(Graphics g, int sz, float s)
    {
        using var b = new SolidBrush(Color.FromArgb(59, 130, 246));
        g.FillEllipse(b, 6 * s, 6 * s, 20 * s, 20 * s);
    }

    private static void DrawWhatsApp(Graphics g, int sz, float s)
    {
        // WhatsApp Yeşili Dairesel Balon
        using var bGreen = new SolidBrush(Color.FromArgb(37, 211, 102));
        g.FillEllipse(bGreen, 3 * s, 3 * s, 26 * s, 26 * s);

        // Konuşma kuyruğu
        var pts = new PointF[]
        {
            new PointF(6 * s, 22 * s),
            new PointF(4 * s, 27 * s),
            new PointF(10 * s, 25 * s)
        };
        g.FillPolygon(bGreen, pts);

        // Beyaz Telefon / Mesaj Simgesi
        using var penW = new Pen(Color.White, 2.2f * s)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawArc(penW, 10 * s, 10 * s, 12 * s, 12 * s, -30, 180);
        g.DrawLine(penW, 10 * s, 16 * s, 13 * s, 19 * s);
        g.DrawLine(penW, 18 * s, 11 * s, 21 * s, 14 * s);
    }

    private static void DrawBatchInvoices(Graphics g, int sz, float s)
    {
        // Çoklu Belge Yığını (Toplu PDF)
        // 1. Arka Belge (Gri/Açık Mavi)
        using var bBack = new SolidBrush(Color.FromArgb(203, 213, 225));
        g.FillRectangle(bBack, 3 * s, 3 * s, 18 * s, 22 * s);
        using var pBack = new Pen(Color.FromArgb(148, 163, 184), 1.2f * s);
        g.DrawRectangle(pBack, 3 * s, 3 * s, 18 * s, 22 * s);

        // 2. Orta Belge (Teal)
        using var bMid = new SolidBrush(Color.FromArgb(241, 245, 249));
        g.FillRectangle(bMid, 7 * s, 6 * s, 18 * s, 22 * s);
        using var pMid = new Pen(Color.FromArgb(14, 116, 144), 1.4f * s);
        g.DrawRectangle(pMid, 7 * s, 6 * s, 18 * s, 22 * s);

        // Belge çizgileri
        using var pLine = new Pen(Color.FromArgb(56, 189, 248), 1.2f * s);
        g.DrawLine(pLine, 10 * s, 11 * s, 21 * s, 11 * s);
        g.DrawLine(pLine, 10 * s, 15 * s, 21 * s, 15 * s);
        g.DrawLine(pLine, 10 * s, 19 * s, 18 * s, 19 * s);

        // PDF Kırmızı Rozeti
        using var bBadge = new SolidBrush(Color.FromArgb(220, 38, 38));
        g.FillEllipse(bBadge, 17 * s, 17 * s, 13 * s, 13 * s);
        using var pPlus = new Pen(Color.White, 2f * s);
        g.DrawLine(pPlus, 23.5f * s, 20 * s, 23.5f * s, 27 * s);
        g.DrawLine(pPlus, 20 * s, 23.5f * s, 27 * s, 23.5f * s);
    }
}
