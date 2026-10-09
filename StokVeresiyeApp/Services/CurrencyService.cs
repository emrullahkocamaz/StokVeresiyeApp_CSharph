using System.Globalization;
using System.Net.Http;
using System.Xml.Linq;

namespace StokVeresiyeApp.Services;

public class CurrencyRateItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double ForexBuying { get; set; }
    public double ForexSelling { get; set; }
    public double BanknoteBuying { get; set; }
    public double BanknoteSelling { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
}

public static class CurrencyService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static List<CurrencyRateItem> _cachedRates = new();
    private static DateTime _lastFetchTime = DateTime.MinValue;

    public static async Task<List<CurrencyRateItem>> GetTcmbRatesAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedRates.Count > 0 && (DateTime.Now - _lastFetchTime).TotalMinutes < 30)
        {
            return _cachedRates;
        }

        try
        {
            string url = "https://www.tcmb.gov.tr/kurlar/today.xml";
            var xmlBytes = await _http.GetByteArrayAsync(url);
            // TCMB XML ISO-8859-9 veya UTF-8 olabilir
            string xmlStr = System.Text.Encoding.UTF8.GetString(xmlBytes);
            var xdoc = XDocument.Parse(xmlStr);

            var list = new List<CurrencyRateItem>();
            var rootDateStr = xdoc.Root?.Attribute("Tarih")?.Value ?? DateTime.Today.ToString("dd.MM.yyyy");

            foreach (var elem in xdoc.Descendants("Currency"))
            {
                string code = elem.Attribute("CurrencyCode")?.Value ?? "";
                if (code != "USD" && code != "EUR" && code != "GBP" && code != "CHF" && code != "RUB")
                    continue;

                string name = elem.Element("Isim")?.Value ?? code;
                double fb = ParseDouble(elem.Element("ForexBuying")?.Value);
                double fs = ParseDouble(elem.Element("ForexSelling")?.Value);
                double bb = ParseDouble(elem.Element("BanknoteBuying")?.Value);
                double bs = ParseDouble(elem.Element("BanknoteSelling")?.Value);

                list.Add(new CurrencyRateItem
                {
                    Code = code,
                    Name = name,
                    ForexBuying = fb > 0 ? fb : bb,
                    ForexSelling = fs > 0 ? fs : bs,
                    BanknoteBuying = bb > 0 ? bb : fb,
                    BanknoteSelling = bs > 0 ? bs : fs,
                    Date = DateTime.Today
                });
            }

            if (list.Count > 0)
            {
                _cachedRates = list;
                _lastFetchTime = DateTime.Now;
                return list;
            }
        }
        catch
        {
            // İnternet yoksa veya TCMB erişilemezse
        }

        // Fallback / Önbellek
        if (_cachedRates.Count > 0) return _cachedRates;

        return GetFallbackRates();
    }

    public static List<CurrencyRateItem> GetTcmbRatesSync()
    {
        try
        {
            return Task.Run(() => GetTcmbRatesAsync()).GetAwaiter().GetResult();
        }
        catch
        {
            return GetFallbackRates();
        }
    }

    private static double ParseDouble(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return 0;
        if (double.TryParse(val.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double res))
            return res;
        if (double.TryParse(val.Trim().Replace('.', ','), out double resTr))
            return resTr;
        return 0;
    }

    private static List<CurrencyRateItem> GetFallbackRates()
    {
        return new List<CurrencyRateItem>
        {
            new() { Code = "USD", Name = "ABD DOLARI", ForexBuying = 34.20, ForexSelling = 34.35 },
            new() { Code = "EUR", Name = "EURO", ForexBuying = 37.10, ForexSelling = 37.28 },
            new() { Code = "GBP", Name = "İNGİLİZ STERLİNİ", ForexBuying = 44.50, ForexSelling = 44.75 }
        };
    }
}
