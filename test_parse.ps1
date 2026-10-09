Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.Core.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.DocumentLayoutAnalysis.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.Fonts.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.Package.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.Tokenizers.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.Tokens.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\UglyToad.PdfPig.dll'
Add-Type -Path 'C:\Users\emrullah.kocamaz.ASEKER\Downloads\StokVeresiyeApp_CSharph\StokVeresiyeApp\bin\Debug\net8.0-windows\StokVeresiyeApp.dll'

$res = [StokVeresiyeApp.Services.InvoiceParserService]::ParseInvoiceFile('C:\Users\emrullah.kocamaz.ASEKER\Downloads\62755179091404.Pdf')
Write-Host "Success: $($res.Success)"
Write-Host "FileType: $($res.FileType)"
Write-Host "InvNo: $($res.InvoiceNumber)"
Write-Host "SubTotal: $($res.SubTotal)"
Write-Host "GrandTotal: $($res.GrandTotal)"
Write-Host "ItemCount: $($res.Items.Count)"
$sum = 0
foreach ($it in $res.Items) {
    Write-Host "Line $($it.LineNo): $($it.ItemName) | Qty:$($it.Quantity) | UnitPrice:$($it.UnitPrice) | Disc%:$($it.DiscountPercent) | LineTotal:$($it.LineTotal)"
    $sum += $it.LineTotal
}
Write-Host "SUM: $sum"
