using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public class CloudBackupConfig
{
    public string GmailAddress { get; set; } = "";
    public string GmailAppPassword { get; set; } = "";
    public string TargetDirectory { get; set; } = "";
    public bool BackupOnExit { get; set; } = true;
    public bool DailyBackupEnabled { get; set; } = true;
    public bool BackupOnClosing { get; set; } = true;
    public bool SendToGmail { get; set; } = false;
    public DateTime? LastBackupDate { get; set; }
    public bool? LastBackupVerified { get; set; }
    public string LastBackupVerifyMessage { get; set; } = "";

    // PDF fatura arşivi (yalnızca PDF'ler; veritabanı yedeğinden ayrıdır)
    public string PdfArchiveDirectory { get; set; } = "";     // asıl arşiv
    public string PdfPcBackupDirectory { get; set; } = "";    // bilgisayardaki ikinci kopya
    public string PdfDriveDirectory { get; set; } = "";       // Google Drive masaüstü klasörü
    public string PdfDriveMode { get; set; } = "Ask";         // Ask = her faturada sor, Always = otomatik, Never = sorma
    public bool DailyPdfBackupEnabled { get; set; } = true;
    public DateTime? LastPdfBackupDate { get; set; }
}

public class BackupFileInfo
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public double SizeMb { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FormattedSize => $"{SizeMb:N2} MB";
    public string FormattedDate => CreatedAt.ToString("dd.MM.yyyy HH:mm");
}

public static class CloudBackupService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp"
    );
    private static readonly string ConfigJsonPath = Path.Combine(ConfigDir, "cloud_backup_config.json");

    private static CloudBackupConfig _config = new();

    static CloudBackupService()
    {
        LoadConfig();
    }

    public static CloudBackupConfig Config => _config;

    public static void LoadConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (File.Exists(ConfigJsonPath))
            {
                string json = File.ReadAllText(ConfigJsonPath);
                var loaded = JsonSerializer.Deserialize<CloudBackupConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                    return;
                }
            }
        }
        catch { }

        _config = new CloudBackupConfig();
        _config.TargetDirectory = DetectDefaultBackupDirectory();
        SaveConfig();
    }

    public static void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigJsonPath, json);
        }
        catch { }
    }

    public static string DetectDefaultBackupDirectory()
    {
        try
        {
            // 1. Google Drive Masaüstü Uygulaması Kontrolü
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string gDrive = Path.Combine(userProfile, "Google Drive");
            if (Directory.Exists(gDrive))
            {
                string gTarget = Path.Combine(gDrive, "Bilensis_Yedekler");
                Directory.CreateDirectory(gTarget);
                return gTarget;
            }

            // Alternatif Google Drive konumu (G:\ veya My Drive)
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                string drivePath = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                if (Directory.Exists(drivePath))
                {
                    string target = Path.Combine(drivePath, "Bilensis_Yedekler");
                    Directory.CreateDirectory(target);
                    return target;
                }
            }

            // 2. OneDrive Kontrolü
            string oneDrive = Path.Combine(userProfile, "OneDrive");
            if (Directory.Exists(oneDrive))
            {
                string oTarget = Path.Combine(oneDrive, "Bilensis_Yedekler");
                Directory.CreateDirectory(oTarget);
                return oTarget;
            }

            // 3. Belgelerim Standart Yedek Klasörü
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string defaultDir = Path.Combine(docs, "Bilensis_Yedekler");
            Directory.CreateDirectory(defaultDir);
            return defaultDir;
        }
        catch
        {
            string fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Yedekler");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public static string GetBackupTargetDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_config.TargetDirectory) && Directory.Exists(_config.TargetDirectory))
        {
            return _config.TargetDirectory;
        }
        _config.TargetDirectory = DetectDefaultBackupDirectory();
        SaveConfig();
        return _config.TargetDirectory;
    }

    public static (bool Success, string Message, string? BackupFilePath) ExecuteBackup(bool silent = false)
    {
        try
        {
            string targetDir = GetBackupTargetDirectory();
            Directory.CreateDirectory(targetDir);

            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string tempBakName = $"Bilensis_DB_{timeStamp}.bak";
            string tempBakPath = Path.Combine(Path.GetTempPath(), tempBakName);
            string zipName = $"Bilensis_Bulut_Yedek_{timeStamp}.zip";
            string finalZipPath = Path.Combine(targetDir, zipName);

            string dbName = Database.CurrentDatabase;
            string sql = $@"BACKUP DATABASE [{dbName}] TO DISK = '{tempBakPath}' WITH FORMAT, INIT, NAME = 'Bilensis Auto Backup'";

            try
            {
                using var conn = Database.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = 180;
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // SQL Server servis hesabı temp klasörüne yazamazsa AppData klasörünü dene
                string appDataBak = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), tempBakName);
                string sqlFallback = $@"BACKUP DATABASE [{dbName}] TO DISK = '{appDataBak}' WITH FORMAT, INIT";
                using var connFallback = Database.Open();
                using var cmdFallback = connFallback.CreateCommand();
                cmdFallback.CommandText = sqlFallback;
                cmdFallback.CommandTimeout = 180;
                cmdFallback.ExecuteNonQuery();
                tempBakPath = appDataBak;
            }

            if (!File.Exists(tempBakPath))
            {
                return (false, "Veritabanı .bak dosyası oluşturulamadı.", null);
            }

            // 1) SQL Server .bak dosyasını doğrulasın (bozuk yedek hiç yedek değildir)
            var sqlVerify = Database.VerifyBackupFile(tempBakPath);
            long bakLength = new FileInfo(tempBakPath).Length;

            // .bak dosyasını yüksek oranda sıkıştırılmış .zip arşivine paketle
            using (var zipArchive = ZipFile.Open(finalZipPath, ZipArchiveMode.Create))
            {
                zipArchive.CreateEntryFromFile(tempBakPath, tempBakName, CompressionLevel.Optimal);
            }

            // 2) Zip'i baştan okuyarak bütünlüğünü ve boyutunu doğrula
            var zipVerify = VerifyBackupZip(finalZipPath, bakLength);

            // Geçici .bak dosyasını temizle
            try { File.Delete(tempBakPath); } catch { }

            bool verified = sqlVerify.Ok && zipVerify.Ok;
            string verifyText = verified
                ? "\n✅ Yedek doğrulandı (SQL kontrolü + arşiv bütünlüğü)."
                : $"\n❌ YEDEK DOĞRULANAMADI! {(sqlVerify.Ok ? "" : "SQL: " + sqlVerify.Message + " ")}{(zipVerify.Ok ? "" : "Arşiv: " + zipVerify.Message)}";

            _config.LastBackupDate = DateTime.Now;
            _config.LastBackupVerified = verified;
            _config.LastBackupVerifyMessage = verified ? "Doğrulandı" : (sqlVerify.Ok ? zipVerify.Message : sqlVerify.Message);
            SaveConfig();

            string mailStatus = "";
            // Gmail yedekleme aktifse e-posta ile de gönder
            if (_config.SendToGmail && !string.IsNullOrWhiteSpace(_config.GmailAddress) && !string.IsNullOrWhiteSpace(_config.GmailAppPassword))
            {
                var mailResult = SendBackupToGmail(finalZipPath);
                mailStatus = mailResult.Success 
                    ? "\n✉️ Gmail bulut hesabına başarıyla iletildi!" 
                    : $"\n⚠️ Gmail gönderim uyarısı: {mailResult.Message}";
            }

            AuditLogService.Log("Sistem", "Bulut Yedekleme", null, "Otomatik Bulut Yedekleme", null, $"Veritabanı yedeği alındı: {finalZipPath}");

            string msg = $"Veritabanı yedeklendi.\n\nKonum: {finalZipPath}{verifyText}{mailStatus}";
            if (silent && !verified)
                MessageBox.Show(msg, "⚠️ Otomatik Yedek Doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return (verified, msg, finalZipPath);
        }
        catch (Exception ex)
        {
            return (false, $"Yedekleme sırasında hata oluştu: {ex.Message}", null);
        }
    }

    /// <summary>Zip içindeki .bak girdisini baştan sona okuyup beklenen boyutla karşılaştırır.</summary>
    public static (bool Ok, string Message) VerifyBackupZip(string zipPath, long expectedLength)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
            if (entry == null) return (false, "Arşivde .bak dosyası yok.");

            long read = 0;
            var buffer = new byte[1024 * 1024];
            using var s = entry.Open();
            int n;
            while ((n = s.Read(buffer, 0, buffer.Length)) > 0) read += n;

            if (read != entry.Length || (expectedLength > 0 && read != expectedLength))
                return (false, "Arşiv boyutu beklenenle uyuşmuyor.");
            return (true, "Arşiv sağlam.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Var olan bir yedek zip'inin okunabilirliğini denetler (liste ekranından elle doğrulama).</summary>
    public static (bool Ok, string Message) VerifyExistingBackup(string zipPath)
        => VerifyBackupZip(zipPath, 0);

    public static void AutoCheckDailyBackup()
    {
        try
        {
            if (!_config.DailyBackupEnabled) return;

            // Eğer bugün henüz yedek alınmadıysa otomatik sessiz yedek al
            if (_config.LastBackupDate == null || _config.LastBackupDate.Value.Date < DateTime.Today)
            {
                ExecuteBackup(silent: true);
            }
        }
        catch { }
    }

    /// <summary>Yedek için tanımlı Gmail hesabı üzerinden herhangi bir alıcıya ekli e-posta gönderir.</summary>
    public static (bool Success, string Message) SendMail(string to, string subject, string body, string? attachmentPath = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.GmailAddress) || string.IsNullOrWhiteSpace(_config.GmailAppPassword))
                return (false, "Gmail hesabı tanımlı değil (Ayarlar > Bulut Yedekleme).");

            using var message = new MailMessage { From = new MailAddress(_config.GmailAddress, ThermalReceiptService.Config.StoreHeader) };
            message.To.Add(to.Trim());
            message.Subject = subject;
            message.Body = body;
            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
                message.Attachments.Add(new Attachment(attachmentPath));

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_config.GmailAddress, _config.GmailAppPassword.Replace(" ", "")),
                Timeout = 20000
            };
            smtp.Send(message);
            return (true, "Gönderildi.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static (bool Success, string Message) SendBackupToGmail(string zipFilePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_config.GmailAddress) || string.IsNullOrWhiteSpace(_config.GmailAppPassword))
            {
                return (false, "Gmail kullanıcı adı veya şifresi girilmemiş.");
            }

            if (!File.Exists(zipFilePath))
            {
                return (false, "Yedek dosyası bulunamadı.");
            }

            using var message = new MailMessage();
            message.From = new MailAddress(_config.GmailAddress, "Bilensis Otomatik Bulut Yedek");
            message.To.Add(_config.GmailAddress);
            message.Subject = $"📦 Bilensis Veritabanı Bulut Yedeği - {DateTime.Now:dd.MM.yyyy HH:mm}";
            message.Body = $"Merhaba,\n\nBilensis Stok & Cari Yönetim Sistemi'nin otomatik bulut SQL veritabanı yedeği ektedir.\n\nYedek Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\nDosya: {Path.GetFileName(zipFilePath)}\n\nBu e-posta otomatik oluşturulmuştur.";
            message.IsBodyHtml = false;

            var attachment = new Attachment(zipFilePath);
            message.Attachments.Add(attachment);

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_config.GmailAddress, _config.GmailAppPassword.Replace(" ", "")),
                Timeout = 15000
            };

            smtp.Send(message);
            return (true, "Yedek Gmail hesabınıza başarıyla gönderildi.");
        }
        catch (Exception ex)
        {
            return (false, $"Gmail gönderimi başarısız: {ex.Message}");
        }
    }

    public static (bool Success, string Message) TestGmailConnection(string email, string password)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Lütfen Gmail adresinizi ve şifrenizi giriniz.");
            }

            using var message = new MailMessage();
            message.From = new MailAddress(email, "Bilensis Sistem Testi");
            message.To.Add(email);
            message.Subject = "✅ Bilensis Google / Gmail Bağlantı Testi Başarılı";
            message.Body = $"Tebrikler!\n\nBilensis Bulut Yedekleme Google / Gmail bağlantı ayarlarınız başarıyla doğrulandı.\n\nTest Zamanı: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
            message.IsBodyHtml = false;

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(email, password.Replace(" ", "")),
                Timeout = 15000
            };

            smtp.Send(message);
            return (true, "Gmail bağlantısı başarıyla doğrulandı! Test e-postası gelen kutunuza gönderildi.");
        }
        catch (Exception ex)
        {
            return (false, $"Bağlantı hatası: {ex.Message}\n\nİpucu: Google hesabınızda '2 Adımlı Doğrulama' açık ise, lütfen Gmail şifreniz yerine Google Hesap Ayarları > Güvenlik > Uygulama Şifreleri (App Password) bölümünden 16 haneli bir şifre oluşturup giriniz.");
        }
    }

    public static List<BackupFileInfo> GetExistingBackups()
    {
        var list = new List<BackupFileInfo>();
        try
        {
            string targetDir = GetBackupTargetDirectory();
            if (Directory.Exists(targetDir))
            {
                var files = Directory.GetFiles(targetDir, "Bilensis_*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetCreationTime(f));

                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    list.Add(new BackupFileInfo
                    {
                        FileName = fi.Name,
                        FullPath = fi.FullName,
                        SizeMb = fi.Length / (1024.0 * 1024.0),
                        CreatedAt = fi.CreationTime
                    });
                }
            }
        }
        catch { }

        return list;
    }

    public static (bool Success, string Message) RestoreBackup(string backupFilePath)
    {
        string? tempBakExtracted = null;
        try
        {
            if (!File.Exists(backupFilePath))
            {
                return (false, "Seçilen yedek dosyası bulunamadı.");
            }

            string actualBakPath = backupFilePath;

            // Eğer dosya .zip ise içindeki .bak dosyasını geçici klasöre çıkart
            if (backupFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "Bilensis_Restore_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                using (var archive = ZipFile.OpenRead(backupFilePath))
                {
                    var bakEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
                    if (bakEntry == null)
                    {
                        return (false, "Yedek .zip arşivi içerisinde geçerli bir SQL Server .bak dosyası bulunamadı.");
                    }

                    tempBakExtracted = Path.Combine(tempDir, bakEntry.Name);
                    bakEntry.ExtractToFile(tempBakExtracted, true);
                    actualBakPath = tempBakExtracted;
                }
            }

            string dbName = Database.CurrentDatabase;
            string masterCs = Database.Config.BuildConnectionString("master");

            // Master veritabanı üzerinden mevcut açık bağlantıları sonlandır ve RESTORE yap
            using (var masterConn = new SqlConnection(masterCs))
            {
                masterConn.Open();

                // 1. Veritabanını SINGLE_USER moduna al (bağlantıları kopar)
                try
                {
                    using var killCmd = masterConn.CreateCommand();
                    killCmd.CommandText = $@"
ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                    killCmd.CommandTimeout = 60;
                    killCmd.ExecuteNonQuery();
                }
                catch { }

                // 2. RESTORE DATABASE komutunu çalıştır
                using (var restoreCmd = masterConn.CreateCommand())
                {
                    restoreCmd.CommandText = $@"
RESTORE DATABASE [{dbName}] FROM DISK = '{actualBakPath}' WITH REPLACE;";
                    restoreCmd.CommandTimeout = 300;
                    restoreCmd.ExecuteNonQuery();
                }

                // 3. Veritabanını tekrar MULTI_USER moduna al
                using (var multiCmd = masterConn.CreateCommand())
                {
                    multiCmd.CommandText = $@"
ALTER DATABASE [{dbName}] SET MULTI_USER;";
                    multiCmd.CommandTimeout = 60;
                    multiCmd.ExecuteNonQuery();
                }
            }

            AuditLogService.Log("Sistem", "Yedekten Geri Yükleme", null, "Veritabanı Geri Yükleme", null, $"Veritabanı yedeğe geri döndürüldü: {Path.GetFileName(backupFilePath)}");

            return (true, $"Veritabanı başarıyla seçilen yedeğe ({Path.GetFileName(backupFilePath)}) geri döndürüldü!");
        }
        catch (Exception ex)
        {
            // Olası hata durumunda veritabanını MULTI_USER'a döndürmeyi dene
            try
            {
                string masterCs = Database.Config.BuildConnectionString("master");
                using var masterConn = new SqlConnection(masterCs);
                masterConn.Open();
                using var multiCmd = masterConn.CreateCommand();
                multiCmd.CommandText = $"ALTER DATABASE [{Database.CurrentDatabase}] SET MULTI_USER;";
                multiCmd.ExecuteNonQuery();
            }
            catch { }

            return (false, $"Geri yükleme sırasında hata oluştu: {ex.Message}");
        }
        finally
        {
            if (tempBakExtracted != null && File.Exists(tempBakExtracted))
            {
                try { File.Delete(tempBakExtracted); } catch { }
                try { Directory.Delete(Path.GetDirectoryName(tempBakExtracted)!, true); } catch { }
            }
        }
    }
}
