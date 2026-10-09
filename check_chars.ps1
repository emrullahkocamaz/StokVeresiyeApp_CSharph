$line = (Get-Content "StokVeresiyeApp\MainForm.cs" -Encoding UTF8)[2040]
Write-Output "Line: $line"
$chars = $line.ToCharArray()
foreach ($ch in $chars) {
    if ([int]$ch -gt 127) {
        Write-Output "Char: '$ch' Code: $([int]$ch) Hex: $('{0:X4}' -f [int]$ch)"
    }
}
