using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;
using System.Security.Cryptography;
using System.Text;

namespace StokVeresiyeApp.Services;

public static class UserService
{
    public static User? CurrentUser { get; private set; }

    /// <summary>Giriş yapan kişi, bilinen zayıf/varsayılan bir şifreyle girdiyse true olur: şifresini değiştirmesi istenir.</summary>
    public static bool MustChangePassword { get; private set; }

    private static readonly string[] WeakPasswords = { "123456", "1234", "12345", "admin", "admin123", "password", "367244" };

    // Kullanıcı adı olarak ayrılmış (yetki yükseltmeyi önlemek için): yalnızca süper kullanıcı oluşturabilir
    private static readonly string[] ReservedUsernames = { "super", "superuser" };

    public static void Initialize()
    {
        // Varsayılan kullanıcı veya şifre oluşturulmaz. İlk kurulumda yönetici şifresini kullanıcı belirler
        // (bkz. NeedsFirstRunSetup / CreateInitialAdmin).
    }

    // ---- Şifre karma (PBKDF2) ----
    // Biçim: pbkdf2-sha256$<iterasyon>$<tuz base64>$<hash base64>. Her şifrenin kendi rastgele tuzu vardır.
    private const string Pbkdf2Prefix = "pbkdf2-sha256";
    private const int Pbkdf2Iterations = 210_000;

    public static string HashPassword(string plainPassword)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(plainPassword, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Pbkdf2Prefix}${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Eski sürümlerden kalan (sabit tuzlu SHA-256) karma. Yalnızca eski kayıtları doğrulamak için.</summary>
    private static string LegacyHash(string plainPassword)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plainPassword + "_BILGE_SALT_2026"));
        return Convert.ToHexString(hash);
    }

    /// <summary>Şifreyi doğrular. needsUpgrade true ise kayıt eski biçimdedir ve yeni biçime çevrilmelidir.</summary>
    public static bool VerifyPassword(string plainPassword, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(storedHash)) return false;

        if (storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal))
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out int iterations)) return false;
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(plainPassword, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                bool ok = CryptographicOperations.FixedTimeEquals(actual, expected);
                needsUpgrade = ok && iterations < Pbkdf2Iterations;
                return ok;
            }
            catch { return false; }
        }

        // Eski biçim
        byte[] a = Encoding.UTF8.GetBytes(LegacyHash(plainPassword));
        byte[] b = Encoding.UTF8.GetBytes(storedHash.ToUpperInvariant());
        bool legacyOk = a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        needsUpgrade = legacyOk;
        return legacyOk;
    }

    public static bool IsWeakPassword(string password) =>
        password.Length < 6 || WeakPasswords.Contains(password, StringComparer.OrdinalIgnoreCase);

    // ---- İlk kurulum ----

    /// <summary>Hiç aktif (süper kullanıcı dışında) kullanıcı yoksa ilk kurulum gerekir.</summary>
    public static bool NeedsFirstRunSetup()
    {
        var n = Database.ExecuteScalar("SELECT COUNT(*) FROM Users WHERE IsActive = 1 AND LOWER(Username) <> 'super';");
        return n == null || Convert.ToInt64(n) == 0;
    }

    public static (bool Success, string Message) CreateInitialAdmin(string password)
    {
        if (IsWeakPassword(password))
            return (false, "Şifre en az 6 karakter olmalı ve yaygın bir şifre (123456, admin vb.) olmamalıdır.");

        Database.Execute(@"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);",
            ("@u", "admin"), ("@p", HashPassword(password)), ("@fn", "Sistem Yöneticisi"),
            ("@r", "Admin"), ("@perm", "ALL"), ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        return (true, "Yönetici hesabı oluşturuldu.");
    }

    // ---- Süper kullanıcı (lisans üretici) ----

    /// <summary>Süper kullanıcı hesabını oluşturur veya şifresini günceller. Yalnızca "--set-super" komutuyla çağrılır.</summary>
    public static void SetSuperUserPassword(string password)
    {
        string hash = HashPassword(password);
        int updated = Database.Execute("UPDATE Users SET PasswordHash = @p, IsActive = 1 WHERE LOWER(Username) = 'super';", ("@p", hash));
        if (updated == 0)
        {
            Database.Execute(@"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES ('super', @p, N'Süper Kullanıcı', 'SuperAdmin', 'ALL', 1, @created);",
                ("@p", hash), ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        }
    }

    /// <summary>Lisans üretici penceresini açmak için süper kullanıcı şifresini veritabanındaki karmayla doğrular.</summary>
    public static bool VerifySuperPassword(string password)
    {
        try
        {
            var stored = Database.ExecuteScalar("SELECT TOP 1 PasswordHash FROM Users WHERE LOWER(Username) = 'super' AND IsActive = 1;")?.ToString();
            if (string.IsNullOrEmpty(stored)) return false;
            if (!VerifyPassword(password, stored, out bool upgrade)) return false;
            if (upgrade)
            {
                Database.Execute("UPDATE Users SET PasswordHash = @p WHERE LOWER(Username) = 'super';", ("@p", HashPassword(password)));
            }
            return true;
        }
        catch { return false; }
    }

    public static bool SuperUserExists()
    {
        var n = Database.ExecuteScalar("SELECT COUNT(*) FROM Users WHERE LOWER(Username) = 'super' AND IsActive = 1;");
        return n != null && Convert.ToInt64(n) > 0;
    }

    public static void ChangePassword(long userId, string newPassword)
    {
        Database.Execute("UPDATE Users SET PasswordHash = @p WHERE Id = @id;", ("@p", HashPassword(newPassword)), ("@id", userId));
        AuditLogService.Log("Kullanıcı", "Şifre Değiştirildi", userId, null, null, "Kullanıcı şifresi güncellendi.");
        if (CurrentUser != null && CurrentUser.Id == userId) MustChangePassword = false;
    }

    public static (bool Success, string Message, User? User) Authenticate(string username, string password)
    {
        MustChangePassword = false;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Kullanıcı adı ve şifre boş bırakılamaz.", null);

        username = username.Trim().ToLowerInvariant();

        try
        {
            using var c = Database.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
SELECT Id, Username, PasswordHash, FullName, Role, Permissions, AssignedWarehouses, IsActive, CreatedAt
FROM Users
WHERE LOWER(Username) = @u;";
            cmd.Parameters.AddWithValue("@u", username);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return (false, "Kullanıcı adı veya şifre hatalı.", null);
            }

            bool isActive = Convert.ToBoolean(reader["IsActive"]);
            string dbHash = reader["PasswordHash"]?.ToString() ?? "";
            if (!VerifyPassword(password, dbHash, out bool needsUpgrade))
            {
                return (false, "Kullanıcı adı veya şifre hatalı.", null);
            }

            if (!isActive)
            {
                return (false, "Bu kullanıcı hesabı yönetici tarafından devre dışı bırakılmıştır.", null);
            }

            long userId = Convert.ToInt64(reader["Id"]);
            var user = new User
            {
                Id = userId,
                Username = reader["Username"]?.ToString() ?? "",
                PasswordHash = dbHash,
                FullName = reader["FullName"]?.ToString() ?? "",
                Role = reader["Role"]?.ToString() ?? "Kullanıcı",
                Permissions = reader["Permissions"]?.ToString() ?? "",
                AssignedWarehouses = reader.HasColumn("AssignedWarehouses") ? reader["AssignedWarehouses"]?.ToString() ?? "ALL" : "ALL",
                IsActive = true,
                CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var dt) ? dt : DateTime.Now
            };
            reader.Close();

            // Eski biçimli karmayı sessizce yeni biçime çevir
            if (needsUpgrade)
            {
                try
                {
                    using var up = c.CreateCommand();
                    up.CommandText = "UPDATE Users SET PasswordHash = @p WHERE Id = @id;";
                    up.Parameters.AddWithValue("@p", HashPassword(password));
                    up.Parameters.AddWithValue("@id", userId);
                    up.ExecuteNonQuery();
                }
                catch { }
            }

            CurrentUser = user;
            MustChangePassword = WeakPasswords.Contains(password, StringComparer.OrdinalIgnoreCase);
            return (true, "Giriş başarılı.", user);
        }
        catch (Exception ex)
        {
            return (false, "Giriş doğrulanırken hata oluştu: " + ex.Message, null);
        }
    }

    public static void Logout()
    {
        CurrentUser = null;
    }

    public static List<User> GetAllUsers()
    {
        var list = new List<User>();
        using var c = Database.Open();
        using var cmd = c.CreateCommand();

        // Eğer giriş yapan süper kullanıcı değilse, süper kullanıcı hesabını listede gizle
        if (CurrentUser?.IsSuperUser == true)
        {
            cmd.CommandText = "SELECT Id, Username, PasswordHash, FullName, Role, Permissions, AssignedWarehouses, IsActive, CreatedAt FROM Users ORDER BY Id ASC;";
        }
        else
        {
            cmd.CommandText = "SELECT Id, Username, PasswordHash, FullName, Role, Permissions, AssignedWarehouses, IsActive, CreatedAt FROM Users WHERE LOWER(Username) != 'super' AND Role != 'SuperAdmin' ORDER BY Id ASC;";
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new User
            {
                Id = Convert.ToInt64(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? "",
                PasswordHash = reader["PasswordHash"]?.ToString() ?? "",
                FullName = reader["FullName"]?.ToString() ?? "",
                Role = reader["Role"]?.ToString() ?? "Kullanıcı",
                Permissions = reader["Permissions"]?.ToString() ?? "",
                AssignedWarehouses = reader.HasColumn("AssignedWarehouses") ? reader["AssignedWarehouses"]?.ToString() ?? "ALL" : "ALL",
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var dt) ? dt : DateTime.Now
            });
        }
        return list;
    }

    public static (bool Success, string Message) AddUser(User user, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            return (false, "Kullanıcı adı boş olamaz.");

        if (string.IsNullOrWhiteSpace(plainPassword))
            return (false, "Şifre boş olamaz.");

        user.Username = user.Username.Trim().ToLowerInvariant();

        if (ReservedUsernames.Contains(user.Username) && CurrentUser?.IsSuperUser != true)
        {
            return (false, "Bu kullanıcı adı sistem tarafından ayrılmıştır.");
        }

        if (user.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) && CurrentUser?.IsSuperUser != true)
        {
            return (false, "SuperAdmin rolünü yalnızca süper kullanıcı atayabilir.");
        }

        try
        {
            using var c = Database.Open();
            // Aynı kullanıcı adı var mı?
            using (var check = c.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = @u;";
                check.Parameters.AddWithValue("@u", user.Username);
                if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                    return (false, $"'{user.Username}' kullanıcı adı zaten kullanımda.");
            }

            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, AssignedWarehouses, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, @wh, @act, @created);";
            cmd.Parameters.AddWithValue("@u", user.Username);
            cmd.Parameters.AddWithValue("@p", HashPassword(plainPassword));
            cmd.Parameters.AddWithValue("@fn", user.FullName.Trim());
            cmd.Parameters.AddWithValue("@r", user.Role);
            cmd.Parameters.AddWithValue("@perm", user.Permissions);
            cmd.Parameters.AddWithValue("@wh", string.IsNullOrWhiteSpace(user.AssignedWarehouses) ? "ALL" : user.AssignedWarehouses);
            cmd.Parameters.AddWithValue("@act", user.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();

            AuditLogService.Log("Kullanıcı", "Ekleme", null, user.Username, null, $"{user.FullName} ({user.Role}) eklendi.");
            return (true, "Kullanıcı başarıyla oluşturuldu.");
        }
        catch (Exception ex)
        {
            return (false, "Kullanıcı kaydedilemedi: " + ex.Message);
        }
    }

    public static (bool Success, string Message) UpdateUser(User user, string? newPlainPassword = null)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            return (false, "Kullanıcı adı boş olamaz.");

        user.Username = user.Username.Trim().ToLowerInvariant();

        if ((ReservedUsernames.Contains(user.Username) || user.Role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)) && CurrentUser?.IsSuperUser != true)
        {
            return (false, "Süper kullanıcı hesabını yalnızca süper kullanıcı düzenleyebilir.");
        }

        try
        {
            using var c = Database.Open();
            // Başka kullanıcıda bu username var mı?
            using (var check = c.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = @u AND Id != @id;";
                check.Parameters.AddWithValue("@u", user.Username);
                check.Parameters.AddWithValue("@id", user.Id);
                if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                    return (false, $"'{user.Username}' kullanıcı adı başka bir kullanıcıya ait.");
            }

            using var cmd = c.CreateCommand();
            if (!string.IsNullOrWhiteSpace(newPlainPassword))
            {
                cmd.CommandText = @"
UPDATE Users 
SET Username = @u, PasswordHash = @p, FullName = @fn, Role = @r, Permissions = @perm, AssignedWarehouses = @wh, IsActive = @act
WHERE Id = @id;";
                cmd.Parameters.AddWithValue("@p", HashPassword(newPlainPassword));
            }
            else
            {
                cmd.CommandText = @"
UPDATE Users 
SET Username = @u, FullName = @fn, Role = @r, Permissions = @perm, AssignedWarehouses = @wh, IsActive = @act
WHERE Id = @id;";
            }

            cmd.Parameters.AddWithValue("@u", user.Username);
            cmd.Parameters.AddWithValue("@fn", user.FullName.Trim());
            cmd.Parameters.AddWithValue("@r", user.Role);
            cmd.Parameters.AddWithValue("@perm", user.Permissions);
            cmd.Parameters.AddWithValue("@wh", string.IsNullOrWhiteSpace(user.AssignedWarehouses) ? "ALL" : user.AssignedWarehouses);
            cmd.Parameters.AddWithValue("@act", user.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", user.Id);
            cmd.ExecuteNonQuery();

            AuditLogService.Log("Kullanıcı", "Güncelleme", user.Id, user.Username, null, $"{user.FullName} güncellendi.");
            return (true, "Kullanıcı bilgileri güncellendi.");
        }
        catch (Exception ex)
        {
            return (false, "Kullanıcı güncellenemedi: " + ex.Message);
        }
    }

    public static (bool Success, string Message) DeleteUser(long userId)
    {
        try
        {
            using var c = Database.Open();
            using var check = c.CreateCommand();
            check.CommandText = "SELECT Username, Role FROM Users WHERE Id = @id;";
            check.Parameters.AddWithValue("@id", userId);
            using var reader = check.ExecuteReader();
            if (!reader.Read())
                return (false, "Kullanıcı bulunamadı.");

            string uname = reader["Username"]?.ToString() ?? "";
            string role = reader["Role"]?.ToString() ?? "";

            if (uname.Equals("super", StringComparison.OrdinalIgnoreCase) || role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Süper Kullanıcı hesabı sistem koruması altındadır ve silinemez!");
            }

            if (uname.Equals("admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Sistem ana yöneticisi (Admin) silinemez!");
            }

            if (CurrentUser != null && CurrentUser.Id == userId)
            {
                return (false, "Şu anda oturum açmış olduğunuz kendi hesabınızı silemezsiniz.");
            }

            reader.Close();

            using var delCmd = c.CreateCommand();
            delCmd.CommandText = "DELETE FROM Users WHERE Id = @id;";
            delCmd.Parameters.AddWithValue("@id", userId);
            delCmd.ExecuteNonQuery();

            AuditLogService.Log("Kullanıcı", "Silme", userId, uname, null, "Kullanıcı hesabı silindi.");
            return (true, "Kullanıcı silindi.");
        }
        catch (Exception ex)
        {
            return (false, "Kullanıcı silinemedi: " + ex.Message);
        }
    }
}

internal static class SqlDataReaderExtensions
{
    public static bool HasColumn(this Microsoft.Data.SqlClient.SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

