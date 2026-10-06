using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;
using StokVeresiyeApp.Services;

namespace StokVeresiyeApp.Services;

public class MobileScanLogItem
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public double Stock { get; set; }
    public double Price { get; set; }
    public string DeviceInfo { get; set; } = string.Empty;
}

public static class MobileScannerService
{
    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    public static int Port { get; private set; } = 5055;
    public static bool IsRunning { get; private set; }
    public static List<MobileScanLogItem> RecentScans { get; } = new();

    public static event Action<string, Product?>? BarcodeScannedFromMobile;
    public static event Action<string>? LogReceived;

    /// <summary>
    /// Mobil barkod HTTP sunucusunu başlatır.
    /// </summary>
    private static readonly object _startLock = new();
    private static bool _networkSetupAttempted;

    public static bool Start(int preferredPort = 5055)
    {
        lock (_startLock)
        {
            if (IsRunning) return true;

            int[] candidatePorts = { preferredPort, 5056, 5057, 8085, 8090 };

            // 1) Tüm ağ arayüzlerinde dinlemeyi dene (telefon erişimi için gerekli)
            foreach (var port in candidatePorts)
            {
                if (TryStartListener(port, $"http://*:{port}/"))
                {
                    EnsureFirewallRuleOnce(candidatePorts);
                    return true;
                }
            }

            // 2) Yetki yoksa: tek seferlik yönetici izniyle URL ACL + güvenlik duvarı kuralı ekle
            if (!_networkSetupAttempted)
            {
                _networkSetupAttempted = true;
                if (EnsureNetworkAccess(candidatePorts))
                {
                    try
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BilgeStok");
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(Path.Combine(dir, "mobile_fw_ok.flag"), DateTime.Now.ToString("o"));
                    }
                    catch { }
                    foreach (var port in candidatePorts)
                    {
                        if (TryStartListener(port, $"http://*:{port}/")) return true;
                    }
                }
            }

            // 3) Son çare: yalnızca bu bilgisayardan erişim (telefon bağlanamaz)
            foreach (var port in candidatePorts)
            {
                if (TryStartListener(port, $"http://localhost:{port}/", $"http://127.0.0.1:{port}/"))
                {
                    LogReceived?.Invoke("Yönetici izni verilmediği için sunucu yalnızca bu bilgisayardan erişilebilir.");
                    return true;
                }
            }

            return false;
        }
    }

    private static void EnsureFirewallRuleOnce(int[] ports)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BilgeStok");
            Directory.CreateDirectory(dir);
            string marker = Path.Combine(dir, "mobile_fw_ok.flag");
            if (File.Exists(marker) || _networkSetupAttempted) return;

            _networkSetupAttempted = true;
            if (EnsureNetworkAccess(ports))
                File.WriteAllText(marker, DateTime.Now.ToString("o"));
        }
        catch { }
    }

    private static bool TryStartListener(int port, params string[] prefixes)
    {
        try
        {
            var listener = new HttpListener();
            foreach (var p in prefixes) listener.Prefixes.Add(p);
            listener.Start();

            _listener = listener;
            Port = port;
            IsRunning = true;
            _cts = new CancellationTokenSource();

            Task.Run(() => ListenLoopAsync(_cts.Token));
            LogReceived?.Invoke($"Mobil Barkod Sunucusu başlatıldı. Port: {Port}");
            return true;
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"Port {port} başlatılamadı: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Yönetici izni (UAC) ile http URL rezervasyonu ve gelen bağlantı için güvenlik duvarı kuralı ekler.
    /// </summary>
    private static bool EnsureNetworkAccess(int[] ports)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var port in ports)
            {
                // S-1-1-0 = Everyone (Windows dilinden bağımsız)
                sb.Append($"netsh http delete urlacl url=http://*:{port}/ >nul 2>&1 & ");
                sb.Append($"netsh http add urlacl url=http://*:{port}/ sddl=D:(A;;GX;;;S-1-1-0) >nul 2>&1 & ");
            }
            string portList = string.Join(",", ports);
            sb.Append("netsh advfirewall firewall delete rule name=\"Bilensis Mobil Barkod\" >nul 2>&1 & ");
            sb.Append($"netsh advfirewall firewall add rule name=\"Bilensis Mobil Barkod\" dir=in action=allow protocol=TCP localport={portList} profile=any >nul 2>&1");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + sb,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(15000);
            return true;
        }
        catch
        {
            // Kullanıcı UAC penceresini reddetti
            return false;
        }
    }

    /// <summary>
    /// Mobil sunucuyu durdurur.
    /// </summary>
    public static void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _listener = null;
            IsRunning = false;
            LogReceived?.Invoke("Mobil Barkod Sunucusu durduruldu.");
        }
        catch { }
    }

    /// <summary>
    /// Bilgisayarın yerel ağdaki (Wi-Fi / Ethernet) IPv4 adreslerini döner.
    /// </summary>
    public static List<string> GetLocalIpAddresses()
    {
        var list = new List<string>();
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    list.Add(ip.ToString());
                }
            }
        }
        catch { }

        if (list.Count == 0) list.Add("127.0.0.1");
        return list;
    }

    /// <summary>
    /// Telefondan açılabilecek birincil URL adresini döner.
    /// </summary>
    public static string GetPrimaryUrl()
    {
        var ips = GetLocalIpAddresses();
        string ip = ips.FirstOrDefault(x => !x.StartsWith("127.")) ?? "localhost";
        return $"http://{ip}:{Port}/";
    }

    private static async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context), ct);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"İstek alma hatası: {ex.Message}");
            }
        }
    }

    private static async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var res = context.Response;

        // CORS Başlıkları
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            res.StatusCode = 200;
            res.Close();
            return;
        }

        string rawUrl = req.RawUrl ?? "/";
        string path = rawUrl.Split('?')[0].ToLowerInvariant();

        try
        {
            if (path == "/" || path == "/index.html" || path == "/scanner")
            {
                await ServeScannerAppAsync(res);
            }
            else if (path == "/api/product")
            {
                await HandleGetProductApiAsync(req, res);
            }
            else if (path == "/api/send-desktop")
            {
                await HandleSendDesktopApiAsync(req, res);
            }
            else if (path == "/api/ping")
            {
                await WriteJsonAsync(res, new { status = "ok", app = "BilgeStok", version = "2.1.0" });
            }
            else
            {
                res.StatusCode = 404;
                await WriteTextAsync(res, "404 - Sayfa Bulunamadı");
            }
        }
        catch (Exception ex)
        {
            try
            {
                res.StatusCode = 500;
                await WriteJsonAsync(res, new { success = false, error = ex.Message });
            }
            catch { }
        }
    }

    private static async Task HandleGetProductApiAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        string? query = req.QueryString["barcode"] ?? req.QueryString["q"];
        if (string.IsNullOrWhiteSpace(query))
        {
            await WriteJsonAsync(res, new { success = false, message = "Barkod veya arama terimi belirtilmedi." });
            return;
        }

        query = query.Trim();

        try
        {
            var dt = Database.Query(@"
SELECT TOP 1 
    p.Id, p.Code, p.Barcode, p.Name, p.Category, p.Unit, 
    p.OpeningStock, p.PurchasePrice, p.SalePrice, p.WholesalePrice, p.SpecialPrice,
    p.VatPercent, p.MinStockLevel, p.ExpiryDate, p.BatchNumber,
    COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Gelen', 'İade Giriş')), 0) AS TotalIn,
    COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış')), 0) AS TotalOut
FROM Products p
WHERE p.IsActive = 1 AND (p.Barcode = @q OR p.Code = @q OR p.Name LIKE @like)
ORDER BY CASE WHEN p.Barcode = @q THEN 0 WHEN p.Code = @q THEN 1 ELSE 2 END, p.Id DESC;",
                ("@q", query),
                ("@like", $"%{query}%")
            );

            if (dt.Rows.Count == 0)
            {
                await WriteJsonAsync(res, new { success = false, message = $"'{query}' bilgisiyle eşleşen ürün bulunamadı." });
                return;
            }

            var r = dt.Rows[0];
            long pid = Convert.ToInt64(r["Id"]);
            double openStock = Convert.ToDouble(r["OpeningStock"]);
            double totalIn = Convert.ToDouble(r["TotalIn"]);
            double totalOut = Convert.ToDouble(r["TotalOut"]);
            double currentStock = openStock + totalIn - totalOut;
            double minStock = Convert.ToDouble(r["MinStockLevel"]);
            string? expStr = r["ExpiryDate"]?.ToString();

            // Depo dağılımı
            var dtWh = Database.Query(@"
SELECT w.Name AS WhName, 
       COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen', 'İade Giriş') THEN sm.Quantity 
                         WHEN sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış') THEN -sm.Quantity 
                         ELSE 0 END), 0) AS Qty
FROM Warehouses w
LEFT JOIN StockMovements sm ON sm.WarehouseId = w.Id AND sm.ProductId = @pid
WHERE w.IsActive = 1
GROUP BY w.Id, w.Name
HAVING COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen', 'İade Giriş') THEN sm.Quantity 
                         WHEN sm.MovementType IN ('Satış', 'Satılan', 'Fire', 'Transfer Çıkış') THEN -sm.Quantity 
                         ELSE 0 END), 0) > 0;",
                ("@pid", pid)
            );

            var whList = new List<object>();
            foreach (DataRow whRow in dtWh.Rows)
            {
                whList.Add(new
                {
                    warehouse = whRow["WhName"]?.ToString() ?? "",
                    quantity = Convert.ToDouble(whRow["Qty"])
                });
            }

            int? daysToExpiry = null;
            if (!string.IsNullOrWhiteSpace(expStr) && DateTime.TryParse(expStr, out var expDt))
            {
                daysToExpiry = (int)(expDt.Date - DateTime.Today).TotalDays;
            }

            var result = new
            {
                success = true,
                product = new
                {
                    id = pid,
                    code = r["Code"]?.ToString() ?? "",
                    barcode = r["Barcode"]?.ToString() ?? "",
                    name = r["Name"]?.ToString() ?? "",
                    category = r["Category"]?.ToString() ?? "Genel",
                    unit = r["Unit"]?.ToString() ?? "Adet",
                    currentStock = currentStock,
                    minStockLevel = minStock,
                    purchasePrice = Convert.ToDouble(r["PurchasePrice"]),
                    salePrice = Convert.ToDouble(r["SalePrice"]),
                    wholesalePrice = Convert.ToDouble(r["WholesalePrice"]),
                    specialPrice = Convert.ToDouble(r["SpecialPrice"]),
                    vatPercent = Convert.ToDouble(r["VatPercent"]),
                    expiryDate = expStr,
                    daysToExpiry = daysToExpiry,
                    batchNumber = r["BatchNumber"]?.ToString() ?? "",
                    warehouses = whList
                }
            };

            await WriteJsonAsync(res, result);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(res, new { success = false, message = "Ürün sorgulama hatası: " + ex.Message });
        }
    }

    private static async Task HandleSendDesktopApiAsync(HttpListenerRequest req, HttpListenerResponse res)
    {
        string? barcode = req.QueryString["barcode"];
        string? device = req.QueryString["device"] ?? "Mobil Tarayıcı";

        if (req.HttpMethod == "POST" && req.HasEntityBody)
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            string body = await reader.ReadToEndAsync();
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("barcode", out var bProp))
                    barcode = bProp.GetString();
                if (doc.RootElement.TryGetProperty("device", out var dProp))
                    device = dProp.GetString();
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            await WriteJsonAsync(res, new { success = false, message = "Barkod boş olamaz." });
            return;
        }

        barcode = barcode.Trim();
        var prod = ProductService.GetByCode(barcode);

        var logItem = new MobileScanLogItem
        {
            Timestamp = DateTime.Now,
            Barcode = barcode,
            ProductName = prod?.Name ?? "(Bilinmeyen Ürün)",
            Stock = prod?.CurrentStock ?? 0,
            Price = prod?.SalePrice ?? 0,
            DeviceInfo = device ?? "Mobil"
        };

        lock (RecentScans)
        {
            RecentScans.Insert(0, logItem);
            if (RecentScans.Count > 100) RecentScans.RemoveAt(RecentScans.Count - 1);
        }

        BarcodeScannedFromMobile?.Invoke(barcode, prod);

        await WriteJsonAsync(res, new
        {
            success = true,
            message = $"'{barcode}' barkodu masaüstüne iletildi.",
            productName = prod?.Name
        });
    }

    private static async Task WriteJsonAsync(HttpListenerResponse res, object data)
    {
        res.ContentType = "application/json; charset=utf-8";
        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    private static async Task WriteTextAsync(HttpListenerResponse res, string text)
    {
        res.ContentType = "text/plain; charset=utf-8";
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    private static async Task ServeScannerAppAsync(HttpListenerResponse res)
    {
        res.ContentType = "text/html; charset=utf-8";
        string html = GetMobileScannerHtml();
        byte[] bytes = Encoding.UTF8.GetBytes(html);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes);
        res.Close();
    }

    private static string GetMobileScannerHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <meta name=""apple-mobile-web-app-capable"" content=""yes"">
    <meta name=""apple-mobile-web-app-status-bar-style"" content=""black-translucent"">
    <title>Bilensis Mobil Canlı Barkod Okuyucu</title>
    <script src=""https://unpkg.com/@zxing/library@latest/umd/index.min.js""></script>
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: #0f172a;
            color: #f8fafc;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            overflow-x: hidden;
        }
        header {
            background: linear-gradient(135deg, #1e293b, #0f172a);
            padding: 12px 16px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            border-bottom: 1px solid #334155;
            position: sticky;
            top: 0;
            z-index: 50;
        }
        .brand { display: flex; align-items: center; gap: 8px; font-weight: 700; font-size: 1.05rem; }
        .badge-live {
            background: #10b981;
            color: #fff;
            padding: 3px 8px;
            border-radius: 9999px;
            font-size: 0.7rem;
            font-weight: 600;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }
        .badge-live::before {
            content: '';
            width: 6px;
            height: 6px;
            background: #fff;
            border-radius: 50%;
            animation: pulse 1.5s infinite;
        }
        @keyframes pulse { 0% { opacity: 1; transform: scale(1); } 50% { opacity: 0.4; transform: scale(1.3); } 100% { opacity: 1; transform: scale(1); } }
        
        .camera-container {
            position: relative;
            width: 100%;
            height: 48vh;
            background: #000;
            overflow: hidden;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        video {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }
        .scanner-overlay {
            position: absolute;
            top: 0; left: 0; right: 0; bottom: 0;
            pointer-events: none;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .reticle {
            width: 78%;
            max-width: 320px;
            height: 180px;
            border: 2px solid rgba(255, 255, 255, 0.4);
            border-radius: 12px;
            position: relative;
            box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.5);
        }
        .reticle::before {
            content: '';
            position: absolute;
            left: 0; right: 0;
            height: 2px;
            background: #10b981;
            box-shadow: 0 0 8px #10b981;
            animation: scanLine 2s infinite ease-in-out;
        }
        @keyframes scanLine {
            0% { top: 5%; opacity: 0.8; }
            50% { top: 90%; opacity: 1; }
            100% { top: 5%; opacity: 0.8; }
        }
        .reticle-corner {
            position: absolute;
            width: 16px;
            height: 16px;
            border-color: #10b981;
            border-style: solid;
        }
        .c-tl { top: -2px; left: -2px; border-width: 3px 0 0 3px; border-top-left-radius: 6px; }
        .c-tr { top: -2px; right: -2px; border-width: 3px 3px 0 0; border-top-right-radius: 6px; }
        .c-bl { bottom: -2px; left: -2px; border-width: 0 0 3px 3px; border-bottom-left-radius: 6px; }
        .c-br { bottom: -2px; right: -2px; border-width: 0 3px 3px 0; border-bottom-right-radius: 6px; }

        .cam-controls {
            position: absolute;
            bottom: 12px;
            display: flex;
            gap: 12px;
            z-index: 10;
        }
        .btn-ctrl {
            background: rgba(15, 23, 42, 0.85);
            border: 1px solid #475569;
            color: #fff;
            padding: 8px 14px;
            border-radius: 20px;
            font-size: 0.82rem;
            backdrop-filter: blur(8px);
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 6px;
        }
        .btn-ctrl:active { transform: scale(0.96); }

        .manual-bar {
            padding: 10px 14px;
            background: #1e293b;
            display: flex;
            gap: 8px;
            border-bottom: 1px solid #334155;
        }
        .manual-bar input {
            flex: 1;
            background: #0f172a;
            border: 1px solid #475569;
            color: #fff;
            padding: 10px 14px;
            border-radius: 8px;
            font-size: 0.95rem;
            outline: none;
        }
        .manual-bar input:focus { border-color: #14b8c4; }
        .manual-bar button {
            background: #14b8c4;
            color: #fff;
            border: none;
            padding: 0 16px;
            border-radius: 8px;
            font-weight: 600;
            cursor: pointer;
        }

        .content-area {
            flex: 1;
            padding: 14px;
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        /* Ürün Detay Kartı */
        .card-product {
            background: #1e293b;
            border-radius: 14px;
            padding: 16px;
            border: 1px solid #334155;
            box-shadow: 0 4px 16px rgba(0,0,0,0.3);
            display: none;
            animation: slideUp 0.3s ease-out;
        }
        @keyframes slideUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }

        .prod-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 8px; }
        .prod-name { font-size: 1.15rem; font-weight: 700; color: #f8fafc; line-height: 1.3; }
        .prod-cat {
            background: #334155;
            padding: 3px 8px;
            border-radius: 6px;
            font-size: 0.75rem;
            color: #cbd5e1;
            white-space: nowrap;
        }
        .prod-code { font-size: 0.8rem; color: #94a3b8; margin-top: 4px; }

        .stock-banner {
            margin-top: 12px;
            padding: 12px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .stock-ok { background: rgba(16, 185, 129, 0.15); border: 1px solid #10b981; color: #34d399; }
        .stock-crit { background: rgba(245, 158, 11, 0.15); border: 1px solid #f59e0b; color: #fbbf24; }
        .stock-zero { background: rgba(239, 68, 68, 0.15); border: 1px solid #ef4444; color: #f87171; }
        .stock-val { font-size: 1.35rem; font-weight: 800; }

        .prices-grid {
            margin-top: 12px;
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
        }
        .price-box {
            background: #0f172a;
            border: 1px solid #334155;
            padding: 10px;
            border-radius: 8px;
            text-align: right;
        }
        .price-label { font-size: 0.72rem; color: #94a3b8; text-transform: uppercase; margin-bottom: 2px; }
        .price-val { font-size: 1.15rem; font-weight: 700; }
        .price-sale { color: #10b981; }
        .price-ws { color: #2dd4d4; }

        .extra-info {
            margin-top: 12px;
            padding-top: 10px;
            border-top: 1px dashed #334155;
            font-size: 0.82rem;
            display: flex;
            flex-direction: column;
            gap: 6px;
        }
        .info-row { display: flex; justify-content: space-between; color: #cbd5e1; }
        .info-badge-exp {
            background: #fef3c7;
            color: #92400e;
            padding: 2px 6px;
            border-radius: 4px;
            font-weight: 600;
        }
        .info-badge-expired {
            background: #fee2e2;
            color: #991b1b;
            padding: 2px 6px;
            border-radius: 4px;
            font-weight: 600;
        }

        .btn-desktop {
            margin-top: 14px;
            width: 100%;
            background: linear-gradient(135deg, #14b8c4, #0e9aa7);
            color: #fff;
            padding: 13px;
            border: none;
            border-radius: 10px;
            font-size: 1rem;
            font-weight: 700;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            cursor: pointer;
            box-shadow: 0 4px 12px rgba(14, 154, 167, 0.4);
        }
        .btn-desktop:active { transform: scale(0.98); }

        .placeholder-box {
            text-align: center;
            padding: 24px 16px;
            color: #64748b;
        }
        .placeholder-icon { font-size: 2.5rem; margin-bottom: 8px; opacity: 0.7; }
    </style>
</head>
<body>
    <header>
        <div class=""brand"">
            <span>📦 Bilensis Mobil</span>
        </div>
        <div class=""badge-live"">Canlı Kamera</div>
    </header>

    <div class=""camera-container"">
        <video id=""camVideo"" autoplay playsinline muted></video>
        <div class=""scanner-overlay"">
            <div class=""reticle"">
                <div class=""reticle-corner c-tl""></div>
                <div class=""reticle-corner c-tr""></div>
                <div class=""reticle-corner c-bl""></div>
                <div class=""reticle-corner c-br""></div>
            </div>
        </div>
        <div class=""cam-controls"">
            <button class=""btn-ctrl"" id=""btnTorch"" onclick=""toggleTorch()"">💡 Fener</button>
            <button class=""btn-ctrl"" onclick=""switchCamera()"">🔄 Çevir</button>
        </div>
    </div>

    <div class=""manual-bar"">
        <input type=""text"" id=""txtManual"" placeholder=""Barkod no veya ürün adı..."" enterkeyhint=""search"" onkeydown=""if(event.key==='Enter') searchManual()"">
        <button onclick=""searchManual()"">Ara</button>
    </div>

    <div class=""content-area"">
        <div class=""card-product"" id=""cardProduct"">
            <div class=""prod-header"">
                <div>
                    <div class=""prod-name"" id=""lblProdName""></div>
                    <div class=""prod-code"" id=""lblProdCode""></div>
                </div>
                <div class=""prod-cat"" id=""lblProdCat""></div>
            </div>

            <div class=""stock-banner stock-ok"" id=""pnlStock"">
                <div>
                    <div style=""font-size:0.75rem;opacity:0.9;"">MEVCUT DEPO STOĞU</div>
                    <div style=""font-size:0.8rem;"" id=""lblWhDist""></div>
                </div>
                <div class=""stock-val"" id=""lblProdStock""></div>
            </div>

            <div class=""prices-grid"">
                <div class=""price-box"">
                    <div class=""price-label"">Perakende Fiyat</div>
                    <div class=""price-val price-sale"" id=""lblSalePrice""></div>
                </div>
                <div class=""price-box"">
                    <div class=""price-label"">Toptan Fiyat</div>
                    <div class=""price-val price-ws"" id=""lblWholesalePrice""></div>
                </div>
            </div>

            <div class=""extra-info"">
                <div class=""info-row"">
                    <span>Alış Maliyeti (KDV Hariç):</span>
                    <strong id=""lblPurchasePrice""></strong>
                </div>
                <div class=""info-row"" id=""rowExpiry"">
                    <span>Son Kullanma Tarihi (SKT):</span>
                    <strong id=""lblExpiry""></strong>
                </div>
                <div class=""info-row"" id=""rowBatch"">
                    <span>Parti / Lot Numarası:</span>
                    <strong id=""lblBatch""></strong>
                </div>
            </div>

            <button class=""btn-desktop"" onclick=""sendToDesktop()"">
                💻 Masaüstüne İlet & Seçili Yap
            </button>
        </div>

        <div class=""placeholder-box"" id=""pnlPlaceholder"">
            <div class=""placeholder-icon"">📷</div>
            <p>Kameranızı reyondaki veya depodaki ürünün barkoduna tutun.</p>
            <p style=""font-size:0.8rem;margin-top:4px;"">Barkod okunduğu anda stok ve fiyat bilgileri otomatik ekrana gelecektir.</p>
        </div>
    </div>

    <script>
        let currentStream = null;
        let currentFacing = 'environment';
        let isTorchOn = false;
        let lastScannedCode = null;
        let lastScanTime = 0;
        let codeReader = null;
        let currentProductData = null;

        function playBeep() {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.type = 'sine';
                osc.frequency.setValueAtTime(1850, ctx.currentTime);
                gain.gain.setValueAtTime(0.3, ctx.currentTime);
                osc.start();
                osc.stop(ctx.currentTime + 0.11);
            } catch(e){}
            if (navigator.vibrate) navigator.vibrate(80);
        }

        async function initCamera() {
            try {
                if (currentStream) {
                    currentStream.getTracks().forEach(t => t.stop());
                }
                const constraints = {
                    video: {
                        facingMode: { ideal: currentFacing },
                        width: { ideal: 1280 },
                        height: { ideal: 720 }
                    }
                };
                currentStream = await navigator.mediaDevices.getUserMedia(constraints);
                const video = document.getElementById('camVideo');
                video.srcObject = currentStream;

                startBarcodeScanning(video);
            } catch (err) {
                alert('Kamera açılamadı: ' + err.message + '\nLütfen tarayıcınızın kamera iznini kontrol ediniz.');
            }
        }

        function startBarcodeScanning(video) {
            if ('BarcodeDetector' in window) {
                const detector = new BarcodeDetector({ formats: ['ean_13', 'ean_8', 'code_128', 'code_39', 'upc_a', 'upc_e', 'qr_code'] });
                const scanLoop = async () => {
                    if (video.readyState >= 2) {
                        try {
                            const barcodes = await detector.detect(video);
                            if (barcodes.length > 0) {
                                handleDetectedBarcode(barcodes[0].rawValue);
                            }
                        } catch(e){}
                    }
                    requestAnimationFrame(scanLoop);
                };
                scanLoop();
            } else if (window.ZXing) {
                if (!codeReader) codeReader = new ZXing.BrowserMultiFormatReader();
                codeReader.decodeFromVideoElement(video, (result, err) => {
                    if (result) {
                        handleDetectedBarcode(result.getText());
                    }
                });
            }
        }

        function handleDetectedBarcode(code) {
            const now = Date.now();
            if (code === lastScannedCode && (now - lastScanTime) < 2500) {
                return;
            }
            lastScannedCode = code;
            lastScanTime = now;
            playBeep();
            fetchProduct(code);
        }

        async function fetchProduct(query) {
            try {
                const res = await fetch('/api/product?barcode=' + encodeURIComponent(query));
                const data = await res.json();
                if (data.success && data.product) {
                    displayProduct(data.product);
                } else {
                    alert(data.message || 'Ürün bulunamadı.');
                }
            } catch(e) {
                alert('Sunucu iletişim hatası: ' + e.message);
            }
        }

        function displayProduct(p) {
            currentProductData = p;
            document.getElementById('lblProdName').innerText = p.name;
            document.getElementById('lblProdCode').innerText = 'Kod: ' + p.code + (p.barcode ? ' | Barkod: ' + p.barcode : '');
            document.getElementById('lblProdCat').innerText = p.category;

            const stockPnl = document.getElementById('pnlStock');
            stockPnl.className = 'stock-banner ' + (p.currentStock <= 0 ? 'stock-zero' : (p.currentStock <= p.minStockLevel ? 'stock-crit' : 'stock-ok'));
            document.getElementById('lblProdStock').innerText = p.currentStock.toLocaleString('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 2 }) + ' ' + p.unit;

            if (p.warehouses && p.warehouses.length > 0) {
                const whTexts = p.warehouses.map(w => w.warehouse + ': ' + w.quantity + ' ' + p.unit);
                document.getElementById('lblWhDist').innerText = whTexts.join(' • ');
            } else {
                document.getElementById('lblWhDist').innerText = 'Genel Stok';
            }

            document.getElementById('lblSalePrice').innerText = p.salePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ₺';
            document.getElementById('lblWholesalePrice').innerText = p.wholesalePrice > 0 ? p.wholesalePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ₺' : '-';
            document.getElementById('lblPurchasePrice').innerText = p.purchasePrice.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) + ' ₺';

            const rowExp = document.getElementById('rowExpiry');
            if (p.expiryDate) {
                rowExp.style.display = 'flex';
                let expBadge = '';
                if (p.daysToExpiry !== null) {
                    if (p.daysToExpiry < 0) {
                        expBadge = '<span class=""info-badge-expired"">⛔ Günü Geçti (' + Math.abs(p.daysToExpiry) + 'g)</span>';
                    } else if (p.daysToExpiry <= 30) {
                        expBadge = '<span class=""info-badge-exp"">⚠️ ' + p.daysToExpiry + ' Gün Kaldı</span>';
                    }
                }
                document.getElementById('lblExpiry').innerHTML = p.expiryDate + ' ' + expBadge;
            } else {
                rowExp.style.display = 'none';
            }

            const rowBatch = document.getElementById('rowBatch');
            if (p.batchNumber) {
                rowBatch.style.display = 'flex';
                document.getElementById('lblBatch').innerText = p.batchNumber;
            } else {
                rowBatch.style.display = 'none';
            }

            document.getElementById('cardProduct').style.display = 'block';
            document.getElementById('pnlPlaceholder').style.display = 'none';
        }

        async function sendToDesktop() {
            if (!currentProductData) return;
            try {
                const res = await fetch('/api/send-desktop?barcode=' + encodeURIComponent(currentProductData.barcode || currentProductData.code));
                const data = await res.json();
                if (data.success) {
                    alert('✅ Masaüstüne başarıyla aktarıldı!');
                }
            } catch(e) {
                alert('İletim hatası: ' + e.message);
            }
        }

        function searchManual() {
            const val = document.getElementById('txtManual').value.trim();
            if (val) fetchProduct(val);
        }

        async function toggleTorch() {
            if (!currentStream) return;
            const track = currentStream.getVideoTracks()[0];
            if (track) {
                const caps = track.getCapabilities ? track.getCapabilities() : {};
                if (caps.torch) {
                    isTorchOn = !isTorchOn;
                    await track.applyConstraints({ advanced: [{ torch: isTorchOn }] });
                    document.getElementById('btnTorch').innerText = isTorchOn ? '🔦 Fener Açık' : '💡 Fener';
                } else {
                    alert('Cihazınızda fener özelliği desteklenmiyor.');
                }
            }
        }

        function switchCamera() {
            currentFacing = currentFacing === 'environment' ? 'user' : 'environment';
            initCamera();
        }

        window.addEventListener('load', initCamera);
    </script>
</body>
</html>";
    }
}
