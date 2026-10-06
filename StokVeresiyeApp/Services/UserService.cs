using Microsoft.Data.SqlClient;
using StokVeresiyeApp.Data;
using StokVeresiyeApp.Models;
using System.Security.Cryptography;
using System.Text;

namespace StokVeresiyeApp.Services;

public static class UserService
{
    public static User? CurrentUser { get; private set; }

    public static void Initialize()
    {
        using var c = Database.Open();

        // 1. Süper Kullanıcı Kontrolü (super / 367244)
        using (var checkSuper = c.CreateCommand())
        {
            checkSuper.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = 'super';";
            long countSuper = Convert.ToInt64(checkSuper.ExecuteScalar());

            if (countSuper == 0)
            {
                using var insertSuper = c.CreateCommand();
                insertSuper.CommandText = @"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);";
                insertSuper.Parameters.AddWithValue("@u", "super");
                insertSuper.Parameters.AddWithValue("@p", HashPassword("367244"));
                insertSuper.Parameters.AddWithValue("@fn", "Süper Kullanıcı");
                insertSuper.Parameters.AddWithValue("@r", "SuperAdmin");
                insertSuper.Parameters.AddWithValue("@perm", "ALL");
                insertSuper.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insertSuper.ExecuteNonQuery();
            }
        }

        // 2. Admin Kullanıcı Kontrolü (admin / 123456)
        using (var checkAdmin = c.CreateCommand())
        {
            checkAdmin.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = 'admin';";
            long countAdmin = Convert.ToInt64(checkAdmin.ExecuteScalar());

            if (countAdmin == 0)
            {
                using var insertAdmin = c.CreateCommand();
                insertAdmin.CommandText = @"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);";
                insertAdmin.Parameters.AddWithValue("@u", "admin");
                insertAdmin.Parameters.AddWithValue("@p", HashPassword("123456"));
                insertAdmin.Parameters.AddWithValue("@fn", "Sistem Yöneticisi");
                insertAdmin.Parameters.AddWithValue("@r", "Admin");
                insertAdmin.Parameters.AddWithValue("@perm", "ALL");
                insertAdmin.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insertAdmin.ExecuteNonQuery();
            }
        }
    }

    public static string HashPassword(string plainPassword)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plainPassword + "_BILGE_SALT_2026"));
        return Convert.ToHexString(hash);
    }

    public static (bool Success, string Message, User? User) Authenticate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return (false, "Kullanıcı adı ve şifre boş bırakılamaz.", null);

        username = username.Trim().ToLowerInvariant();
        string hashed = HashPassword(password);

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
                return (false, "Girilen kullanıcı adı bulunamadı.", null);
            }

            bool isActive = Convert.ToBoolean(reader["IsActive"]);
            if (!isActive)
            {
                return (false, "Bu kullanıcı hesabı yönetici tarafından devre dışı bırakılmıştır.", null);
            }

            string dbHash = reader["PasswordHash"]?.ToString() ?? "";
            if (!string.Equals(dbHash, hashed, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Hatalı şifre girdiniz. Lütfen tekrar deneyiniz.", null);
            }

            var user = new User
            {
                Id = Convert.ToInt64(reader["Id"]),
                Username = reader["Username"]?.ToString() ?? "",
                PasswordHash = dbHash,
                FullName = reader["FullName"]?.ToString() ?? "",
                Role = reader["Role"]?.ToString() ?? "Kullanıcı",
                Permissions = reader["Permissions"]?.ToString() ?? "",
                AssignedWarehouses = reader.HasColumn("AssignedWarehouses") ? reader["AssignedWarehouses"]?.ToString() ?? "ALL" : "ALL",
                IsActive = true,
                CreatedAt = DateTime.TryParse(reader["CreatedAt"]?.ToString(), out var dt) ? dt : DateTime.Now
            };

            CurrentUser = user;
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

        if (user.Username == "super" && CurrentUser?.IsSuperUser != true)
        {
            return (false, "'super' kullanıcı adı sistem tarafından ayrılmıştır.");
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

        if (user.Username == "super" && CurrentUser?.IsSuperUser != true)
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

