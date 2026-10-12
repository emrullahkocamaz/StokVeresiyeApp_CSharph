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
        _localDbStarted = false;
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

    private static volatile bool _localDbStarted;

    public static void EnsureLocalDbStarted(bool force = false)
    {
        if (_localDbStarted && !force) return;

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

        _localDbStarted = true;
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

    /// <summary>
    /// Para, miktar ve yüzde alanlarını FLOAT'tan DECIMAL'e çevirir (kuruş hataları birikmesin diye).
    /// Tek transaction'dır; bir kez çalışır, sonraki açılışlarda FLOAT sütun kalmadığı için atlanır.
    /// Yüzde alanları DECIMAL(9,4), diğerleri DECIMAL(18,4).
    /// </summary>
    private static void EnsureDecimalColumns(SqlConnection c)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandTimeout = 300;
            cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND DATA_TYPE IN ('float', 'real'))
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        -- Sayısal sütunları içeren (dahil edilmiş sütun dahil) bizim oluşturduğumuz indeksler yeniden kurulacak
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Stock_Date') DROP INDEX IX_Stock_Date ON StockMovements;

        DECLARE @t SYSNAME, @col SYSNAME, @nullable VARCHAR(3), @dfName SYSNAME, @dfDef NVARCHAR(MAX), @sql NVARCHAR(MAX), @type NVARCHAR(20);
        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT c.TABLE_NAME, c.COLUMN_NAME, c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.TABLES tb ON tb.TABLE_SCHEMA = c.TABLE_SCHEMA AND tb.TABLE_NAME = c.TABLE_NAME AND tb.TABLE_TYPE = 'BASE TABLE'
            WHERE c.TABLE_SCHEMA = 'dbo' AND c.DATA_TYPE IN ('float', 'real');
        OPEN cur;
        FETCH NEXT FROM cur INTO @t, @col, @nullable;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @dfName = NULL; SET @dfDef = NULL;
            SELECT @dfName = dc.name, @dfDef = dc.definition
            FROM sys.default_constraints dc
            INNER JOIN sys.columns sc ON sc.object_id = dc.parent_object_id AND sc.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID('dbo.' + QUOTENAME(@t)) AND sc.name = @col;

            IF @dfName IS NOT NULL
            BEGIN
                SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' DROP CONSTRAINT ' + QUOTENAME(@dfName) + N';';
                EXEC (@sql);
            END

            SET @type = CASE WHEN @col LIKE '%Percent%' THEN 'DECIMAL(9,4)' ELSE 'DECIMAL(18,4)' END;
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ALTER COLUMN ' + QUOTENAME(@col) + N' ' + @type
                     + CASE WHEN @nullable = 'NO' THEN N' NOT NULL;' ELSE N' NULL;' END;
            EXEC (@sql);

            IF @dfName IS NOT NULL
            BEGIN
                SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' ADD CONSTRAINT ' + QUOTENAME(@dfName) + N' DEFAULT ' + @dfDef + N' FOR ' + QUOTENAME(@col) + N';';
                EXEC (@sql);
            END

            FETCH NEXT FROM cur INTO @t, @col, @nullable;
        END
        CLOSE cur; DEALLOCATE cur;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("EnsureDecimalColumns error: " + ex.Message);
        }
    }

    /// <summary>
    /// Tüm ekranların kullandığı TEK stok formülü. Giriş/çıkış hareket türleri burada tanımlıdır.
    /// Transfer Giriş ve Transfer Çıkış toplamda birbirini götürür.
    /// </summary>
    private static void EnsureStockViewAndIndexes(SqlConnection c)
    {
        try
        {
            using var v = c.CreateCommand();
            v.CommandText = @"
CREATE OR ALTER VIEW dbo.vw_ProductStock AS
SELECT p.Id AS ProductId,
       p.OpeningStock + COALESCE(SUM(CASE
            WHEN sm.MovementType IN (N'Gelen', N'İade Giriş', N'Transfer Giriş') THEN sm.Quantity
            WHEN sm.MovementType IN (N'Satılan', N'Satış', N'Giden', N'Çıkış', N'Fire', N'Fire / Zayi', N'Transfer Çıkış') THEN -sm.Quantity
            ELSE 0 END), 0) AS CurrentStock,
       COALESCE(SUM(CASE WHEN sm.MovementType IN (N'Gelen', N'İade Giriş', N'Transfer Giriş') THEN sm.Quantity ELSE 0 END), 0) AS TotalIn,
       COALESCE(SUM(CASE WHEN sm.MovementType IN (N'Satılan', N'Satış', N'Giden', N'Çıkış', N'Fire', N'Fire / Zayi', N'Transfer Çıkış') THEN sm.Quantity ELSE 0 END), 0) AS TotalOut
FROM dbo.Products p
LEFT JOIN dbo.StockMovements sm ON sm.ProductId = p.Id
GROUP BY p.Id, p.OpeningStock;";
            v.ExecuteNonQuery();

            using (var pack = c.CreateCommand())
            {
                pack.CommandText = "IF COL_LENGTH('Invoices', 'PdfSha256') IS NULL ALTER TABLE Invoices ADD PdfSha256 NVARCHAR(64) NULL; IF COL_LENGTH('Products', 'PackSize') IS NULL ALTER TABLE Products ADD PackSize DECIMAL(18,4) NOT NULL CONSTRAINT DF_Products_PackSize DEFAULT 1;";
                pack.ExecuteNonQuery();
            }

            using var idx = c.CreateCommand();
            idx.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Products_Barcode')
    CREATE NONCLUSTERED INDEX IX_Products_Barcode ON Products(Barcode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Stock_Date')
    CREATE NONCLUSTERED INDEX IX_Stock_Date ON StockMovements(MovementDate) INCLUDE (ProductId, MovementType, Quantity);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Stock_DocNo')
    CREATE NONCLUSTERED INDEX IX_Stock_DocNo ON StockMovements(DocumentNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccMov_DocNo')
    CREATE NONCLUSTERED INDEX IX_AccMov_DocNo ON AccountMovements(DocumentNo);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AccMov_Date')
    CREATE NONCLUSTERED INDEX IX_AccMov_Date ON AccountMovements(MovementDate);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Invoices_Number')
    CREATE NONCLUSTERED INDEX IX_Invoices_Number ON Invoices(InvoiceNumber);";
            idx.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("EnsureStockViewAndIndexes error: " + ex.Message);
        }
    }

    /// <summary>
    /// Eski sürümlerde N'siz 'Alış', 'Kredi Kartı' gibi sabitler Latin1 veritabanına 'Alis', 'Kredi Karti' olarak yazıldı.
    /// Bu kayıtlar bakiye/kasa/stok sorgularında sayılmıyordu; doğru yazıma çevrilir. Her açılışta çalışır, tekrarı zararsızdır.
    /// </summary>
    private static void RepairLatin1MangledTexts(SqlConnection c)
    {
        var fixes = new (string Table, string Column, string Wrong, string Right)[]
        {
            ("AccountMovements", "TransactionType", "Alis", "Alış"),
            ("AccountMovements", "TransactionType", "Satis", "Satış"),
            ("AccountMovements", "Method", "Kredi Karti", "Kredi Kartı"),
            ("AccountMovements", "Method", "Parçali Ödeme", "Parçalı Ödeme"),
            ("AccountMovements", "Method", "Açilis Bakiyesi", "Açılış Bakiyesi"),
            ("AccountMovements", "CashBank", "Banka Hesabi", "Banka Hesabı"),
            ("AccountMovements", "CashBank", "Fatura Cari Kaydi", "Fatura Cari Kaydı"),
            ("StockMovements", "MovementType", "Satilan", "Satılan"),
            ("StockMovements", "MovementType", "Satis", "Satış"),
            ("StockMovements", "MovementType", "Çikis", "Çıkış"),
            ("StockMovements", "MovementType", "Iade Giris", "İade Giriş"),
            ("StockMovements", "MovementType", "Transfer Giris", "Transfer Giriş"),
            ("StockMovements", "MovementType", "Transfer Çikis", "Transfer Çıkış"),
            ("Accounts", "Type", "Müsteri", "Müşteri"),
            ("Users", "Role", "Kullanici", "Kullanıcı"),
            ("Invoices", "InvoiceType", "Alis Faturasi", "Alış Faturası"),
        };

        foreach (var f in fixes)
        {
            try
            {
                using var cmd = c.CreateCommand();
                // Latin1_General_BIN: 'Alis' ile 'Alış' ayrı tutulsun, yalnızca bozuk yazım güncellensin
                cmd.CommandText = $"UPDATE dbo.[{f.Table}] SET [{f.Column}] = @right WHERE [{f.Column}] COLLATE Latin1_General_BIN = @wrong;";
                cmd.Parameters.AddWithValue("@right", f.Right);
                cmd.Parameters.AddWithValue("@wrong", f.Wrong);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RepairLatin1MangledTexts {f.Table}.{f.Column} error: " + ex.Message);
            }
        }
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
    Role NVARCHAR(50) NOT NULL DEFAULT N'Kullanıcı',
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
    InvoiceType NVARCHAR(50) NOT NULL DEFAULT N'Alış Faturası',
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

-- FAST / IBAN & Sistem Ayarları Tablosu
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppSettings')
CREATE TABLE AppSettings (
    [Key] NVARCHAR(100) PRIMARY KEY,
    [Value] NVARCHAR(MAX) NULL
);

-- Hızlı Satış Dokunmatik Ekran Butonları için IsFastSale Kolonu
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'IsFastSale')
    ALTER TABLE Products ADD IsFastSale BIT NOT NULL DEFAULT 0;

-- Gider Yönetimi & Net Kâr Tablosu
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Expenses')
CREATE TABLE Expenses (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ExpenseDate NVARCHAR(50) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    Amount FLOAT NOT NULL,
    PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Nakit',
    Note NVARCHAR(MAX) NULL,
    CreatedAt NVARCHAR(50) NOT NULL
);

-- İndeksler
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Stock_Product')
    CREATE NONCLUSTERED INDEX IX_Stock_Product ON StockMovements(ProductId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountMovement_Account')
    CREATE NONCLUSTERED INDEX IX_AccountMovement_Account ON AccountMovements(AccountId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_Date')
    CREATE NONCLUSTERED INDEX IX_AuditLogs_Date ON AuditLogs(LogDate);
";
            cmd.ExecuteNonQuery();

            EnsureDecimalColumns(c);
            EnsureStockViewAndIndexes(c);
            RepairLatin1MangledTexts(c);
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

    // Varsayılan kullanıcı/şifre oluşturulmaz: ilk açılışta yönetici şifresini kullanıcı belirler (UserService.CreateInitialAdmin).
    private static void SeedDefaultUsers()
    {
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

        return PrefixUnicodeLiterals(normalized);
    }

    /// <summary>
    /// Türkçe karakter (ı, ş, ğ, İ...) içeren 'metin' sabitlerinin başına N ekler.
    /// Veritabanı Latin1 harf düzeninde olduğundan N'siz 'Satış' sorguda 'Satis'e dönüşür ve
    /// parametreyle doğru yazılmış 'Satış' kayıtlarıyla eşleşmez (bakiye 0 görünür).
    /// </summary>
    public static string PrefixUnicodeLiterals(string sql)
    {
        if (string.IsNullOrEmpty(sql) || !sql.Any(ch => ch > 127)) return sql;

        var sb = new StringBuilder(sql.Length + 16);
        int i = 0;
        while (i < sql.Length)
        {
            char ch = sql[i];

            // -- satır yorumu
            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                int end = sql.IndexOf('\n', i);
                if (end < 0) end = sql.Length;
                sb.Append(sql, i, end - i);
                i = end;
                continue;
            }

            // /* blok yorumu */
            if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                int end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? sql.Length : end + 2;
                sb.Append(sql, i, end - i);
                i = end;
                continue;
            }

            // [köşeli parantezli tanımlayıcı]
            if (ch == '[')
            {
                int end = sql.IndexOf(']', i + 1);
                end = end < 0 ? sql.Length : end + 1;
                sb.Append(sql, i, end - i);
                i = end;
                continue;
            }

            if (ch == '\'')
            {
                // Literalin sonunu bul ('' kaçış karakterini atla)
                int j = i + 1;
                while (j < sql.Length)
                {
                    if (sql[j] == '\'')
                    {
                        if (j + 1 < sql.Length && sql[j + 1] == '\'') { j += 2; continue; }
                        break;
                    }
                    j++;
                }
                int end = Math.Min(j + 1, sql.Length);

                bool alreadyN = i > 0 && (sql[i - 1] == 'N' || sql[i - 1] == 'n') &&
                                (i < 2 || !(char.IsLetterOrDigit(sql[i - 2]) || sql[i - 2] == '_'));
                bool hasUnicode = false;
                for (int k = i + 1; k < end - 1; k++)
                {
                    if (sql[k] > 127) { hasUnicode = true; break; }
                }

                if (hasUnicode && !alreadyN) sb.Append('N');
                sb.Append(sql, i, end - i);
                i = end;
                continue;
            }

            sb.Append(ch);
            i++;
        }

        return sb.ToString();
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

    /// <summary>.bak dosyasının bozuk olup olmadığını SQL Server'a RESTORE VERIFYONLY ile denetletir.</summary>
    public static (bool Ok, string Message) VerifyBackupFile(string bakFilePath)
    {
        try
        {
            using var c = Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "RESTORE VERIFYONLY FROM DISK = @path;";
            cmd.CommandTimeout = 300;
            cmd.Parameters.AddWithValue("@path", bakFilePath);
            cmd.ExecuteNonQuery();
            return (true, "Yedek dosyası doğrulandı.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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

    /// <summary>
    /// Veritabanındaki tüm kayıtlı işlem, fatura, stok, cari ve log verilerini tamamen siler,
    /// otomatik artan ID sayaçlarını (IDENTITY) sıfırlar (RESEED 0 -> İlk kayıt ID=1 olur),
    /// Merkez Depo'yu yeniden ilklendirir.
    /// </summary>
    public static void ResetAllDataAndReseed(bool keepLicense = true)
    {
        // Silinme sırası: önce bağlı (alt) tablolar
        string[] tables =
        {
            "InvoicePayments", "InvoiceItems", "Invoices", "StockMovements", "ProductPriceHistory", "ProductVariants",
            "Products", "AccountMovements", "Accounts", "DailyRegisterClosings", "Expenses", "AuditLogs", "Users", "Warehouses"
        };

        using (var c = Open())
        using (var tx = c.BeginTransaction())
        {
            try
            {
                SqlCommand Cmd(string sql)
                {
                    var cmd = c.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    return cmd;
                }

                // Süper kullanıcı (lisans üretici hesabı) korunur: sıfırlamadan sonra Id=1 olarak geri yazılır
                DataTable superUser;
                using (var q = Cmd("SELECT Username, PasswordHash, FullName, Role, Permissions, CreatedAt FROM Users WHERE LOWER(Username) = 'super';"))
                using (var r = q.ExecuteReader())
                {
                    superUser = new DataTable();
                    superUser.Load(r);
                }

                foreach (var t in tables)
                {
                    // TRUNCATE kayıtları siler VE ID sayacını tablonun başlangıç değerine (1) döndürür.
                    // (DELETE + RESEED 0, hiç kayıt almamış tabloda ilk Id'yi 0 yapıyordu.)
                    // Yabancı anahtar yüzünden TRUNCATE edilemeyen tabloda DELETE + RESEED kullanılır.
                    using var cmd = Cmd($@"
IF OBJECT_ID('dbo.{t}') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID('dbo.{t}'))
        TRUNCATE TABLE dbo.[{t}];
    ELSE
    BEGIN
        DELETE FROM dbo.[{t}];
        DBCC CHECKIDENT ('dbo.{t}', RESEED, 0) WITH NO_INFOMSGS;
    END
END");
                    cmd.ExecuteNonQuery();
                }

                foreach (DataRow su in superUser.Rows)
                {
                    using var ins = Cmd(@"
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES (@u, @p, @fn, @r, @perm, 1, @created);");
                    ins.Parameters.AddWithValue("@u", su["Username"]);
                    ins.Parameters.AddWithValue("@p", su["PasswordHash"]);
                    ins.Parameters.AddWithValue("@fn", su["FullName"]);
                    ins.Parameters.AddWithValue("@r", su["Role"]);
                    ins.Parameters.AddWithValue("@perm", su["Permissions"]);
                    ins.Parameters.AddWithValue("@created", su["CreatedAt"]);
                    ins.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { }
                throw;
            }
        }

        // 3. Varsayılan Kullanıcıları ve Depoyu ID 1'den Başlayarak Ekle
        SeedDefaultUsers();
        SeedDefaultWarehouse();

        // 4. Arşivlenmiş Fatura PDF Dosyalarını Temizle (eski konum + asıl PDF arşivi)
        foreach (var archiveDir in new[] { Path.Combine(Folder, "Invoices"), Services.PdfArchiveService.ArchiveRoot })
        {
            try
            {
                if (!Directory.Exists(archiveDir)) continue;
                foreach (var f in Directory.GetFiles(archiveDir, "*.*", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); } catch { }
                }
                foreach (var d in Directory.GetDirectories(archiveDir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
                {
                    try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
                }
            }
            catch { }
        }
    }
}
