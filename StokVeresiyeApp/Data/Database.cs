using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StokVeresiyeApp.Data;

public class DatabaseConfig
{
    public string Server { get; set; } = @"(localdb)\MSSQLLocalDB";
    public string DatabaseName { get; set; } = "BilgeStokDB";
    public bool IntegratedSecurity { get; set; } = true;
    public string? UserId { get; set; }
    public string? Password { get; set; }
    public bool TrustServerCertificate { get; set; } = true;
    public int ConnectTimeout { get; set; } = 10;

    public string BuildConnectionString(string? overrideDb = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = overrideDb ?? DatabaseName,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectTimeout,
            MultipleActiveResultSets = true
        };

        if (IntegratedSecurity)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = UserId ?? "";
            builder.Password = Password ?? "";
        }

        return builder.ConnectionString;
    }
}

public static class Database
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StokVeresiyeApp");
    private static readonly string ConfigPath = Path.Combine(Folder, "dbconfig.json");

    private static DatabaseConfig _config = new();

    public static DatabaseConfig Config => _config;
    public static string CurrentServer => _config.Server;
    public static string CurrentDatabase => _config.DatabaseName;

    static Database()
    {
        LoadConfig();
    }

    public static void LoadConfig()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<DatabaseConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                    return;
                }
            }
        }
        catch { }

        _config = new DatabaseConfig();
        SaveConfig();
    }

    public static void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static SqlConnection Open()
    {
        EnsureLocalDbStarted();
        var conn = new SqlConnection(_config.BuildConnectionString());
        conn.Open();
        return conn;
    }

    public static void EnsureLocalDbStarted()
    {
        if (_config.Server.Contains("localdb", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "start MSSQLLocalDB",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(3000);
            }
            catch { }
        }
    }

    public static void Initialize()
    {
        EnsureLocalDbStarted();

        // 1. Veritabanının varlığını master üzerinden kontrol et ve yoksa oluştur
        try
        {
            string masterCs = _config.BuildConnectionString("master");
            using (var masterConn = new SqlConnection(masterCs))
            {
                masterConn.Open();
                using var checkCmd = masterConn.CreateCommand();
                checkCmd.CommandText = $@"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{_config.DatabaseName}')
BEGIN
    CREATE DATABASE [{_config.DatabaseName}];
END";
                checkCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Master DB check error: " + ex.Message);
            throw new Exception($"SQL Server sunucusuna ('{_config.Server}') ulaşılamadı veya bağlantı reddedildi.\n\nSistem Hatası: {ex.Message}", ex);
        }

        EnsureSchema();
    }

    public static void EnsureSchema()
    {
        try
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
CREATE TABLE Products (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(100) NOT NULL UNIQUE,
    Barcode NVARCHAR(100) NULL,
    Name NVARCHAR(250) NOT NULL,
    Category NVARCHAR(100) NOT NULL DEFAULT 'Genel',
    Unit NVARCHAR(50) NOT NULL DEFAULT 'Adet',
    OpeningStock FLOAT NOT NULL DEFAULT 0,
    PurchasePrice FLOAT NOT NULL DEFAULT 0,
    SalePrice FLOAT NOT NULL DEFAULT 0,
    WholesalePrice FLOAT NOT NULL DEFAULT 0,
    SpecialPrice FLOAT NOT NULL DEFAULT 0,
    DiscountPercent FLOAT NOT NULL DEFAULT 0,
    VatPercent FLOAT NOT NULL DEFAULT 20,
    MinStockLevel FLOAT NOT NULL DEFAULT 5,
    IsActive BIT NOT NULL DEFAULT 1
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Warehouses')
CREATE TABLE Warehouses (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL UNIQUE,
    Code NVARCHAR(50) NULL,
    Type NVARCHAR(50) NOT NULL DEFAULT 'Depo',
    ResponsiblePerson NVARCHAR(100) NULL,
    Phone NVARCHAR(50) NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Accounts')
CREATE TABLE Accounts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(250) NOT NULL UNIQUE,
    Type NVARCHAR(50) NOT NULL,
    Phone NVARCHAR(50) NULL,
    Email NVARCHAR(150) NULL,
    Address NVARCHAR(500) NULL,
    TaxOffice NVARCHAR(100) NULL,
    TaxNumber NVARCHAR(100) NULL,
    PriceGroup NVARCHAR(50) NOT NULL DEFAULT 'Perakende',
    DefaultDiscountPercent FLOAT NOT NULL DEFAULT 0,
    BalanceLimit FLOAT NOT NULL DEFAULT 0,
    IsBlacklisted BIT NOT NULL DEFAULT 0,
    Description NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockMovements')
CREATE TABLE StockMovements (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MovementDate NVARCHAR(50) NOT NULL,
    ProductId BIGINT NOT NULL,
    MovementType NVARCHAR(50) NOT NULL,
    Quantity FLOAT NOT NULL,
    UnitPrice FLOAT NOT NULL DEFAULT 0,
    DocumentNo NVARCHAR(100) NULL,
    AccountId BIGINT NULL,
    WarehouseId BIGINT NULL,
    TargetWarehouseId BIGINT NULL,
    Note NVARCHAR(MAX) NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountMovements')
CREATE TABLE AccountMovements (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MovementDate NVARCHAR(50) NOT NULL,
    AccountId BIGINT NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL,
    DocumentNo NVARCHAR(100) NULL,
    Amount FLOAT NOT NULL,
    Method NVARCHAR(50) DEFAULT 'Nakit',
    CashBank NVARCHAR(100) DEFAULT 'Merkez Kasa',
    Note NVARCHAR(MAX) NULL,
    DueDate NVARCHAR(50) NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
CREATE TABLE AuditLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    LogDate NVARCHAR(50) NOT NULL,
    EntityName NVARCHAR(100) NOT NULL,
    ActionType NVARCHAR(100) NOT NULL,
    EntityId BIGINT NULL,
    EntityTitle NVARCHAR(250) NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
CREATE TABLE Users (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Kullanıcı',
    Permissions NVARCHAR(MAX) NOT NULL DEFAULT '',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt NVARCHAR(50) NOT NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppLicense')
CREATE TABLE AppLicense (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MachineCode NVARCHAR(100) NOT NULL,
    LicenseKey NVARCHAR(MAX) NOT NULL,
    ActivatedDate NVARCHAR(50) NOT NULL,
    ExpireDate NVARCHAR(50) NOT NULL,
    LicensedTo NVARCHAR(250) NULL,
    Status NVARCHAR(50) NOT NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invoices')
CREATE TABLE Invoices (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    InvoiceNumber NVARCHAR(100) NOT NULL,
    InvoiceDate NVARCHAR(50) NOT NULL,
    InvoiceType NVARCHAR(50) NOT NULL DEFAULT 'Alış Faturası',
    AccountId BIGINT NOT NULL,
    WarehouseId BIGINT NOT NULL,
    SubTotal FLOAT NOT NULL DEFAULT 0,
    VatTotal FLOAT NOT NULL DEFAULT 0,
    GrandTotal FLOAT NOT NULL DEFAULT 0,
    PdfPath NVARCHAR(500) NULL,
    PdfData VARBINARY(MAX) NULL,
    Note NVARCHAR(MAX) NULL,
    CreatedAt NVARCHAR(50) NOT NULL
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InvoiceItems')
CREATE TABLE InvoiceItems (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    ProductId BIGINT NULL,
    [LineNo] INT NOT NULL DEFAULT 1,
    Barcode NVARCHAR(100) NULL,
    ItemCode NVARCHAR(100) NULL,
    ItemName NVARCHAR(250) NOT NULL,
    Quantity FLOAT NOT NULL,
    Unit NVARCHAR(50) NOT NULL DEFAULT 'Adet',
    UnitPrice FLOAT NOT NULL,
    DiscountPercent FLOAT NOT NULL DEFAULT 0,
    DiscountAmount FLOAT NOT NULL DEFAULT 0,
    VatPercent FLOAT NOT NULL DEFAULT 20,
    VatAmount FLOAT NOT NULL DEFAULT 0,
    OtherTaxes FLOAT NOT NULL DEFAULT 0,
    LineTotal FLOAT NOT NULL
);

-- Eski veritabanları için ilave kolon kontrolleri (ALTER TABLE)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PdfData')
    ALTER TABLE Invoices ADD PdfData VARBINARY(MAX) NULL;

-- Fatura Meta Bilgileri (Özelleştirme No, Senaryo, Fatura Tipi, Sipariş No/Tarihi, İlgili Kitabevi, Kargo ID, Ref No, Düzenleme Saati)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'CustomizationId')
    ALTER TABLE Invoices ADD CustomizationId NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'Scenario')
    ALTER TABLE Invoices ADD Scenario NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'InvoiceKind')
    ALTER TABLE Invoices ADD InvoiceKind NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'OrderNumber')
    ALTER TABLE Invoices ADD OrderNumber NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'OrderDate')
    ALTER TABLE Invoices ADD OrderDate NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'RelatedStore')
    ALTER TABLE Invoices ADD RelatedStore NVARCHAR(250) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'CargoId')
    ALTER TABLE Invoices ADD CargoId NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'ReferenceNo')
    ALTER TABLE Invoices ADD ReferenceNo NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'IssueTime')
    ALTER TABLE Invoices ADD IssueTime NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PaidAmount')
    ALTER TABLE Invoices ADD PaidAmount FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'PaymentStatus')
    ALTER TABLE Invoices ADD PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Ödenmedi';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InvoicePayments')
CREATE TABLE InvoicePayments (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    PaymentDate NVARCHAR(50) NOT NULL,
    Amount FLOAT NOT NULL,
    PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Nakit',
    AccountMovementId BIGINT NULL,
    Note NVARCHAR(MAX) NULL,
    CreatedAt NVARCHAR(50) NOT NULL
);

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'Barcode')
    ALTER TABLE InvoiceItems ADD Barcode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'LineNo')
    ALTER TABLE InvoiceItems ADD [LineNo] INT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'DiscountPercent')
    ALTER TABLE InvoiceItems ADD DiscountPercent FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'DiscountAmount')
    ALTER TABLE InvoiceItems ADD DiscountAmount FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'OtherTaxes')
    ALTER TABLE InvoiceItems ADD OtherTaxes FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'WholesalePrice')
    ALTER TABLE Products ADD WholesalePrice FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'SpecialPrice')
    ALTER TABLE Products ADD SpecialPrice FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'PriceGroup')
    ALTER TABLE Accounts ADD PriceGroup NVARCHAR(50) NOT NULL DEFAULT 'Perakende';

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'DefaultDiscountPercent')
    ALTER TABLE Accounts ADD DefaultDiscountPercent FLOAT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accounts') AND name = 'IsBlacklisted')
    ALTER TABLE Accounts ADD IsBlacklisted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockMovements') AND name = 'WarehouseId')
    ALTER TABLE StockMovements ADD WarehouseId BIGINT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockMovements') AND name = 'TargetWarehouseId')
    ALTER TABLE StockMovements ADD TargetWarehouseId BIGINT NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AccountMovements') AND name = 'DueDate')
    ALTER TABLE AccountMovements ADD DueDate NVARCHAR(50) NULL;

-- SKT ve Parti/Lot Takibi Kolonları
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ExpiryDate')
    ALTER TABLE Products ADD ExpiryDate NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'BatchNumber')
    ALTER TABLE Products ADD BatchNumber NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'ExpiryDate')
    ALTER TABLE InvoiceItems ADD ExpiryDate NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'BatchNumber')
    ALTER TABLE InvoiceItems ADD BatchNumber NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockMovements') AND name = 'ExpiryDate')
    ALTER TABLE StockMovements ADD ExpiryDate NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('StockMovements') AND name = 'BatchNumber')
    ALTER TABLE StockMovements ADD BatchNumber NVARCHAR(100) NULL;

-- Ürün Özellikleri / Formu (Sprey, Sıvı, Tablet, Toz vb.)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Features')
    ALTER TABLE Products ADD Features NVARCHAR(200) NULL;

-- Kullanıcı Depo Yetkisi ('ALL' veya '1,2' gibi virgülle ayrılmış depo Id'leri)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'AssignedWarehouses')
    ALTER TABLE Users ADD AssignedWarehouses NVARCHAR(500) NOT NULL DEFAULT 'ALL';

-- Ürün Fiyat & Stok Geliş Tarihçesi (Faturadan veya Manuel Eklenen Parti/Fiyat/Stok Kayıtları)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductPriceHistory')
CREATE TABLE ProductPriceHistory (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    ChangeDate NVARCHAR(50) NOT NULL,
    DocumentNo NVARCHAR(100) NULL,
    SupplierName NVARCHAR(250) NULL,
    OldStock FLOAT NOT NULL DEFAULT 0,
    AddedStock FLOAT NOT NULL DEFAULT 0,
    NewStock FLOAT NOT NULL DEFAULT 0,
    OldPurchasePrice FLOAT NOT NULL DEFAULT 0,
    NewPurchasePrice FLOAT NOT NULL DEFAULT 0,
    OldSalePrice FLOAT NOT NULL DEFAULT 0,
    NewSalePrice FLOAT NOT NULL DEFAULT 0,
    Note NVARCHAR(MAX) NULL
);

-- Varyant Yönetimi (Renk, Beden, Numara, Model, Barkod ve Fiyat Farkı)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductVariants')
CREATE TABLE ProductVariants (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    VariantName NVARCHAR(100) NOT NULL,
    Barcode NVARCHAR(100) NULL,
    Sku NVARCHAR(100) NULL,
    PriceDifference FLOAT NOT NULL DEFAULT 0,
    StockQuantity FLOAT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1
);

-- Kasa Gün Sonu & Z Raporu Kapatma Tablosu
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyRegisterClosings')
CREATE TABLE DailyRegisterClosings (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ClosingDate NVARCHAR(50) NOT NULL,
    ClosedBy NVARCHAR(100) NOT NULL,
    OpeningCash FLOAT NOT NULL DEFAULT 0,
    CashSales FLOAT NOT NULL DEFAULT 0,
    CardSales FLOAT NOT NULL DEFAULT 0,
    TransferSales FLOAT NOT NULL DEFAULT 0,
    CashExpenses FLOAT NOT NULL DEFAULT 0,
    ExpectedCash FLOAT NOT NULL DEFAULT 0,
    CountedCash FLOAT NOT NULL DEFAULT 0,
    DifferenceCash FLOAT NOT NULL DEFAULT 0,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt NVARCHAR(50) NOT NULL
);

-- Ürün Adı, Barkod, Ürün Kodu ve Fatura Kalem Kolonlarını Güvenle Genişlet (Truncation Hatasını Önleme)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Name' AND max_length < 1000)
    ALTER TABLE Products ALTER COLUMN Name NVARCHAR(500) NOT NULL;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Code' AND max_length < 300)
    ALTER TABLE Products ALTER COLUMN Code NVARCHAR(150) NOT NULL;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'Barcode' AND max_length < 300)
    ALTER TABLE Products ALTER COLUMN Barcode NVARCHAR(150) NULL;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'ItemName' AND max_length < 1000)
    ALTER TABLE InvoiceItems ALTER COLUMN ItemName NVARCHAR(500) NOT NULL;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'Barcode' AND max_length < 300)
    ALTER TABLE InvoiceItems ALTER COLUMN Barcode NVARCHAR(150) NULL;

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InvoiceItems') AND name = 'ItemCode' AND max_length < 300)
    ALTER TABLE InvoiceItems ALTER COLUMN ItemCode NVARCHAR(150) NULL;

-- İndeksler
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Stock_Product')
    CREATE NONCLUSTERED INDEX IX_Stock_Product ON StockMovements(ProductId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountMovement_Account')
    CREATE NONCLUSTERED INDEX IX_AccountMovement_Account ON AccountMovements(AccountId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_Date')
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Date ON AuditLogs(LogDate);
";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("EnsureSchema error: " + ex.Message);
            throw new Exception($"Veritabanı tabloları oluşturulamadı: {ex.Message}", ex);
        }

        // 3. Süper Kullanıcı ve Admin kullanıcılarını hazırla
        SeedDefaultUsers();

        // 5. Varsayılan Merkez Depoyu hazırla
        SeedDefaultWarehouse();
    }

    private static void SeedDefaultWarehouse()
    {
        try
        {
            using var c = Open();
            using var check = c.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Warehouses;";
            if (Convert.ToInt64(check.ExecuteScalar()) == 0)
            {
                using var ins = c.CreateCommand();
                ins.CommandText = @"
INSERT INTO Warehouses (Name, Code, Type, ResponsiblePerson, Phone, IsDefault, IsActive)
VALUES ('Merkez Depo', 'DEP-01', 'Depo', 'Depo Sorumlusu', '', 1, 1);
DECLARE @defId BIGINT = SCOPE_IDENTITY();
UPDATE StockMovements SET WarehouseId = @defId WHERE WarehouseId IS NULL;
";
                ins.ExecuteNonQuery();
            }
            else
            {
                using var upd = c.CreateCommand();
                upd.CommandText = @"
DECLARE @defId BIGINT = (SELECT TOP 1 Id FROM Warehouses WHERE IsDefault = 1);
IF @defId IS NULL SET @defId = (SELECT TOP 1 Id FROM Warehouses ORDER BY Id);
IF @defId IS NOT NULL UPDATE StockMovements SET WarehouseId = @defId WHERE WarehouseId IS NULL;
";
                upd.ExecuteNonQuery();
            }
        }
        catch { }
    }

    private static string HashPassword(string plainPassword)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(plainPassword + "_BILGE_SALT_2026"));
        return Convert.ToHexString(hash);
    }

    private static void SeedDefaultUsers()
    {
        try
        {
            using var c = Open();

            // 1. Süper Kullanıcı (super / 367244)
            using (var checkSuper = c.CreateCommand())
            {
                checkSuper.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = 'super';";
                if (Convert.ToInt64(checkSuper.ExecuteScalar()) == 0)
                {
                    using var insSuper = c.CreateCommand();
                    insSuper.CommandText = @"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);";
                    insSuper.Parameters.AddWithValue("@u", "super");
                    insSuper.Parameters.AddWithValue("@p", HashPassword("367244"));
                    insSuper.Parameters.AddWithValue("@fn", "Süper Kullanıcı");
                    insSuper.Parameters.AddWithValue("@r", "SuperAdmin");
                    insSuper.Parameters.AddWithValue("@perm", "ALL");
                    insSuper.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insSuper.ExecuteNonQuery();
                }
            }

            // 2. Admin Kullanıcı (admin / 123456)
            using (var checkAdmin = c.CreateCommand())
            {
                checkAdmin.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(Username) = 'admin';";
                if (Convert.ToInt64(checkAdmin.ExecuteScalar()) == 0)
                {
                    using var insAdmin = c.CreateCommand();
                    insAdmin.CommandText = @"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);";
                    insAdmin.Parameters.AddWithValue("@u", "admin");
                    insAdmin.Parameters.AddWithValue("@p", HashPassword("123456"));
                    insAdmin.Parameters.AddWithValue("@fn", "Sistem Yöneticisi");
                    insAdmin.Parameters.AddWithValue("@r", "Admin");
                    insAdmin.Parameters.AddWithValue("@perm", "ALL");
                    insAdmin.Parameters.AddWithValue("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    insAdmin.ExecuteNonQuery();
                }
            }
        }
        catch { }
    }

    public static string NormalizeSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return sql;

        // $id -> @id
        string normalized = sql.Replace("$", "@");

        // || -> + (String concatenation)
        // Örn: Code || ' - ' || Name -> Code + ' - ' + Name
        if (normalized.Contains("||"))
        {
            normalized = normalized.Replace("||", "+");
        }

        return normalized;
    }

    public static DataTable Query(string sql, params (string Name, object? Value)[] p)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = NormalizeSql(sql);
        foreach (var x in p)
        {
            string pName = x.Name.StartsWith("@") ? x.Name : (x.Name.StartsWith("$") ? "@" + x.Name[1..] : "@" + x.Name);
            cmd.Parameters.AddWithValue(pName, x.Value ?? DBNull.Value);
        }
        using var r = cmd.ExecuteReader();
        var t = new DataTable();
        t.Load(r);
        return t;
    }

    public static int Execute(string sql, params (string Name, object? Value)[] p)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = NormalizeSql(sql);
        foreach (var x in p)
        {
            string pName = x.Name.StartsWith("@") ? x.Name : (x.Name.StartsWith("$") ? "@" + x.Name[1..] : "@" + x.Name);
            cmd.Parameters.AddWithValue(pName, x.Value ?? DBNull.Value);
        }
        return cmd.ExecuteNonQuery();
    }

    public static (string Name, object? Value) Param(string name, object? value) => (name, value);

    public static object? ExecuteScalar(string sql, params (string Name, object? Value)[] p)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = NormalizeSql(sql);
        foreach (var x in p)
        {
            string pName = x.Name.StartsWith("@") ? x.Name : (x.Name.StartsWith("$") ? "@" + x.Name[1..] : "@" + x.Name);
            cmd.Parameters.AddWithValue(pName, x.Value ?? DBNull.Value);
        }
        return cmd.ExecuteScalar();
    }

    public static T? ExecuteScalar<T>(string sql, params (string Name, object? Value)[] p)
    {
        var res = ExecuteScalar(sql, p);
        if (res == null || res == DBNull.Value) return default;
        return (T)Convert.ChangeType(res, typeof(T));
    }

    public static void BackupDatabase(string targetFilePath)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"BACKUP DATABASE [{_config.DatabaseName}] TO DISK = @path WITH FORMAT, INIT;";
        cmd.Parameters.AddWithValue("@path", targetFilePath);
        cmd.ExecuteNonQuery();
    }

    public static void RestoreDatabase(string sourceFilePath)
    {
        string masterCs = _config.BuildConnectionString("master");
        using var c = new SqlConnection(masterCs);
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
ALTER DATABASE [{_config.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [{_config.DatabaseName}] FROM DISK = @path WITH REPLACE;
ALTER DATABASE [{_config.DatabaseName}] SET MULTI_USER;";
        cmd.Parameters.AddWithValue("@path", sourceFilePath);
        cmd.ExecuteNonQuery();

        // Eski yedek yüklendikten sonra yeni eklenen tüm kolonları (WholesalePrice, SpecialPrice vb.) otomatik ekle
        EnsureSchema();
    }
}
