using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace StokVeresiyeApp.Services;

public class ReceiptConfig
{
    public string PrinterName { get; set; } = "";
    public int PaperWidthMm { get; set; } = 80; // 58 veya 80 mm
    public string StoreHeader { get; set; } = "BİLENSİS TİCARET";
    public string StoreSubHeader { get; set; } = "Stok & Perakende Satış Noktası";
    public string FooterNote { get; set; } = "Bizi tercih ettiğiniz için teşekkür ederiz.\nMali değeri yoktur, bilgi fişidir.";
    public bool AutoPrintOnSale { get; set; } = false;
}

public class ReceiptPrintItem
{
    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public double UnitPrice { get; set; }
    public double Total { get; set; }
}

public static class ThermalReceiptService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp",
        "thermal_receipt_config.json"
    );

    private static ReceiptConfig _config = new();

    static ThermalReceiptService()
    {
        LoadConfig();
    }

    public static ReceiptConfig Config => _config;

    public static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<ReceiptConfig>(json);
                if (loaded != null) _config = loaded;
            }
        }
        catch { }
    }

    public static void SaveConfig()
    {
        try
        {
            string dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Satış tamamlandığında termal yazıcıya bilgi fişi basar.
    /// </summary>
    public static (bool Success, string Message) PrintSale(
        string customerName,
        string docNo,
        string paymentType,
        List<ReceiptPrintItem> items,
        double grossTotal,
        double discount,
        double netTotal,
        double remainingBalance)
    {
        try
        {
            using var pd = new PrintDocument();
            if (!string.IsNullOrWhiteSpace(_config.PrinterName))
            {
                pd.PrinterSettings.PrinterName = _config.PrinterName;
            }

            // Sayfa genişliği (58mm = yaklaşık 200px, 80mm = yaklaşık 280px)
            int printWidth = _config.PaperWidthMm == 58 ? 200 : 280;

            pd.PrintPage += (s, ev) =>
            {
                var g = ev.Graphics;
                if (g == null) return;

                int y = 10;
                int leftMargin = 5;
                int rightMargin = printWidth - 5;

                using var fontHeader = new Font("Courier New", 11f, FontStyle.Bold);
                using var fontBold = new Font("Courier New", 9f, FontStyle.Bold);
                using var fontRegular = new Font("Courier New", 8.5f, FontStyle.Regular);
                using var fontSmall = new Font("Courier New", 7.5f, FontStyle.Regular);
                using var brush = new SolidBrush(Color.Black);
                using var pen = new Pen(Color.Black, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };

                var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
                var sfLeft = new StringFormat { Alignment = StringAlignment.Near };
                var sfRight = new StringFormat { Alignment = StringAlignment.Far };

                // 1. Mağaza Başlığı
                g.DrawString(_config.StoreHeader, fontHeader, brush, new RectangleF(0, y, printWidth, 20), sfCenter);
                y += 20;

                if (!string.IsNullOrWhiteSpace(_config.StoreSubHeader))
                {
                    g.DrawString(_config.StoreSubHeader, fontSmall, brush, new RectangleF(0, y, printWidth, 16), sfCenter);
                    y += 18;
                }

                // Çizgi
                g.DrawLine(pen, leftMargin, y, rightMargin, y);
                y += 6;

                // 2. Fiş Bilgileri
                g.DrawString($"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}", fontSmall, brush, leftMargin, y);
                y += 15;
                if (!string.IsNullOrWhiteSpace(docNo))
                {
                    g.DrawString($"Belge: {docNo}", fontSmall, brush, leftMargin, y);
                    y += 15;
                }
                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    g.DrawString($"Cari: {customerName}", fontSmall, brush, leftMargin, y);
                    y += 15;
                }
                g.DrawString($"Ödeme: {paymentType}", fontSmall, brush, leftMargin, y);
                y += 18;

                // Çizgi
                g.DrawLine(pen, leftMargin, y, rightMargin, y);
                y += 6;

                // 3. Kalem Başlıkları
                g.DrawString("ÜRÜN / MİKTAR", fontBold, brush, leftMargin, y);
                g.DrawString("TUTAR", fontBold, brush, rightMargin, y, sfRight);
                y += 16;
                g.DrawLine(pen, leftMargin, y, rightMargin, y);
                y += 6;

                // 4. Kalemler
                foreach (var it in items)
                {
                    string prodLine = it.Name.Length > 22 ? it.Name.Substring(0, 22) : it.Name;
                    g.DrawString(prodLine, fontRegular, brush, leftMargin, y);
                    g.DrawString($"{it.Total:N2}", fontBold, brush, rightMargin, y, sfRight);
                    y += 14;

                    g.DrawString($" {it.Quantity:N2} x {it.UnitPrice:N2}", fontSmall, brush, leftMargin, y);
                    y += 16;
                }

                // Çizgi
                g.DrawLine(pen, leftMargin, y, rightMargin, y);
                y += 6;

                // 5. Alt Toplamlar
                if (discount > 0)
                {
                    g.DrawString("Ara Toplam:", fontRegular, brush, leftMargin, y);
                    g.DrawString($"{grossTotal:N2} ₺", fontRegular, brush, rightMargin, y, sfRight);
                    y += 15;

                    g.DrawString("İskonto:", fontRegular, brush, leftMargin, y);
                    g.DrawString($"-{discount:N2} ₺", fontRegular, brush, rightMargin, y, sfRight);
                    y += 15;
                }

                g.DrawString("GENEL TOPLAM:", fontBold, brush, leftMargin, y);
                g.DrawString($"{netTotal:N2} ₺", fontBold, brush, rightMargin, y, sfRight);
                y += 20;

                if (remainingBalance > 0)
                {
                    g.DrawString("Kalan Cari Borç:", fontSmall, brush, leftMargin, y);
                    g.DrawString($"{remainingBalance:N2} ₺", fontBold, brush, rightMargin, y, sfRight);
                    y += 16;
                }

                // Çizgi
                g.DrawLine(pen, leftMargin, y, rightMargin, y);
                y += 8;

                // 6. Teşekkür Notu
                if (!string.IsNullOrWhiteSpace(_config.FooterNote))
                {
                    g.DrawString(_config.FooterNote, fontSmall, brush, new RectangleF(0, y, printWidth, 35), sfCenter);
                    y += 35;
                }

                ev.HasMorePages = false;
            };

            pd.Print();
            return (true, "Fiş başarıyla yazdırıldı.");
        }
        catch (Exception ex)
        {
            return (false, $"Yazdırma hatası: {ex.Message}");
        }
    }
}
