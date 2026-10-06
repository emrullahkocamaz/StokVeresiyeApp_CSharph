using System.Data;
using System.Drawing;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ZXing;
using ZXing.Windows.Compatibility;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;
using TgMessage = Telegram.Bot.Types.Message;
using TgUser = Telegram.Bot.Types.User;

namespace StokVeresiyeApp.Services;

public static class TelegramBotService
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp");
    private static readonly string ConfigPath = Path.Combine(Folder, "telegram_config.json");

    private static TelegramConfig _config = new();
    private static ITelegramBotClient? _botClient;
    private static CancellationTokenSource? _cts;
    private static TgUser? _botUser;

    public static TelegramConfig Config => _config;
    public static bool IsRunning => _cts != null && !_cts.IsCancellationRequested && _botClient != null;
    public static string BotUsername => _botUser?.Username ?? "";
    public static string BotFirstName => _botUser?.FirstName ?? "";

    public static event Action<bool>? StatusChanged;
    public static event Action<string>? LogReceived;

    static TelegramBotService()
    {
        LoadConfig();
    }

    public static void LoadConfig()
    {
        try
        {
            if (System.IO.File.Exists(ConfigPath))
            {
                var json = System.IO.File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<TelegramConfig>(json);
                if (loaded != null) _config = loaded;
            }
        }
        catch { }
    }

    public static void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Program açılışında kayıtlı yapılandırmaya göre botu başlatır.
    /// </summary>
    public static async Task AutoStartIfEnabledAsync()
    {
        if (_config.IsEnabled && !string.IsNullOrWhiteSpace(_config.BotToken))
        {
            await StartBotAsync(_config.BotToken);
        }
    }

    /// <summary>
    /// Botu verilen Token ile başlatır.
    /// </summary>
    public static async Task<(bool Success, string Message)> StartBotAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Bot Token boş bırakılamaz.");

        StopBot();

        try
        {
            _botClient = new TelegramBotClient(token.Trim());
            _cts = new CancellationTokenSource();

            // Bot bilgilerini doğrula
            _botUser = await _botClient.GetMe(_cts.Token);

            _config.BotToken = token.Trim();
            _config.IsEnabled = true;
            SaveConfig();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
                DropPendingUpdates = true
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                _cts.Token
            );

            StatusChanged?.Invoke(true);
            LogReceived?.Invoke($"Bot başarıyla başlatıldı: @{_botUser.Username} ({_botUser.FirstName})");
            return (true, $"Bot başarıyla bağlandı!\nKullanıcı Adı: @{_botUser.Username}");
        }
        catch (Exception ex)
        {
            StopBot();
            LogReceived?.Invoke($"Bot başlatma hatası: {ex.Message}");
            return (false, $"Telegram bağlantı hatası: {ex.Message}");
        }
    }

    /// <summary>
    /// Botu durdurur.
    /// </summary>
    public static void StopBot()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch { }
        finally
        {
            _cts = null;
            _botClient = null;
            _botUser = null;
            StatusChanged?.Invoke(false);
            LogReceived?.Invoke("Bot durduruldu.");
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        LogReceived?.Invoke($"Telegram Hata: {exception.Message}");
        return Task.CompletedTask;
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { } message) return;

        long chatId = message.Chat.Id;
        string fromUser = message.From?.Username ?? message.From?.FirstName ?? "Kullanıcı";

        // Yetkilendirme kontrolü
        if (_config.AuthorizedChatId == 0)
        {
            // Henüz yetkili atanmamış, bu kişiyi yetkilendirme seçeneği sun
            if (message.Text != null && message.Text.StartsWith("/bagla", StringComparison.OrdinalIgnoreCase))
            {
                _config.AuthorizedChatId = chatId;
                _config.AuthorizedUserName = fromUser;
                SaveConfig();

                await bot.SendMessage(
                    chatId,
                    $"✅ *Tebrikler!* Telefonunuz Bilensis sistemine yönetici olarak başarıyla bağlandı.\n\n" +
                    $"Artık anlık kasa durumunu, kritik stokları sorgulayabilir ve fotoğrafla barkod okutabilirsiniz.",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: GetMainMenuKeyboard(),
                    cancellationToken: ct
                );
                return;
            }

            await bot.SendMessage(
                chatId,
                $"🔒 *Bilensis - Güvenlik Uyarısı*\n\n" +
                $"Bu bot işletmenizin ticari veritabanına bağlıdır.\n" +
                $"Sizin Chat ID'niz: `{chatId}`\n\n" +
                $"Bu telefonu sisteme yetkili yönetici olarak kaydetmek için lütfen bu bota `/bagla` yazıp gönderiniz veya masaüstü uygulamada Telegram Ayarları ekranına bu ID'yi yazınız.",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct
            );
            return;
        }

        // Yetkisiz kullanıcı engelleme
        if (_config.AuthorizedChatId != chatId)
        {
            await bot.SendMessage(
                chatId,
                "⛔ *Erişim Reddedildi*\nBu bota erişim yetkiniz bulunmamaktadır.",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct
            );
            return;
        }

        // 1. FOTOĞRAF GELMİŞSE (BARKOD OKUMA)
        if (message.Photo != null && message.Photo.Length > 0)
        {
            await HandlePhotoBarcodeAsync(bot, message, ct);
            return;
        }

        // 2. METİN KOMUTLARI
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            await HandleTextCommandAsync(bot, message, ct);
        }
    }

    /// <summary>
    /// Telegram'a gönderilen fotoğraftan barkodu çözüp ürünü arar.
    /// </summary>
    private static async Task HandlePhotoBarcodeAsync(ITelegramBotClient bot, TgMessage message, CancellationToken ct)
    {
        long chatId = message.Chat.Id;

        try
        {
            await bot.SendMessage(chatId, "🔍 Fotoğraf taranıyor, barkod aranıyor...", cancellationToken: ct);

            // En yüksek çözünürlüklü fotoğrafı al
            var photo = message.Photo!.Last();
            var file = await bot.GetFile(photo.FileId, ct);

            if (string.IsNullOrWhiteSpace(file.FilePath))
            {
                await bot.SendMessage(chatId, "❌ Fotoğraf Telegram sunucusundan indirilemedi.", cancellationToken: ct);
                return;
            }

            using var ms = new MemoryStream();
            await bot.DownloadFile(file.FilePath, ms, ct);
            ms.Position = 0;

            // ZXing.Net ile barkodu çöz
            using var bitmap = new Bitmap(ms);
            var reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.QR_CODE
                    }
                }
            };

            var result = reader.Decode(bitmap);

            if (result == null || string.IsNullOrWhiteSpace(result.Text))
            {
                await bot.SendMessage(
                    chatId,
                    "⚠️ *Barkod Algılanamadı*\n\n" +
                    "Fotoğrafta net bir barkod çizgisi tespit edilemedi.\n\n" +
                    "💡 *İpuçları:*\n" +
                    "• Kamerayı barkoda yaklaştırıp odaklayın.\n" +
                    "• Parlama veya gölge olmamasına dikkat edin.\n" +
                    "• Dilerseniz barkod numarasını bota doğrudan mesaj olarak yazabilirsiniz.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
                return;
            }

            string barcode = result.Text.Trim();
            await FindAndSendProductDetailAsync(bot, chatId, barcode, $"✅ *Barkod Okundu:* `{barcode}` ({result.BarcodeFormat})", ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Barkod okuma hatası: {ex.Message}", cancellationToken: ct);
        }
    }

    /// <summary>
    /// Barkod veya isme göre ürünü veritabanında arayıp detay kartını gönderir.
    /// </summary>
    private static async Task FindAndSendProductDetailAsync(ITelegramBotClient bot, long chatId, string query, string? headerText, CancellationToken ct)
    {
        try
        {
            var dt = Database.Query(@"
SELECT TOP 1 
    p.Id, p.Code, p.Barcode, p.Name, p.Category, p.Unit, 
    p.OpeningStock, p.PurchasePrice, p.SalePrice, p.WholesalePrice, p.SpecialPrice,
    p.VatPercent, p.MinStockLevel,
    COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Gelen', 'İade Giriş')), 0) AS TotalIn,
    COALESCE((SELECT SUM(sm.Quantity) FROM StockMovements sm WHERE sm.ProductId = p.Id AND sm.MovementType IN ('Satış', 'Fire', 'Transfer Çıkış')), 0) AS TotalOut
FROM Products p
WHERE p.IsActive = 1 AND (p.Barcode = @q OR p.Code = @q OR p.Name LIKE @like)
ORDER BY CASE WHEN p.Barcode = @q THEN 0 WHEN p.Code = @q THEN 1 ELSE 2 END, p.Id DESC;",
                ("@q", query),
                ("@like", $"%{query}%")
            );

            if (dt.Rows.Count == 0)
            {
                string msg = !string.IsNullOrWhiteSpace(headerText) ? $"{headerText}\n\n" : "";
                msg += $"⚠️ `{query}` bilgisiyle eşleşen kayıtlı bir ürün bulunamadı.";
                await bot.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
                return;
            }

            var r = dt.Rows[0];
            long pid = Convert.ToInt64(r["Id"]);
            string code = r["Code"]?.ToString() ?? "";
            string barcode = r["Barcode"]?.ToString() ?? "-";
            string name = r["Name"]?.ToString() ?? "";
            string category = r["Category"]?.ToString() ?? "Genel";
            string unit = r["Unit"]?.ToString() ?? "Adet";
            double purchase = Convert.ToDouble(r["PurchasePrice"]);
            double sale = Convert.ToDouble(r["SalePrice"]);
            double wholesale = Convert.ToDouble(r["WholesalePrice"]);
            double openStock = Convert.ToDouble(r["OpeningStock"]);
            double totalIn = Convert.ToDouble(r["TotalIn"]);
            double totalOut = Convert.ToDouble(r["TotalOut"]);
            double remaining = openStock + totalIn - totalOut;
            double minStock = Convert.ToDouble(r["MinStockLevel"]);

            // Depo dağılımını çek
            var dtWh = Database.Query(@"
SELECT w.Name AS WhName, 
       COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen', 'İade Giriş') THEN sm.Quantity 
                         WHEN sm.MovementType IN ('Satış', 'Fire', 'Transfer Çıkış') THEN -sm.Quantity 
                         ELSE 0 END), 0) AS Qty
FROM Warehouses w
LEFT JOIN StockMovements sm ON sm.WarehouseId = w.Id AND sm.ProductId = @pid
WHERE w.IsActive = 1
GROUP BY w.Id, w.Name
HAVING COALESCE(SUM(CASE WHEN sm.MovementType IN ('Gelen', 'İade Giriş') THEN sm.Quantity 
                         WHEN sm.MovementType IN ('Satış', 'Fire', 'Transfer Çıkış') THEN -sm.Quantity 
                         ELSE 0 END), 0) > 0;",
                ("@pid", pid)
            );

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(headerText))
            {
                sb.AppendLine(headerText);
                sb.AppendLine();
            }

            sb.AppendLine($"📦 *{name}*");
            sb.AppendLine($"🏷️ *Barkod:* `{barcode}` | *Kod:* `{code}`");
            sb.AppendLine($"📂 *Kategori:* {category}");
            sb.AppendLine();

            string stockIcon = remaining <= minStock ? "🔴" : (remaining <= minStock * 2 ? "🟡" : "🟢");
            sb.AppendLine($"{stockIcon} *Mevcut Stok:* *{remaining:N2} {unit}*");
            sb.AppendLine($"⚠️ *Kritik Seviye:* {minStock:N2} {unit}");
            sb.AppendLine();

            sb.AppendLine($"💰 *Perakende Fiyat:* *{sale:N2} ₺*");
            if (wholesale > 0) sb.AppendLine($"💼 *Toptan Fiyat:* {wholesale:N2} ₺");
            sb.AppendLine($"💵 *Son Alış Fiyatı:* {purchase:N2} ₺ (KDV Hariç)");

            if (dtWh.Rows.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("🏢 *Depo Dağılımı:*");
                foreach (DataRow wr in dtWh.Rows)
                {
                    sb.AppendLine($"• {wr["WhName"]}: {Convert.ToDouble(wr["Qty"]):N2} {unit}");
                }
            }

            await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Ürün sorgu hatası: {ex.Message}", cancellationToken: ct);
        }
    }

    /// <summary>
    /// Metin tabanlı komutları işler.
    /// </summary>
    private static async Task HandleTextCommandAsync(ITelegramBotClient bot, TgMessage message, CancellationToken ct)
    {
        long chatId = message.Chat.Id;
        string text = message.Text!.Trim();

        // 1. Menü & Başlangıç
        if (text is "/start" or "/menu" or "/yardim" or "/help")
        {
            string welcome = 
                $"👋 *Bilensis Yönetici Asistanına Hoş Geldiniz!*\n\n" +
                $"Aşağıdaki hızlı butonları kullanarak dükkanınızın durumunu anlık takip edebilirsiniz.\n\n" +
                $"📷 *Barkod Okutma:* Ürün barkodunun fotoğrafını doğrudan bu bota atmanız yeterlidir!\n" +
                $"🔍 *İsimle Arama:* Bir ürün adı veya barkod no yazıp gönderebilirsiniz.";

            await bot.SendMessage(chatId, welcome, parseMode: ParseMode.Markdown, replyMarkup: GetMainMenuKeyboard(), cancellationToken: ct);
            return;
        }

        // 2. Kasa Durumu
        if (text.Equals("📊 Kasa Durumu", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/kasa", StringComparison.OrdinalIgnoreCase))
        {
            await SendCashSummaryAsync(bot, chatId, ct);
            return;
        }

        // 3. Kritik Stoklar
        if (text.Equals("📦 Kritik Stoklar", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/kritik", StringComparison.OrdinalIgnoreCase))
        {
            await SendCriticalStocksAsync(bot, chatId, ct);
            return;
        }

        // 4. En Çok Borçlular
        if (text.Equals("👥 En Çok Borçlular", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/borclular", StringComparison.OrdinalIgnoreCase))
        {
            await SendTopDebtorsAsync(bot, chatId, ct);
            return;
        }

        // 5. Günün Özeti
        if (text.Equals("📈 Günün Özeti", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/bugun", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/rapor", StringComparison.OrdinalIgnoreCase))
        {
            await SendDailySummaryAsync(bot, chatId, ct);
            return;
        }

        // 5.1 Canlı Mobil Barkod Okuyucu
        if (text.Equals("📱 Canlı Barkod Okuyucu (Web)", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/tara", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/mobil", StringComparison.OrdinalIgnoreCase))
        {
            string url = MobileScannerService.GetPrimaryUrl();
            string msg = 
                $"📱 *Mobil Canlı Barkod Okuyucu*\n\n" +
                $"Dükkan Wi-Fi ağına bağlıyken telefonunuzun tarayıcısından canlı kamera ile barkod okutmak için linke dokunun:\n\n" +
                $"👉 {url}\n\n" +
                $"💡 *Özellikler:*\n" +
                $"• Telefon kamerası canlı video ile barkodu okur (BİP sesiyle).\n" +
                $"• Anlık depo stoku, perakende ve toptan satış fiyatı ekrana gelir.\n" +
                $"• Varsa Son Kullanma Tarihi (SKT) ve Parti/Lot bilgisi gösterilir.\n" +
                $"• 'Masaüstüne Aktar' butonuyla bilgisayarda ürün anında seçili hale gelir.";

            await bot.SendMessage(chatId, msg, parseMode: ParseMode.Markdown, cancellationToken: ct);
            return;
        }

        // 6. Ürün Arama Komutu: /stok [kelime] veya doğrudan barkod/isim yazımı
        string query = text;
        if (text.StartsWith("/stok", StringComparison.OrdinalIgnoreCase))
        {
            query = text.Substring(5).Trim();
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            await FindAndSendProductDetailAsync(bot, chatId, query, null, ct);
        }
    }

    private static async Task SendCashSummaryAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        try
        {
            var s = DashboardService.GetSummary();
            string cashIcon = s.NetCashToday >= 0 ? "🟢" : "🔴";

            var sb = new StringBuilder();
            sb.AppendLine("📊 *GÜNCEL KASA & FİNANS DURUMU*");
            sb.AppendLine($"📅 Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine("────────────────────────");
            sb.AppendLine($"{cashIcon} *Bugünkü Net Kasa:* *{s.NetCashToday:N2} ₺*");
            sb.AppendLine($"📦 *Toplam Stok Değeri:* {s.TotalStockValue:N2} ₺ ({s.TotalStockQuantity:N0} Adet)");
            sb.AppendLine($"🟢 *Müşteri Alacakları (Veresiye):* {s.TotalCustomerReceivable:N2} ₺");
            sb.AppendLine($"🔴 *Tedarikçi Borçları:* {s.TotalSupplierPayable:N2} ₺");

            if (s.OverdueReceivableCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"⚠️ *Vadesi Geçen Alacak:* *{s.OverdueReceivableTotal:N2} ₺* ({s.OverdueReceivableCount} Müşteri)");
            }

            await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Kasa durumu alınamadı: {ex.Message}", cancellationToken: ct);
        }
    }

    private static async Task SendCriticalStocksAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        try
        {
            var dt = DashboardService.GetCriticalStockProducts();
            if (dt.Rows.Count == 0)
            {
                await bot.SendMessage(chatId, "✅ *Tüm Stoklar Yeterli!*\nKritik seviyenin altına düşen ürün bulunmuyor.", parseMode: ParseMode.Markdown, cancellationToken: ct);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"⚠️ *KRİTİK SEVİYEDEKİ ÜRÜNLER ({dt.Rows.Count} Kalem)*");
            sb.AppendLine("────────────────────────");

            int count = 0;
            foreach (DataRow r in dt.Rows)
            {
                if (++count > 15)
                {
                    sb.AppendLine($"_...ve {dt.Rows.Count - 15} ürün daha._");
                    break;
                }

                string name = r["Ürün Adı"]?.ToString() ?? "";
                double stock = Convert.ToDouble(r["Kalan Stok"]);
                double min = Convert.ToDouble(r["Kritik Seviye"]);
                string unit = r["Birim"]?.ToString() ?? "Adet";

                sb.AppendLine($"🔴 *{name}*");
                sb.AppendLine($"   Kalan: *{stock:N2}* / Kritik: {min:N2} {unit}");
            }

            await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Kritik stoklar alınamadı: {ex.Message}", cancellationToken: ct);
        }
    }

    private static async Task SendTopDebtorsAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        try
        {
            var dt = DashboardService.GetTopDebtorCustomers();
            if (dt.Rows.Count == 0)
            {
                await bot.SendMessage(chatId, "✅ Borçlu müşteri bulunmamaktadır.", cancellationToken: ct);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("👥 *EN ÇOK BORCU OLAN MÜŞTERİLER*");
            sb.AppendLine("────────────────────────");

            int idx = 1;
            foreach (DataRow r in dt.Rows)
            {
                string name = r["Müşteri"]?.ToString() ?? "";
                double balance = Convert.ToDouble(r["Borç Bakiyesi (₺)"]);
                string phone = r["Telefon"]?.ToString() ?? "";

                sb.AppendLine($"{idx++}. *{name}*");
                sb.AppendLine($"   Borç: *{balance:N2} ₺* {(string.IsNullOrWhiteSpace(phone) ? "" : "📞 " + phone)}");
            }

            await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Borçlu listesi alınamadı: {ex.Message}", cancellationToken: ct);
        }
    }

    private static async Task SendDailySummaryAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        try
        {
            string today = DateTime.Today.ToString("yyyy-MM-dd");
            var dtMov = Database.Query(@"
SELECT 
    COUNT(*) AS TotalCount,
    COALESCE(SUM(CASE WHEN TransactionType='Satış' THEN Amount ELSE 0 END), 0) AS TotalSales,
    COALESCE(SUM(CASE WHEN TransactionType='Tahsilat' THEN Amount ELSE 0 END), 0) AS TotalCollections,
    COALESCE(SUM(CASE WHEN TransactionType='Ödeme' THEN Amount ELSE 0 END), 0) AS TotalPayments
FROM AccountMovements
WHERE MovementDate >= @t;", ("@t", today));

            var r = dtMov.Rows[0];
            int count = Convert.ToInt32(r["TotalCount"]);
            double sales = Convert.ToDouble(r["TotalSales"]);
            double colls = Convert.ToDouble(r["TotalCollections"]);
            double pays = Convert.ToDouble(r["TotalPayments"]);
            double net = colls - pays;

            var sb = new StringBuilder();
            sb.AppendLine($"📈 *GÜNLÜK HAREKET ÖZETİ* ({DateTime.Today:dd.MM.yyyy})");
            sb.AppendLine("────────────────────────");
            sb.AppendLine($"📝 *Bugünkü İşlem Sayısı:* {count} Adet");
            sb.AppendLine($"🛍️ *Bugünkü Satış Tutarı:* {sales:N2} ₺");
            sb.AppendLine($"💵 *Bugünkü Nakit/Kart Tahsilat:* +{colls:N2} ₺");
            sb.AppendLine($"💸 *Bugünkü Kasa Çıkışı / Ödeme:* -{pays:N2} ₺");
            sb.AppendLine("────────────────────────");
            sb.AppendLine($"{(net >= 0 ? "🟢" : "🔴")} *Günlük Net Kasa Değişimi:* *{net:N2} ₺*");

            await bot.SendMessage(chatId, sb.ToString(), parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendMessage(chatId, $"❌ Günlük özet alınamadı: {ex.Message}", cancellationToken: ct);
        }
    }

    /// <summary>
    /// Telegram'da klavye altındaki hızlı butonları oluşturur.
    /// </summary>
    private static ReplyKeyboardMarkup GetMainMenuKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📊 Kasa Durumu"), new KeyboardButton("📦 Kritik Stoklar") },
            new[] { new KeyboardButton("👥 En Çok Borçlular"), new KeyboardButton("📈 Günün Özeti") },
            new[] { new KeyboardButton("📱 Canlı Barkod Okuyucu (Web)") }
        })
        {
            ResizeKeyboard = true
        };
    }

    /// <summary>
    /// Yönetici telefonuna sistem uyarısı gönderir.
    /// </summary>
    public static async Task SendAlertToAdminAsync(string message)
    {
        if (!IsRunning || _botClient == null || _config.AuthorizedChatId == 0) return;

        try
        {
            await _botClient.SendMessage(_config.AuthorizedChatId, message, parseMode: ParseMode.Markdown);
        }
        catch { }
    }
}
