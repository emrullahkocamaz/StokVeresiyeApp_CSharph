using Microsoft.Win32;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace StokVeresiyeApp.Services;

public static class LicenseService
{
    private const string SecretSalt = "BILGE_STOK_OFFLINE_LICENSE_2026_EMRKCM_SECURE_KEY";
    public const string SupportEmail = "emrkcm@gmail.com";

    private static readonly string LicenseFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp",
        "license.dat"
    );

    private static LicenseInfo? _cachedLicense;

    public static void Initialize()
    {
        // AppLicense tablosunu oluştur
        using var c = Database.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppLicense')
CREATE TABLE AppLicense (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MachineCode NVARCHAR(100) NOT NULL,
    LicenseKey NVARCHAR(MAX) NOT NULL,
    ActivatedDate NVARCHAR(50) NOT NULL,
    ExpireDate NVARCHAR(50) NOT NULL,
    LicensedTo NVARCHAR(250) NULL,
    Status NVARCHAR(50) NOT NULL
);";
        cmd.ExecuteNonQuery();

        CheckLicenseStatus();
    }

    /// <summary>
    /// Bu bilgisayara özel benzersiz 16 karakterli makine donanım kodunu üretir.
    /// Format: BS-XXXX-XXXX-XXXX-XXXX
    /// </summary>
    public static string GetMachineCode()
    {
        try
        {
            var rawBuilder = new StringBuilder();
            rawBuilder.Append(Environment.MachineName);
            rawBuilder.Append("|");
            rawBuilder.Append(Environment.ProcessorCount);
            rawBuilder.Append("|");

            // Windows MachineGuid (sistem yeniden kurulana kadar sabit kalan benzersiz GUID)
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                var guid = key?.GetValue("MachineGuid")?.ToString();
                if (!string.IsNullOrWhiteSpace(guid))
                {
                    rawBuilder.Append(guid);
                }
            }
            catch { }

            // Kullanıcı profil klasörü yolu da ek bir bileşen sağlar
            rawBuilder.Append("|");
            rawBuilder.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawBuilder.ToString()));
            string hex = Convert.ToHexString(hashBytes).ToUpperInvariant();

            // İlk 16 karakteri alıp 4'erli bloklar halinde formatla: BS-XXXX-XXXX-XXXX-XXXX
            string sub = hex.Substring(0, 16);
            return $"BS-{sub.Substring(0, 4)}-{sub.Substring(4, 4)}-{sub.Substring(8, 4)}-{sub.Substring(12, 4)}";
        }
        catch
        {
            return "BS-AAAA-BBBB-CCCC-DDDD";
        }
    }

    /// <summary>
    /// Geliştirici / Yönetici için benzersiz 1 yıllık (veya belirtilen gün kadar) çevrimdışı lisans anahtarı üretir.
    /// </summary>
    public static string GenerateLicenseKey(string machineCode, string licensedTo, int validityDays = 365)
    {
        if (string.IsNullOrWhiteSpace(machineCode))
            throw new ArgumentException("Makine kodu boş olamaz.");

        string cleanMachine = machineCode.Replace("-", "").Trim().ToUpperInvariant();
        DateTime expireDate = DateTime.Today.AddDays(validityDays);
        string expireStr = expireDate.ToString("yyyy-MM-dd");
        string client = string.IsNullOrWhiteSpace(licensedTo) ? "Lisansli Firma" : licensedTo.Trim();

        // Payload format: CLEAN_MACHINE|EXPIRE_DATE|CLIENT_NAME
        string payload = $"{cleanMachine}|{expireStr}|{client}";
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        string payloadBase64 = Convert.ToBase64String(payloadBytes);

        // HMAC-SHA256 ile imzalama
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretSalt));
        byte[] signBytes = hmac.ComputeHash(payloadBytes);
        string signHex = Convert.ToHexString(signBytes).Substring(0, 16).ToUpperInvariant();

        return $"BSLK.{payloadBase64}.{signHex}";
    }

    /// <summary>
    /// Girilen lisans anahtarını doğrular ve geçerliyse sisteme aktifleştirir.
    /// </summary>
    public static (bool Success, string Message, LicenseInfo? Info) ActivateLicense(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return (false, "Lütfen bir lisans anahtarı giriniz.", null);

        licenseKey = licenseKey.Trim();
        var parts = licenseKey.Split('.');
        if (parts.Length != 3 || parts[0] != "BSLK")
        {
            return (false, "Geçersiz lisans anahtarı formatı. Lütfen size iletilen anahtarı eksiksiz giriniz.", null);
        }

        string payloadBase64 = parts[1];
        string providedSign = parts[2].ToUpperInvariant();

        byte[] payloadBytes;
        try
        {
            payloadBytes = Convert.FromBase64String(payloadBase64);
        }
        catch
        {
            return (false, "Lisans anahtarı hasarlı veya bozuk.", null);
        }

        // 1. İmzayı doğrula
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretSalt));
        byte[] expectedSignBytes = hmac.ComputeHash(payloadBytes);
        string expectedSign = Convert.ToHexString(expectedSignBytes).Substring(0, 16).ToUpperInvariant();

        if (providedSign != expectedSign)
        {
            return (false, "Lisans anahtarı imza doğrulaması başarısız! Anahtar geçersiz.", null);
        }

        // 2. Payload'ı ayrıştır
        string payload = Encoding.UTF8.GetString(payloadBytes);
        var pParts = payload.Split('|');
        if (pParts.Length < 2)
        {
            return (false, "Lisans verisi okunamadı.", null);
        }

        string targetMachine = pParts[0].ToUpperInvariant();
        string expireStr = pParts[1];
        string licensedTo = pParts.Length >= 3 ? pParts[2] : "Lisanslı Kullanıcı";

        // 3. Makine kodu kontrolü
        string currentMachine = GetMachineCode().Replace("-", "").ToUpperInvariant();
        if (targetMachine != currentMachine)
        {
            return (false, "Bu lisans anahtarı başka bir bilgisayar donanımı için üretilmiştir! Kendi makine kodunuzla lisans talep etmelisiniz.", null);
        }

        // 4. Tarih kontrolü
        if (!DateTime.TryParse(expireStr, out var expireDate))
        {
            return (false, "Lisans geçerlilik tarihi çözülemedi.", null);
        }

        if (DateTime.Today > expireDate.Date)
        {
            return (false, $"Bu lisansın geçerlilik süresi dolmuştur! (Son Geçerlilik: {expireDate:dd.MM.yyyy})", null);
        }

        // 5. Veritabanına ve yerel dosyaya kaydet
        try
        {
            using var c = Database.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO AppLicense (MachineCode, LicenseKey, ActivatedDate, ExpireDate, LicensedTo, Status)
VALUES (@mc, @key, @act, @exp, @to, 'Active');";
            cmd.Parameters.AddWithValue("@mc", GetMachineCode());
            cmd.Parameters.AddWithValue("@key", licenseKey);
            cmd.Parameters.AddWithValue("@act", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@exp", expireDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@to", licensedTo);
            cmd.ExecuteNonQuery();

            // Yerel dosyaya da güvenli olarak yaz
            SaveLicenseToFile(licenseKey);

            var info = new LicenseInfo
            {
                MachineCode = GetMachineCode(),
                LicenseKey = licenseKey,
                ActivatedDate = DateTime.Now,
                ExpireDate = expireDate,
                LicensedTo = licensedTo,
                IsValid = true,
                StatusMessage = "Lisans Aktif"
            };

            _cachedLicense = info;
            return (true, $"Tebrikler! Lisansınız 1 yıl süreyle başarıyla aktif edildi.\nSon Geçerlilik: {expireDate:dd.MM.yyyy}\nKalan Gün: {info.DaysRemaining}", info);
        }
        catch (Exception ex)
        {
            return (false, "Lisans kaydedilirken veritabanı hatası oluştu: " + ex.Message, null);
        }
    }

    /// <summary>
    /// Mevcut sistem lisansının durumunu sorgular.
    /// </summary>
    public static LicenseInfo CheckLicenseStatus()
    {
        var info = new LicenseInfo
        {
            MachineCode = GetMachineCode(),
            IsValid = false,
            StatusMessage = "Lisans Bulunamadı"
        };

        try
        {
            string? licenseKey = null;
            string? status = null;

            // Önce veritabanından son lisansı oku
            using (var c = Database.Open())
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = "SELECT TOP 1 LicenseKey, Status FROM AppLicense ORDER BY Id DESC;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    licenseKey = reader["LicenseKey"]?.ToString();
                    status = reader["Status"]?.ToString();
                }
            }

            if (status != null && status.Equals("Passive", StringComparison.OrdinalIgnoreCase))
            {
                info.StatusMessage = "Sistem lisansı pasife alınmıştır. Sistemi kullanmak için lütfen yeni lisans anahtarı giriniz.";
                info.IsValid = false;
                _cachedLicense = info;
                return info;
            }

            // Veritabanında yoksa yerel dosyaya bak
            if (string.IsNullOrWhiteSpace(licenseKey) && File.Exists(LicenseFilePath))
            {
                licenseKey = File.ReadAllText(LicenseFilePath).Trim();
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                info.StatusMessage = "Sistem lisanslanmamış. Lütfen lisans anahtarınızı giriniz.";
                _cachedLicense = info;
                return info;
            }

            // Anahtarı doğrula
            var parts = licenseKey.Split('.');
            if (parts.Length != 3 || parts[0] != "BSLK")
            {
                info.StatusMessage = "Kayıtlı lisans anahtarı geçersiz formatta.";
                _cachedLicense = info;
                return info;
            }

            byte[] payloadBytes = Convert.FromBase64String(parts[1]);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretSalt));
            byte[] expectedSignBytes = hmac.ComputeHash(payloadBytes);
            string expectedSign = Convert.ToHexString(expectedSignBytes).Substring(0, 16).ToUpperInvariant();

            if (parts[2].ToUpperInvariant() != expectedSign)
            {
                info.StatusMessage = "Kayıtlı lisansın imza doğrulaması başarısız!";
                _cachedLicense = info;
                return info;
            }

            string payload = Encoding.UTF8.GetString(payloadBytes);
            var pParts = payload.Split('|');
            string targetMachine = pParts[0].ToUpperInvariant();
            string expireStr = pParts[1];
            string licensedTo = pParts.Length >= 3 ? pParts[2] : "Lisanslı Firma";

            string currentMachine = GetMachineCode().Replace("-", "").ToUpperInvariant();
            if (targetMachine != currentMachine)
            {
                info.StatusMessage = "Lisans başka bir bilgisayara ait! Donanım uyuşmazlığı.";
                _cachedLicense = info;
                return info;
            }

            if (!DateTime.TryParse(expireStr, out var expireDate))
            {
                info.StatusMessage = "Lisans bitiş tarihi okunamadı.";
                _cachedLicense = info;
                return info;
            }

            info.ExpireDate = expireDate;
            info.LicenseKey = licenseKey;
            info.LicensedTo = licensedTo;

            if (DateTime.Today > expireDate.Date)
            {
                info.IsValid = false;
                info.StatusMessage = $"Lisans süreniz dolmuştur! (Bitiş Tarihi: {expireDate:dd.MM.yyyy}). Sistemi kullanmaya devam etmek için lütfen yeni lisans alınız.";
            }
            else
            {
                info.IsValid = true;
                info.StatusMessage = $"Lisans Aktif. Kalan Süre: {info.DaysRemaining} gün (Bitiş: {expireDate:dd.MM.yyyy})";
            }

            _cachedLicense = info;
            return info;
        }
        catch (Exception ex)
        {
            info.StatusMessage = "Lisans kontrol hatası: " + ex.Message;
            _cachedLicense = info;
            return info;
        }
    }

    public static LicenseInfo CurrentLicense => _cachedLicense ?? CheckLicenseStatus();

    public static bool IsLicenseActive() => CurrentLicense.IsValid;

    /// <summary>
    /// Mevcut aktif lisansı pasife alır ve sistemi lisanssız duruma çeker.
    /// </summary>
    public static (bool Success, string Message) DeactivateLicense(string reason = "Kullanıcı/Yönetici tarafından pasife alındı")
    {
        try
        {
            using var c = Database.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE AppLicense SET Status = 'Passive' WHERE Status = 'Active';";
            cmd.ExecuteNonQuery();

            if (File.Exists(LicenseFilePath))
            {
                try { File.Delete(LicenseFilePath); } catch { }
            }

            _cachedLicense = null;
            CheckLicenseStatus();

            AuditLogService.Log("Lisans", "Pasife Alındı", null, GetMachineCode(), null, reason);
            return (true, "Lisans başarıyla pasife alındı. Sistem lisanssız duruma geçirildi.");
        }
        catch (Exception ex)
        {
            return (false, "Lisans pasife alınırken hata oluştu: " + ex.Message);
        }
    }

    /// <summary>
    /// İnternet bağlantısı üzerinden doğrudan emrkcm@gmail.com adresine lisans talebini iletir.
    /// </summary>
    public static async Task<(bool Success, string Message)> SendDirectLicenseRequestMailAsync(
        string companyName,
        string contactName,
        string phone,
        string email,
        string requestNote)
    {
        string machineCode = GetMachineCode();

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var postData = new Dictionary<string, string>
            {
                { "_subject", $"Bilensis - 1 Yıllık Lisans Talebi ({companyName})" },
                { "Makine_Kodu", machineCode },
                { "Firma_Unvani", string.IsNullOrWhiteSpace(companyName) ? "[Belirtilmedi]" : companyName },
                { "Yetkili_Kisi", string.IsNullOrWhiteSpace(contactName) ? "[Belirtilmedi]" : contactName },
                { "Telefon_Numarasi", string.IsNullOrWhiteSpace(phone) ? "[Belirtilmedi]" : phone },
                { "Iletisim_Eposta", string.IsNullOrWhiteSpace(email) ? "[Belirtilmedi]" : email },
                { "Talep_Notu", string.IsNullOrWhiteSpace(requestNote) ? "1 Yıllık Lisans Talebi" : requestNote },
                { "Talep_Tarihi", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") },
                { "_template", "table" }
            };

            var content = new FormUrlEncodedContent(postData);

            // Formsubmit AJAX endpoint doğrudan emrkcm@gmail.com'a iletir
            var response = await client.PostAsync("https://formsubmit.co/ajax/emrkcm@gmail.com", content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Lisans talebiniz doğrudan 'emrkcm@gmail.com' adresine iletildi!\nLisans anahtarınız üretildiğinde e-posta veya telefon yoluyla size ulaştırılacaktır.");
            }
            else
            {
                return (false, "Web mail servisi yanıt vermedi. Lütfen varsayılan e-posta istemcisi ile göndermeyi deneyiniz.");
            }
        }
        catch (Exception ex)
        {
            return (false, "İnternet bağlantısı kurulamadı: " + ex.Message);
        }
    }

    public static void OpenLicenseMailRequest(string? customerNote = null)
    {
        string machineCode = GetMachineCode();
        string subject = Uri.EscapeDataString("Bilensis - Lisans Satın Alma / Yenileme Talebi");
        string body = Uri.EscapeDataString(
            $"Merhaba,\r\n\r\n" +
            $"Bilensis (Stok & Cari Yönetim Sistemi) için 1 yıllık lisans anahtarı talep ediyorum.\r\n\r\n" +
            $"Makine Donanım Kodum:\r\n{machineCode}\r\n\r\n" +
            $"Firma / Yetkili Adı:\r\n{(string.IsNullOrWhiteSpace(customerNote) ? "[Lütfen Firma Adınızı Yazınız]" : customerNote)}\r\n\r\n" +
            $"Tarih: {DateTime.Now:dd.MM.yyyy HH:mm}\r\n\r\n" +
            $"İyi çalışmalar dilerim."
        );

        string mailtoUri = $"mailto:{SupportEmail}?subject={subject}&body={body}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mailtoUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Mail istemcisi açılamazsa Clipboard'a kopyala
            Clipboard.SetText(machineCode);
            MessageBox.Show(
                $"Varsayılan e-posta istemcisi açılamadı.\n\nMakine Kodunuz ({machineCode}) panoya kopyalandı!\nLütfen manuel olarak '{SupportEmail}' adresine gönderiniz.",
                "E-Posta Yönlendirme",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }

    private static void SaveLicenseToFile(string licenseKey)
    {
        try
        {
            string dir = Path.GetDirectoryName(LicenseFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(LicenseFilePath, licenseKey);
        }
        catch { }
    }
}
