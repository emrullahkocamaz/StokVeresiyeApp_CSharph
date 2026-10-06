using System;
using System.Text.RegularExpressions;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Helpers;

public static class BarcodeHelper
{
    private static readonly Random _rnd = new();

    /// <summary>
    /// 12 haneli rakam dizesinden uluslararası GS1 standart EAN-13 kontrol basamağını (13. basamak) hesaplar.
    /// </summary>
    public static int CalculateEan13CheckDigit(string first12Digits)
    {
        if (string.IsNullOrWhiteSpace(first12Digits) || first12Digits.Length != 12 || !Regex.IsMatch(first12Digits, @"^\d{12}$"))
            return 0;

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int d = first12Digits[i] - '0';
            // Tek basamaklar (0,2,4...) x 1, Çift basamaklar (1,3,5...) x 3
            sum += (i % 2 == 0) ? d : (d * 3);
        }

        int mod = sum % 10;
        return (mod == 0) ? 0 : (10 - mod);
    }

    /// <summary>
    /// Bir barkod numarasının geçerli bir EAN-13 olup olmadığını doğrular.
    /// </summary>
    public static bool IsValidEan13(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 13 || !Regex.IsMatch(barcode, @"^\d{13}$"))
            return false;

        string first12 = barcode.Substring(0, 12);
        int expectedCheck = CalculateEan13CheckDigit(first12);
        int actualCheck = barcode[12] - '0';
        return expectedCheck == actualCheck;
    }

    /// <summary>
    /// Sistemde ve veritabanında daha önce hiç kullanılmamış, standart POS cihazlarıyla %100 uyumlu
    /// 13 haneli benzersiz EAN-13 mağaza içi iç barkod numarası üretir.
    /// (Prefix: 200 - Uluslararası GS1 standartlarında mağaza içi serbest kullanım aralığı)
    /// </summary>
    public static string GenerateUniqueInternalBarcode(string prefix = "200")
    {
        while (true)
        {
            // 200 + YYMMDD (6 hane) + 3 hane rastgele = 12 hane
            string timePart = DateTime.Now.ToString("yyMMdd");
            int rndPart = _rnd.Next(100, 999);
            string base12 = $"{prefix}{timePart}{rndPart}";

            if (base12.Length > 12) base12 = base12.Substring(0, 12);
            while (base12.Length < 12) base12 += _rnd.Next(0, 10).ToString();

            int checkDigit = CalculateEan13CheckDigit(base12);
            string fullBarcode = base12 + checkDigit;

            // Veritabanında daha önce kayıtlı mı diye kontrol et
            try
            {
                var count = Database.ExecuteScalar("SELECT COUNT(1) FROM Products WHERE Barcode = @b;", ("@b", fullBarcode));
                if (Convert.ToInt32(count) == 0)
                {
                    return fullBarcode;
                }
            }
            catch
            {
                return fullBarcode;
            }
        }
    }
}
