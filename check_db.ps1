$cs = "Server=(localdb)\MSSQLLocalDB;Database=BilgeStokDB;Integrated Security=True;TrustServerCertificate=True;"
$c = New-Object System.Data.SqlClient.SqlConnection($cs)
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = @"
SELECT 
  (SELECT COUNT(*) FROM Invoices) AS FaturaSayisi,
  (SELECT COUNT(*) FROM InvoiceItems) AS FaturaKalemSayisi,
  (SELECT COUNT(*) FROM StockMovements) AS StokHareketSayisi,
  (SELECT COUNT(*) FROM Products) AS UrunSayisi,
  (SELECT COUNT(*) FROM Accounts) AS CariSayisi,
  (SELECT COUNT(*) FROM DailyRegisterClosings) AS ZSayisi,
  (SELECT COUNT(*) FROM Users) AS KullaniciSayisi,
  (SELECT COUNT(*) FROM Warehouses) AS DepoSayisi
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
  Write-Host "Fatura Sayisi:" $reader["FaturaSayisi"]
  Write-Host "Fatura Kalem Sayisi:" $reader["FaturaKalemSayisi"]
  Write-Host "Stok Hareket Sayisi:" $reader["StokHareketSayisi"]
  Write-Host "Urun Sayisi:" $reader["UrunSayisi"]
  Write-Host "Cari Sayisi:" $reader["CariSayisi"]
  Write-Host "Z Raporu Sayisi:" $reader["ZSayisi"]
  Write-Host "Kullanici Sayisi:" $reader["KullaniciSayisi"]
  Write-Host "Depo Sayisi:" $reader["DepoSayisi"]
}
$reader.Close()

$cmd.CommandText = "SELECT Id, Username, FullName, Role FROM Users;"
$r2 = $cmd.ExecuteReader()
Write-Host "--- KULLANICILAR ---"
while ($r2.Read()) {
  Write-Host "ID:" $r2["Id"] "Username:" $r2["Username"] "Role:" $r2["Role"]
}
$r2.Close()

$cmd.CommandText = "SELECT Id, Name, Code, IsDefault FROM Warehouses;"
$r3 = $cmd.ExecuteReader()
Write-Host "--- DEPOLAR ---"
while ($r3.Read()) {
  Write-Host "ID:" $r3["Id"] "Name:" $r3["Name"] "Default:" $r3["IsDefault"]
}
$r3.Close()
$c.Close()
