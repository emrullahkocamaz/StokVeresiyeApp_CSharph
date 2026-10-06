using System.Globalization;

namespace StokVeresiyeApp.Helpers;

public enum ScaleBarcodeType
{
    None,           // Standart Barkod
    EmbeddedWeight, // 28: Ağırlık / Gramaj Kodlu Barkod (Örn: 28 + 00123 + 01450 + C -> 1.450 kg)
    EmbeddedPrice   // 27: Tutar / Fiyat Kodlu Barkod (Örn: 27 + 00123 + 01550 + C -> 15.50 TL)
}

public class ScaleBarcodeResult
{
    public bool IsScaleBarcode { get; set; }
    public ScaleBarcodeType BarcodeType { get; set; } = ScaleBarcodeType.None;
    public string RawBarcode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;     // 5 haneli ürün PLU kodu (örn: 00123)
    public string CleanProductCode { get; set; } = string.Empty; // Baştaki sıfırlar atılmış hali (örn: 123)
    public double WeightKg { get; set; }                        // Gramaj (kg cinsinden, örn: 1.450)
    public double TotalPrice { get; set; }                      // Tutar (TL cinsinden, örn: 15.50)
    public string Description { get; set; } = string.Empty;
}

public static class BarcodeScaleHelper
{
    /// <summary>
    /// Verilen barkodun terazi barkodu (27 veya 28 ile başlayan EAN-13) olup olmadığını çözümler.
    /// </summary>
    public static ScaleBarcodeResult Parse(string rawBarcode)
    {
        if (string.IsNullOrWhiteSpace(rawBarcode))
            return new ScaleBarcodeResult { RawBarcode = rawBarcode ?? string.Empty };

        string barcode = rawBarcode.Trim();

        // Terazi barkodları genellikle 12 veya 13 karakter uzunluğundadır (EAN-13)
        // Format: [2 hane Prefix] [5 hane Ürün Kodu] [5 hane Değer] [1 hane CheckDigit]
        if (barcode.Length >= 12 && (barcode.StartsWith("27") || barcode.StartsWith("28")))
        {
            string prefix = barcode.Substring(0, 2);
            string productCode = barcode.Substring(2, 5);
            string cleanCode = productCode.TrimStart('0');
            if (string.IsNullOrEmpty(cleanCode)) cleanCode = "0";

            string valuePart = barcode.Length >= 12 ? barcode.Substring(7, 5) : "00000";

            if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out double rawVal))
            {
                if (prefix == "28")
                {
                    // 28: Ağırlık / Gramaj (5 hane gram cinsinden)
                    // Örn: 01450 -> 1.450 kg
                    double weightKg = rawVal / 1000.0;
                    return new ScaleBarcodeResult
                    {
                        IsScaleBarcode = true,
                        BarcodeType = ScaleBarcodeType.EmbeddedWeight,
                        RawBarcode = barcode,
                        ProductCode = productCode,
                        CleanProductCode = cleanCode,
                        WeightKg = weightKg,
                        TotalPrice = 0,
                        Description = $"Tartılı Ürün (Ağırlık: {weightKg:N3} kg)"
                    };
                }
                else if (prefix == "27")
                {
                    // 27: Fiyat / Tutar (5 hane kuruş cinsinden)
                    // Örn: 01550 -> 15.50 TL
                    double totalPrice = rawVal / 100.0;
                    return new ScaleBarcodeResult
                    {
                        IsScaleBarcode = true,
                        BarcodeType = ScaleBarcodeType.EmbeddedPrice,
                        RawBarcode = barcode,
                        ProductCode = productCode,
                        CleanProductCode = cleanCode,
                        WeightKg = 0,
                        TotalPrice = totalPrice,
                        Description = $"Tartılı Ürün (Tutar: {totalPrice:N2} ₺)"
                    };
                }
            }
        }

        // Standart Normal Ürün Barkodu
        return new ScaleBarcodeResult
        {
            IsScaleBarcode = false,
            BarcodeType = ScaleBarcodeType.None,
            RawBarcode = barcode,
            ProductCode = barcode,
            CleanProductCode = barcode,
            WeightKg = 1.0,
            TotalPrice = 0,
            Description = "Normal Barkod"
        };
    }
}
