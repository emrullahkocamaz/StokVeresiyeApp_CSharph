using System.Data;
using System.Security.Cryptography;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

/// <summary>
/// Fatura PDF arşivi. PDF'ler veritabanında değil, programın kendi belirlediği klasörde saklanır.
/// Fatura kaydedilirken PDF ÖNCE bu klasöre kopyalanıp doğrulanır; böylece kaynak dosya (e-posta eki, USB, indirilenler)
/// sonradan silinse bile fatura belgesi kaybolmaz. Klasör düzeni: Kök\yyyy\MM\FaturaNo_hash8.pdf
/// Günlük olarak yalnızca bu klasör, hem bilgisayardaki ikinci bir klasöre hem de Google Drive klasörüne yedeklenir.
/// </summary>
public static class PdfArchiveService
{
    private static readonly object _backupLock = new();

    // ---- Klasörler ----

    private static CloudBackupConfig Cfg => CloudBackupService.Config;

    /// <summary>Asıl PDF arşivi. Ayarlanmadıysa Belgelerim\Bilensis_Faturalar.</summary>
    public static string ArchiveRoot
    {
        get
        {
            string dir = Cfg.PdfArchiveDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Bilensis_Faturalar");
                Cfg.PdfArchiveDirectory = dir;
                CloudBackupService.SaveConfig();
            }
            return dir;
        }
    }

    /// <summary>Bilgisayardaki ikinci kopya. Mümkünse sistem diskinden farklı bir disk seçilir.</summary>
    public static string PcBackupRoot
    {
        get
        {
            string dir = Cfg.PdfPcBackupDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = DetectDefaultPcBackupDirectory();
                Cfg.PdfPcBackupDirectory = dir;
                CloudBackupService.SaveConfig();
            }
            return dir;
        }
    }

    /// <summary>Google Drive (masaüstü uygulaması) klasörü. Bulunamazsa boş döner.</summary>
    public static string DriveRoot
    {
        get
        {
            string dir = Cfg.PdfDriveDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = DetectDriveFolder() ?? "";
                if (!string.IsNullOrEmpty(dir))
                {
                    Cfg.PdfDriveDirectory = dir;
                    CloudBackupService.SaveConfig();
                }
            }
            return dir;
        }
    }

    public static string? DetectDriveFolder()
    {
        try
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var candidate in new[] { Path.Combine(profile, "Google Drive"), Path.Combine(profile, "My Drive") })
            {
                if (Directory.Exists(candidate)) return Path.Combine(candidate, "Bilensis_Faturalar");
            }

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                string my = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                if (Directory.Exists(my)) return Path.Combine(my, "Bilensis_Faturalar");
            }
        }
        catch { }
        return null;
    }

    private static string DetectDefaultPcBackupDirectory()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        try
        {
            string? docsRoot = Path.GetPathRoot(docs);
            var other = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady
                            && !string.Equals(d.RootDirectory.FullName, docsRoot, StringComparison.OrdinalIgnoreCase)
                            && d.AvailableFreeSpace > 1L * 1024 * 1024 * 1024)
                .OrderByDescending(d => d.AvailableFreeSpace)
                .FirstOrDefault();
            if (other != null) return Path.Combine(other.RootDirectory.FullName, "Bilensis_Yedekler", "Faturalar");
        }
        catch { }
        return Path.Combine(docs, "Bilensis_Yedekler", "Faturalar");
    }

    // ---- Arşive kaydetme ----

    public record StoredPdf(string Path, string Sha256, bool CreatedNew);

    private static string SafeName(string s)
    {
        string clean = string.Join("_", (s ?? "").Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        clean = clean.Trim();
        if (clean.Length == 0) clean = "Fatura";
        return clean.Length > 60 ? clean[..60] : clean;
    }

    private static string Sha256Of(string file)
    {
        using var fs = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    private static bool IsUnderArchive(string path)
    {
        try
        {
            string root = System.IO.Path.GetFullPath(ArchiveRoot).TrimEnd('\\') + "\\";
            return System.IO.Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>
    /// Kaynak dosyayı arşive kopyalar ve kopyanın aynı içerikte olduğunu doğrular. Hata olursa istisna fırlatır;
    /// çağıran taraf faturayı kaydetmemelidir.
    /// </summary>
    public static StoredPdf Store(string sourcePath, string invoiceNumber, DateTime invoiceDate)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Fatura dosyası bulunamadı.", sourcePath);

        string hash = Sha256Of(sourcePath);

        // Zaten arşivdeyse tekrar kopyalama
        if (IsUnderArchive(sourcePath)) return new StoredPdf(System.IO.Path.GetFullPath(sourcePath), hash, false);

        string ext = System.IO.Path.GetExtension(sourcePath);
        if (string.IsNullOrEmpty(ext)) ext = ".pdf";
        string dir = System.IO.Path.Combine(ArchiveRoot, invoiceDate.ToString("yyyy"), invoiceDate.ToString("MM"));
        string target = System.IO.Path.Combine(dir, $"{SafeName(invoiceNumber)}_{hash[..8]}{ext}");

        try
        {
            Directory.CreateDirectory(dir);
            if (File.Exists(target) && Sha256Of(target) == hash) return new StoredPdf(target, hash, false);

            string tmp = target + ".tmp";
            File.Copy(sourcePath, tmp, true);
            if (Sha256Of(tmp) != hash)
            {
                try { File.Delete(tmp); } catch { }
                throw new IOException("Kopya doğrulanamadı (içerik farklı).");
            }
            File.Move(tmp, target, true);
            return new StoredPdf(target, hash, true);
        }
        catch (Exception ex)
        {
            throw new IOException($"Fatura PDF'i güvenli arşiv klasörüne kopyalanamadı ({ArchiveRoot}). Fatura kaydedilmedi.\n\n{ex.Message}", ex);
        }
    }

    public static StoredPdf StoreBytes(byte[] data, string extension, string invoiceNumber, DateTime invoiceDate)
    {
        string hash = Convert.ToHexString(SHA256.HashData(data));
        string ext = string.IsNullOrEmpty(extension) ? ".pdf" : extension;
        string dir = System.IO.Path.Combine(ArchiveRoot, invoiceDate.ToString("yyyy"), invoiceDate.ToString("MM"));
        string target = System.IO.Path.Combine(dir, $"{SafeName(invoiceNumber)}_{hash[..8]}{ext}");

        Directory.CreateDirectory(dir);
        if (File.Exists(target) && Sha256Of(target) == hash) return new StoredPdf(target, hash, false);

        string tmp = target + ".tmp";
        File.WriteAllBytes(tmp, data);
        if (Sha256Of(tmp) != hash)
        {
            try { File.Delete(tmp); } catch { }
            throw new IOException("Arşive yazılan dosya doğrulanamadı.");
        }
        File.Move(tmp, target, true);
        return new StoredPdf(target, hash, true);
    }

    // ---- Eski kayıtları taşıma ----

    /// <summary>
    /// Eski sürümlerde PDF'i veritabanına gömülmüş (PdfData) veya arşiv dışında bir yerde kalmış faturaları
    /// arşiv klasörüne alır, hash'i kaydeder ve veritabanındaki PDF verisini temizler (dosya doğrulandıktan sonra).
    /// </summary>
    public static int MigrateLegacy()
    {
        int moved = 0;
        try
        {
            var ids = Database.Query("SELECT Id FROM Invoices WHERE PdfData IS NOT NULL OR (PdfPath IS NOT NULL AND PdfPath <> '' AND (PdfSha256 IS NULL OR PdfSha256 = ''));");
            foreach (DataRow idRow in ids.Rows)
            {
                long id = Convert.ToInt64(idRow["Id"]);
                try
                {
                    var dt = Database.Query("SELECT InvoiceNumber, InvoiceDate, PdfPath, PdfData FROM Invoices WHERE Id = @id;", ("@id", id));
                    if (dt.Rows.Count == 0) continue;
                    var r = dt.Rows[0];
                    string invNo = r["InvoiceNumber"]?.ToString() ?? $"Fatura{id}";
                    DateTime date = DateTime.TryParse(r["InvoiceDate"]?.ToString(), out var d) ? d : DateTime.Today;
                    string? path = r["PdfPath"]?.ToString();
                    byte[]? data = r["PdfData"] == DBNull.Value ? null : (byte[])r["PdfData"];

                    StoredPdf? stored = null;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        stored = Store(path, invNo, date);
                    else if (data != null && data.Length > 0)
                        stored = StoreBytes(data, ".pdf", invNo, date);

                    if (stored == null) continue;

                    Database.Execute("UPDATE Invoices SET PdfPath = @p, PdfSha256 = @h, PdfData = NULL WHERE Id = @id;",
                        ("@p", stored.Path), ("@h", stored.Sha256), ("@id", id));
                    moved++;
                }
                catch { /* bir fatura taşınamazsa diğerlerine devam edilir; PdfData silinmez */ }
            }
        }
        catch { }
        return moved;
    }

    // ---- Yedekleme (yalnızca PDF'ler) ----

    public record BackupResult(int Copied, int Skipped, int Failed, string Message);

    /// <summary>Arşivdeki yeni/eksik dosyaları hedef klasöre kopyalar. Hedefte hiçbir şey silinmez.</summary>
    private static BackupResult MirrorTo(string destRoot)
    {
        int copied = 0, skipped = 0, failed = 0;
        string src = ArchiveRoot;
        if (!Directory.Exists(src)) return new BackupResult(0, 0, 0, "Arşiv klasörü henüz yok.");

        foreach (var file in Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories))
        {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                string rel = System.IO.Path.GetRelativePath(src, file);
                string target = System.IO.Path.Combine(destRoot, rel);
                var fi = new FileInfo(file);
                if (File.Exists(target) && new FileInfo(target).Length == fi.Length)
                {
                    skipped++;
                    continue;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                string tmp = target + ".tmp";
                File.Copy(file, tmp, true);
                File.Move(tmp, target, true);
                copied++;
            }
            catch { failed++; }
        }
        return new BackupResult(copied, skipped, failed, "");
    }

    /// <summary>PDF arşivini bilgisayardaki ikinci klasöre ve Google Drive klasörüne yedekler.</summary>
    public static (bool Success, string Message) BackupAll()
    {
        lock (_backupLock)
        {
            var lines = new List<string>();
            bool ok = true;

            try
            {
                var pc = MirrorTo(PcBackupRoot);
                lines.Add($"💻 Bilgisayar yedeği ({PcBackupRoot}): {pc.Copied} yeni, {pc.Skipped} güncel" + (pc.Failed > 0 ? $", {pc.Failed} HATA" : ""));
                if (pc.Failed > 0) ok = false;
            }
            catch (Exception ex)
            {
                ok = false;
                lines.Add("💻 Bilgisayar yedeği alınamadı: " + ex.Message);
            }

            string drive = DriveRoot;
            if (string.IsNullOrWhiteSpace(drive))
            {
                ok = false;
                lines.Add("☁️ Google Drive klasörü bulunamadı. Google Drive masaüstü uygulamasını kurun veya Ayarlar'dan klasör seçin.");
            }
            else
            {
                try
                {
                    var dr = MirrorTo(drive);
                    lines.Add($"☁️ Google Drive ({drive}): {dr.Copied} yeni, {dr.Skipped} güncel" + (dr.Failed > 0 ? $", {dr.Failed} HATA" : "") + " — senkronizasyonu Drive uygulaması yapar.");
                    if (dr.Failed > 0) ok = false;
                }
                catch (Exception ex)
                {
                    ok = false;
                    lines.Add("☁️ Google Drive yedeği alınamadı: " + ex.Message);
                }
            }

            if (ok)
            {
                Cfg.LastPdfBackupDate = DateTime.Now;
                CloudBackupService.SaveConfig();
            }

            return (ok, string.Join("\n", lines));
        }
    }

    /// <summary>Günlük otomatik PDF yedeği: bugün henüz alınmadıysa sessizce çalıştırır.</summary>
    public static void AutoCheckDailyPdfBackup()
    {
        try
        {
            if (!Cfg.DailyPdfBackupEnabled) return;
            if (Cfg.LastPdfBackupDate != null && Cfg.LastPdfBackupDate.Value.Date >= DateTime.Today) return;
            var res = BackupAll();
            AuditLogService.Log("Sistem", "PDF Fatura Yedeği", null, "Günlük PDF Yedeği", null, res.Message.Replace("\n", " | "));
        }
        catch { }
    }

    /// <summary>Tek bir faturanın PDF'ini hemen Google Drive klasörüne kopyalar.</summary>
    public static (bool Success, string Message) CopyToDrive(IEnumerable<string> storedPaths)
    {
        string drive = DriveRoot;
        if (string.IsNullOrWhiteSpace(drive))
            return (false, "Google Drive klasörü bulunamadı. Google Drive masaüstü uygulamasını kurun veya Bulut Yedekleme > PDF Arşivi ayarlarından klasör seçin.");

        int ok = 0, fail = 0;
        foreach (var p in storedPaths.Where(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x)))
        {
            try
            {
                string rel = IsUnderArchive(p) ? System.IO.Path.GetRelativePath(ArchiveRoot, p) : System.IO.Path.GetFileName(p);
                string target = System.IO.Path.Combine(drive, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                File.Copy(p, target, true);
                ok++;
            }
            catch { fail++; }
        }

        return fail == 0
            ? (true, $"{ok} fatura Google Drive klasörüne kopyalandı.")
            : (false, $"{ok} fatura kopyalandı, {fail} fatura kopyalanamadı.");
    }

    /// <summary>
    /// Fatura kaydı sonrası: ayara göre Drive'a kopyalamayı sorar (Sor), hemen yapar (Her zaman) veya hiçbir şey yapmaz (Hiç).
    /// </summary>
    public static void OfferDriveCopy(IWin32Window? owner, IReadOnlyCollection<string> storedPaths)
    {
        if (storedPaths.Count == 0) return;
        string mode = Cfg.PdfDriveMode;

        if (string.Equals(mode, "Never", StringComparison.OrdinalIgnoreCase)) return;

        if (!string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
        {
            string what = storedPaths.Count == 1 ? "Bu faturanın PDF'i" : $"{storedPaths.Count} faturanın PDF'i";
            var ask = MessageBox.Show(
                $"{what} güvenli arşiv klasörüne kaydedildi.\n\nGoogle Drive klasörüne de şimdi yedeklensin mi?\n\n(Evet derseniz Drive uygulaması bulutla senkronize eder. Hayır derseniz günlük otomatik yedekte yine de alınır.)",
                "PDF Fatura Yedeği", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return;
        }

        var res = CopyToDrive(storedPaths);
        if (!res.Success)
            MessageBox.Show(res.Message, "PDF Fatura Yedeği", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
