$lines = [System.IO.File]::ReadAllLines("StokVeresiyeApp\MainForm.cs")
for ($i = 2035; $i -lt 2050; $i++) {
    Write-Output "[$($i+1)] $($lines[$i])"
}
