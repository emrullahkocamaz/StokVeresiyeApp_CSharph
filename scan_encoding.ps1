$files = Get-ChildItem -Path "StokVeresiyeApp" -Recurse -Filter "*.cs"
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Contains([char]0xFFFD)) {
        Write-Output "CORRUPT: $($f.FullName)"
    }
}
