using System.Drawing;

namespace StokVeresiyeApp.Helpers;

public static class BarcodeRenderer
{
    // Code 128B Karakter Tablosu
    private static readonly string[] Patterns = new string[]
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213", // 0-9
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132", // 10-19
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211", // 20-29
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313", // 30-39
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331", // 40-49
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111", // 50-59
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214", // 60-69
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111", // 70-79
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141", // 80-89
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141", // 90-99
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112" // 100-106 (StartB = 104, Stop = 106)
    };

    public static void DrawBarcode(Graphics g, string code, RectangleF bounds)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        // Code 128B Encoding
        var patternList = new List<string>();
        int checksum = 104; // Start B
        patternList.Add(Patterns[104]);

        int weight = 1;
        foreach (char c in code)
        {
            int val = c - 32;
            if (val >= 0 && val <= 102)
            {
                patternList.Add(Patterns[val]);
                checksum += val * weight;
                weight++;
            }
        }

        int checkIndex = checksum % 103;
        patternList.Add(Patterns[checkIndex]);
        patternList.Add(Patterns[106]); // Stop

        string fullPattern = string.Join("", patternList);

        // Toplam modül genişliğini hesapla
        int totalModules = 0;
        foreach (char ch in fullPattern)
        {
            totalModules += (ch - '0');
        }

        float moduleWidth = bounds.Width / totalModules;
        if (moduleWidth < 0.5f) moduleWidth = 0.5f;

        float currentX = bounds.X + (bounds.Width - (totalModules * moduleWidth)) / 2f;
        bool isBar = true;

        using var blackBrush = new SolidBrush(Color.Black);

        foreach (char ch in fullPattern)
        {
            int width = ch - '0';
            float barWidth = width * moduleWidth;

            if (isBar)
            {
                g.FillRectangle(blackBrush, currentX, bounds.Y, barWidth, bounds.Height);
            }

            currentX += barWidth;
            isBar = !isBar;
        }
    }

    public static void DrawProductLabel(Graphics g, string productName, string barcode, double price, string template, Rectangle bounds)
    {
        g.FillRectangle(Brushes.White, bounds);
        using var pen = new Pen(Color.FromArgb(200, 200, 200), 1f);
        g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        int pad = 8;
        int innerX = bounds.X + pad;
        int innerY = bounds.Y + pad;
        int innerW = bounds.Width - (pad * 2);
        int innerH = bounds.Height - (pad * 2);

        // Şablon: "Raf Etiketi (Büyük Fiyatlı)"
        if (template.Contains("Raf"))
        {
            // 1. Ürün Adı
            using var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            using var formatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
            var titleRect = new RectangleF(innerX, innerY, innerW, 36);
            g.DrawString(productName, fontTitle, Brushes.Black, titleRect, formatCenter);

            // 2. Barkod Çizgileri
            float barcodeY = innerY + 38;
            float barcodeH = innerH - 95;
            if (barcodeH < 25) barcodeH = 25;
            var barcodeRect = new RectangleF(innerX, barcodeY, innerW, barcodeH);
            DrawBarcode(g, barcode, barcodeRect);

            // 3. Barkod Numarası
            using var fontCode = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            g.DrawString(barcode, fontCode, Brushes.DimGray, new RectangleF(innerX, barcodeY + barcodeH + 2, innerW, 16), formatCenter);

            // 4. Büyük Fiyat Bandı
            float priceY = innerY + innerH - 36;
            using var fontPrice = new Font("Segoe UI", 16f, FontStyle.Bold);
            using var fontSymbol = new Font("Segoe UI", 10f, FontStyle.Bold);
            
            var priceBgRect = new Rectangle(innerX, (int)priceY, innerW, 36);
            g.FillRectangle(new SolidBrush(Color.FromArgb(241, 245, 249)), priceBgRect);
            g.DrawRectangle(pen, priceBgRect);

            string priceStr = $"{price:N2} ₺";
            g.DrawString(priceStr, fontPrice, new SolidBrush(Color.FromArgb(15, 23, 42)), priceBgRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        else // Standart Ürün / Termal Rulo Etiketi
        {
            // 1. Ürün Adı
            using var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var formatCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
            var titleRect = new RectangleF(innerX, innerY, innerW, 28);
            g.DrawString(productName, fontTitle, Brushes.Black, titleRect, formatCenter);

            // 2. Barkod Çizgileri
            float barcodeY = innerY + 30;
            float barcodeH = innerH - 65;
            if (barcodeH < 20) barcodeH = 20;
            var barcodeRect = new RectangleF(innerX, barcodeY, innerW, barcodeH);
            DrawBarcode(g, barcode, barcodeRect);

            // 3. Barkod Metni
            using var fontCode = new Font("Segoe UI", 8f, FontStyle.Regular);
            g.DrawString(barcode, fontCode, Brushes.DimGray, new RectangleF(innerX, barcodeY + barcodeH + 1, innerW, 14), formatCenter);

            // 4. Fiyat
            using var fontPrice = new Font("Segoe UI", 12f, FontStyle.Bold);
            g.DrawString($"{price:N2} ₺", fontPrice, new SolidBrush(Color.FromArgb(220, 38, 38)), new RectangleF(innerX, innerY + innerH - 22, innerW, 22), formatCenter);
        }
    }

    /// <summary>
    /// Verilen metin veya URL için yüksek çözünürlüklü QR Kod Bitmap üretir.
    /// </summary>
    public static Bitmap GenerateQrCode(string content, int width = 250, int height = 250)
    {
        try
        {
            var writer = new ZXing.Windows.Compatibility.BarcodeWriter
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new ZXing.QrCode.QrCodeEncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 1,
                    CharacterSet = "UTF-8"
                }
            };
            return writer.Write(content);
        }
        catch
        {
            var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.DrawString(content, SystemFonts.DefaultFont, Brushes.Black, 10, 10);
            return bmp;
        }
    }
}
