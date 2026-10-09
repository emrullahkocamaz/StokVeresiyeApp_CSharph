using System;
using StokVeresiyeApp.Services;

var res = InvoiceParserService.ParseInvoiceFile(@"C:\Users\emrullah.kocamaz.ASEKER\Downloads\62755179091404.Pdf");
Console.WriteLine($"Success: {res.Success}");
Console.WriteLine($"FileType: {res.FileType}");
Console.WriteLine($"InvNo: {res.InvoiceNumber}");
Console.WriteLine($"SubTotal: {res.SubTotal}");
Console.WriteLine($"VatTotal: {res.VatTotal}");
Console.WriteLine($"GrandTotal: {res.GrandTotal}");
Console.WriteLine($"ItemCount: {res.Items.Count}");
double sumTotal = 0;
foreach (var it in res.Items)
{
    Console.WriteLine($"Line {it.LineNo}: {it.ItemName} | Qty: {it.Quantity} | UnitPrice: {it.UnitPrice} | Disc%: {it.DiscountPercent} | LineTotal: {it.LineTotal}");
    sumTotal += it.LineTotal;
}
Console.WriteLine($"Sum of LineTotals: {sumTotal}");
