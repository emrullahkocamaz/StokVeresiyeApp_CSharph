using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public class MaintenanceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public double InitialSizeMb { get; set; }
    public double FinalSizeMb { get; set; }
    public double FreedSpaceMb => Math.Max(0, InitialSizeMb - FinalSizeMb);
    public int OptimizedTablesCount { get; set; }
    public TimeSpan Duration { get; set; }
}

public static class DatabaseMaintenanceService
{
    /// <summary>
    /// SQL Server veritabanının kontrolsüz şişmesini önler.
    /// Log dosyasını küçültür (Shrink), veritabanı dosyasını sıkıştırır,
    /// tüm tabloların indekslerini baştan inşa eder (Reindex) ve sorgu istatistiklerini günceller.
    /// </summary>
    public static async Task<MaintenanceResult> ExecuteMaintenanceAsync()
    {
        var sw = Stopwatch.StartNew();
        var result = new MaintenanceResult();

        return await Task.Run(() =>
        {
            try
            {
                string dbName = Database.CurrentDatabase;
                result.InitialSizeMb = GetDatabaseSizeMb();

                using var conn = Database.Open();

                // 1. Recovery modelini SIMPLE yap (Log dosyasının şişmesini durdurur)
                try
                {
                    using var cmd1 = conn.CreateCommand();
                    cmd1.CommandText = $"ALTER DATABASE [{dbName}] SET RECOVERY SIMPLE WITH NO_WAIT;";
                    cmd1.CommandTimeout = 30;
                    cmd1.ExecuteNonQuery();
                }
                catch { }

                // 2. Checkpoint al
                try
                {
                    using var cmdChk = conn.CreateCommand();
                    cmdChk.CommandText = "CHECKPOINT;";
                    cmdChk.CommandTimeout = 30;
                    cmdChk.ExecuteNonQuery();
                }
                catch { }

                // 3. Log dosyasını bul ve küçült (Log Shrink)
                try
                {
                    string logFileName = "";
                    using (var cmdLogName = conn.CreateCommand())
                    {
                        cmdLogName.CommandText = "SELECT TOP 1 name FROM sys.database_files WHERE type_desc = 'LOG';";
                        var scalar = cmdLogName.ExecuteScalar();
                        if (scalar != null) logFileName = scalar.ToString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(logFileName))
                    {
                        using var cmdShrinkLog = conn.CreateCommand();
                        cmdShrinkLog.CommandText = $"DBCC SHRINKFILE ('{logFileName}', 1);";
                        cmdShrinkLog.CommandTimeout = 60;
                        cmdShrinkLog.ExecuteNonQuery();
                    }
                }
                catch { }

                // 4. Veritabanını sıkıştır (Database Shrink)
                try
                {
                    using var cmdShrinkDb = conn.CreateCommand();
                    cmdShrinkDb.CommandText = $"DBCC SHRINKDATABASE ([{dbName}], 5);";
                    cmdShrinkDb.CommandTimeout = 120;
                    cmdShrinkDb.ExecuteNonQuery();
                }
                catch { }

                // 5. Tüm tablolardaki parçalanmış indexleri yeniden inşa et (Rebuild Indexes)
                int tablesCount = 0;
                try
                {
                    var dtTables = new DataTable();
                    using (var cmdTables = conn.CreateCommand())
                    {
                        cmdTables.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';";
                        using var da = new SqlDataAdapter((SqlCommand)cmdTables);
                        da.Fill(dtTables);
                    }

                    foreach (DataRow row in dtTables.Rows)
                    {
                        string tableName = row["TABLE_NAME"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(tableName)) continue;

                        try
                        {
                            using var cmdReindex = conn.CreateCommand();
                            cmdReindex.CommandText = $"ALTER INDEX ALL ON [{tableName}] REBUILD;";
                            cmdReindex.CommandTimeout = 60;
                            cmdReindex.ExecuteNonQuery();
                            tablesCount++;
                        }
                        catch { }
                    }
                }
                catch { }
                result.OptimizedTablesCount = tablesCount;

                // 6. Sorgu optimizasyon istatistiklerini güncelle (Update Statistics)
                try
                {
                    using var cmdStats = conn.CreateCommand();
                    cmdStats.CommandText = "EXEC sp_updatestats;";
                    cmdStats.CommandTimeout = 60;
                    cmdStats.ExecuteNonQuery();
                }
                catch { }

                sw.Stop();
                result.Duration = sw.Elapsed;
                result.FinalSizeMb = GetDatabaseSizeMb();
                result.Success = true;

                AuditLogService.Log("Sistem", "Veritabanı Bakımı", null, "SQL Veritabanı Optimizasyonu", null, 
                    $"Başlangıç: {result.InitialSizeMb:N1} MB, Bitiş: {result.FinalSizeMb:N1} MB, Temizlenen: {result.FreedSpaceMb:N1} MB, Tablolar: {tablesCount}");

                result.Message = $"Veritabanı başarıyla optimize edildi!\n\n" +
                                 $"📊 Önceki Boyut: {result.InitialSizeMb:N2} MB\n" +
                                 $"📉 Yeni Boyut: {result.FinalSizeMb:N2} MB\n" +
                                 $"✨ Kazanılan Alan: {result.FreedSpaceMb:N2} MB\n" +
                                 $"⚡ Optimize Edilen Tablo Sayısı: {tablesCount}\n" +
                                 $"⏱️ İşlem Süresi: {result.Duration.TotalSeconds:N1} saniye";

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.Success = false;
                result.Duration = sw.Elapsed;
                result.Message = $"Veritabanı bakımı sırasında hata oluştu: {ex.Message}";
                return result;
            }
        });
    }

    public static double GetDatabaseSizeMb()
    {
        try
        {
            using var conn = Database.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT SUM(size * 8.0 / 1024.0) 
FROM sys.database_files;";
            var val = cmd.ExecuteScalar();
            return val != null && double.TryParse(val.ToString(), out double mb) ? mb : 0;
        }
        catch
        {
            return 0;
        }
    }
}
