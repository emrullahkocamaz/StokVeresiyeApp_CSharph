using System;
using System.IO;
using System.IO.Compression;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;

namespace StokVeresiyeApp.Services;

public static class CloudBackupService
{
    private static readonly string ConfigFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StokVeresiyeApp",
        "cloud_backup_config.txt"
    );

    public static string GetBackupTargetDirectory()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                string dir = File.ReadAllText(ConfigFile).Trim();
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    return dir;
            }

            // Otomatik tespit: Kullanıcı Google Drive veya OneDrive kullanıyorsa oraya koy
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string gDrive = Path.Combine(userProfile, "Google Drive");
            if (Directory.Exists(gDrive))
            {
                string gTarget = Path.Combine(gDrive, "Bilensis_Yedekler");
                Directory.CreateDirectory(gTarget);
                return gTarget;
            }

            string oneDrive = Path.Combine(userProfile, "OneDrive");
            if (Directory.Exists(oneDrive))
            {
                string oTarget = Path.Combine(oneDrive, "Bilensis_Yedekler");
                Directory.CreateDirectory(oTarget);
                return oTarget;
            }

            // Standart yedek klasörü (Belgelerim / Bilensis_Yedekler)
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

    public static void SetBackupTargetDirectory(string directory)
    {
        try
        {
            string cfgDir = Path.GetDirectoryName(ConfigFile)!;
            Directory.CreateDirectory(cfgDir);
            File.WriteAllText(ConfigFile, directory);
        }
        catch { }
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
                // SQL Server ayrı servis kullanıcısı olduğunda temp'e yazamayabilir; AppData klasörünü dene
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

            // .bak dosyasını yüksek oranda sıkıştırılmış .zip arşivine paketle
            using (var zipArchive = ZipFile.Open(finalZipPath, ZipArchiveMode.Create))
            {
                zipArchive.CreateEntryFromFile(tempBakPath, tempBakName, CompressionLevel.Optimal);
            }

            // Geçici .bak dosyasını temizle
            try { File.Delete(tempBakPath); } catch { }

            AuditLogService.Log("Sistem", "Bulut Yedekleme", null, null, null, $"Veritabanı yedeği alındı: {finalZipPath}");

            return (true, $"Veritabanı başarıyla yedeklendi ve bulut hedefine aktarıldı!\n\nKonum: {finalZipPath}", finalZipPath);
        }
        catch (Exception ex)
        {
            return (false, $"Yedekleme sırasında hata oluştu: {ex.Message}", null);
        }
    }
}
