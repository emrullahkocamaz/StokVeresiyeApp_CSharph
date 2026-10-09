# Bilensis Veritabani Sifirlama Scripti
$server = "(localdb)\MSSQLLocalDB"
$database = "BilgeStokDB"
$connectionString = "Server=$server;Database=$database;Integrated Security=True;TrustServerCertificate=True;"

Write-Host "LocalDB baslatiliyor..." -ForegroundColor Cyan
& sqllocaldb start MSSQLLocalDB | Out-Null

$sql = @"
-- 1. Tum Hareket ve Islem Tablolarini Sil
DELETE FROM InvoicePayments;
DELETE FROM InvoiceItems;
DELETE FROM Invoices;
DELETE FROM StockMovements;
DELETE FROM ProductPriceHistory;
DELETE FROM ProductVariants;
DELETE FROM Products;
DELETE FROM AccountMovements;
DELETE FROM Accounts;
DELETE FROM DailyRegisterClosings;
DELETE FROM Expenses;
DELETE FROM AuditLogs;
DELETE FROM Users;
DELETE FROM Warehouses;

-- 2. Otomatik Artan ID (IDENTITY) Sayaclarini Sifirla (Sonraki kayit ID=1 baslar)
IF OBJECT_ID('InvoicePayments') IS NOT NULL DBCC CHECKIDENT ('InvoicePayments', RESEED, 0);
IF OBJECT_ID('InvoiceItems') IS NOT NULL DBCC CHECKIDENT ('InvoiceItems', RESEED, 0);
IF OBJECT_ID('Invoices') IS NOT NULL DBCC CHECKIDENT ('Invoices', RESEED, 0);
IF OBJECT_ID('StockMovements') IS NOT NULL DBCC CHECKIDENT ('StockMovements', RESEED, 0);
IF OBJECT_ID('ProductPriceHistory') IS NOT NULL DBCC CHECKIDENT ('ProductPriceHistory', RESEED, 0);
IF OBJECT_ID('ProductVariants') IS NOT NULL DBCC CHECKIDENT ('ProductVariants', RESEED, 0);
IF OBJECT_ID('Products') IS NOT NULL DBCC CHECKIDENT ('Products', RESEED, 0);
IF OBJECT_ID('AccountMovements') IS NOT NULL DBCC CHECKIDENT ('AccountMovements', RESEED, 0);
IF OBJECT_ID('Accounts') IS NOT NULL DBCC CHECKIDENT ('Accounts', RESEED, 0);
IF OBJECT_ID('DailyRegisterClosings') IS NOT NULL DBCC CHECKIDENT ('DailyRegisterClosings', RESEED, 0);
IF OBJECT_ID('Expenses') IS NOT NULL DBCC CHECKIDENT ('Expenses', RESEED, 0);
IF OBJECT_ID('AuditLogs') IS NOT NULL DBCC CHECKIDENT ('AuditLogs', RESEED, 0);
IF OBJECT_ID('Users') IS NOT NULL DBCC CHECKIDENT ('Users', RESEED, 0);
IF OBJECT_ID('Warehouses') IS NOT NULL DBCC CHECKIDENT ('Warehouses', RESEED, 0);

-- 3. Varsayilan Depoyu ID=1 Olarak Ekle
INSERT INTO Warehouses (Name, Code, Type, ResponsiblePerson, Phone, IsDefault, IsActive)
VALUES ('Merkez Depo', 'DEP-01', 'Depo', 'Depo Sorumlusu', '', 1, 1);

-- 4. Varsayilan Kullanicilari Ekle
-- admin / 123456 (Hash: SHA256('123456_BILGE_SALT_2026'))
-- super / 367244 (Hash: SHA256('367244_BILGE_SALT_2026'))
INSERT INTO Users (Username, PasswordHash, FullName, Role, Permissions, IsActive, CreatedAt)
VALUES 
('admin', '7D93BBE1BC95893C4C9BBA50D57CD6B8196F4B6886A2E44ED2025EB3EC5FD023', 'Sistem Yöneticisi', 'Admin', 'ALL', 1, CONVERT(VARCHAR(19), GETDATE(), 120)),
('super', '063B2BF16C1D5408D1FDC22EE0C87333B1BD84976451B0813959FE2FE870EB84', 'Süper Kullanıcı', 'SuperAdmin', 'ALL', 1, CONVERT(VARCHAR(19), GETDATE(), 120));
"@

Write-Host "Veritabani tablolari temizleniyor ve ID sayaclari sifirlaniyor..." -ForegroundColor Yellow

try {
    $conn = New-Object Microsoft.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.ExecuteNonQuery() | Out-Null
    $conn.Close()
    Write-Host "BASARILI: Veritabani tamamen sifirlandi! Tum ID sayaclari 1'e cekildi." -ForegroundColor Green
}
catch {
    try {
        # Fallback to System.Data.SqlClient if Microsoft.Data.SqlClient not loaded
        $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $cmd.ExecuteNonQuery() | Out-Null
        $conn.Close()
        Write-Host "BASARILI: Veritabani tamamen sifirlandi! Tum ID sayaclari 1'e cekildi." -ForegroundColor Green
    }
    catch {
        Write-Host "HATA: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Arşiv klasörünü temizle
$archivePath = "$env:LOCALAPPDATA\StokVeresiyeApp\Invoices"
if (Test-Path $archivePath) {
    Remove-Item "$archivePath\*" -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Fatura arsiv dosyalari temizlendi: $archivePath" -ForegroundColor Green
}
