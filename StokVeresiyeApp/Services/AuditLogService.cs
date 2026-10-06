using System.Data;
using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public static class AuditLogService
{
    public static void Log(
        string entityName,       // "Cari", "CariHareket", "Urun", "StokHareket"
        string actionType,       // "Silindi", "Güncellendi", "Yeni Eklendi"
        long? entityId,
        string entityTitle,      // "Ahmet Yılmaz (Müşteri)"
        string? oldValues = null,
        string? newValues = null,
        string? description = null)
    {
        try
        {
            Database.Execute(@"
INSERT INTO AuditLogs(LogDate, EntityName, ActionType, EntityId, EntityTitle, OldValues, NewValues, Description)
VALUES($d, $e, $a, $id, $t, $old, $new, $desc);
",
                ("$d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                ("$e", entityName),
                ("$a", actionType),
                ("$id", entityId.HasValue ? entityId.Value : (object)DBNull.Value),
                ("$t", entityTitle ?? ""),
                ("$old", oldValues ?? (object)DBNull.Value),
                ("$new", newValues ?? (object)DBNull.Value),
                ("$desc", description ?? (object)DBNull.Value));
        }
        catch
        {
            // Log hatası ana işlemi durdurmasın
        }
    }

    public static DataTable GetLogs(string? entityName = null, string? actionType = null, DateTime? start = null, DateTime? end = null, string? search = null)
    {
        var sql = @"
SELECT TOP 500
    Id,
    LogDate AS [İşlem Tarihi],
    EntityName AS [Kayıt Türü],
    ActionType AS [İşlem],
    EntityTitle AS [İlgili Kayıt / Başlık],
    COALESCE(Description, '') AS [Açıklama],
    COALESCE(OldValues, '') AS [Önceki Bilgiler],
    COALESCE(NewValues, '') AS [Yeni Bilgiler]
FROM AuditLogs
WHERE 1=1
";
        var conditions = new List<string>();
        var parameters = new List<(string, object?)>();

        if (!string.IsNullOrWhiteSpace(entityName) && entityName != "Tümü")
        {
            conditions.Add("EntityName = $e");
            parameters.Add(("$e", entityName));
        }

        if (!string.IsNullOrWhiteSpace(actionType) && actionType != "Tümü")
        {
            conditions.Add("ActionType = $a");
            parameters.Add(("$a", actionType));
        }

        if (start.HasValue)
        {
            conditions.Add("LogDate >= $s");
            parameters.Add(("$s", start.Value.ToString("yyyy-MM-dd 00:00:00")));
        }

        if (end.HasValue)
        {
            conditions.Add("LogDate <= $eDate");
            parameters.Add(("$eDate", end.Value.ToString("yyyy-MM-dd 23:59:59")));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(EntityTitle LIKE $q OR Description LIKE $q OR OldValues LIKE $q OR NewValues LIKE $q)");
            parameters.Add(("$q", $"%{search.Trim()}%"));
        }

        if (conditions.Count > 0)
        {
            sql += " AND " + string.Join(" AND ", conditions);
        }

        sql += " ORDER BY Id DESC";

        return Database.Query(sql, parameters.ToArray());
    }
}
