using StokVeresiyeApp.Data;

namespace StokVeresiyeApp.Services;

public static class SettingsService
{
    public static string Get(string key, string defaultValue = "")
    {
        try
        {
            var dt = Database.Query("SELECT [Value] FROM AppSettings WHERE [Key] = @k;", ("@k", key));
            if (dt.Rows.Count > 0 && dt.Rows[0]["Value"] != DBNull.Value)
                return dt.Rows[0]["Value"]?.ToString() ?? defaultValue;
        }
        catch { }
        return defaultValue;
    }

    public static void Set(string key, string value)
    {
        try
        {
            Database.Execute(@"
IF EXISTS (SELECT 1 FROM AppSettings WHERE [Key] = @k)
    UPDATE AppSettings SET [Value] = @v WHERE [Key] = @k;
ELSE
    INSERT INTO AppSettings ([Key], [Value]) VALUES (@k, @v);",
                ("@k", key), ("@v", value));
        }
        catch { }
    }
}
