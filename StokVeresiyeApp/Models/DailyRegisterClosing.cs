namespace StokVeresiyeApp.Models;

public class DailyRegisterClosing
{
    public long Id { get; set; }
    public string ClosingDate { get; set; } = string.Empty;
    public string ClosedBy { get; set; } = string.Empty;
    public double OpeningCash { get; set; } = 0;
    public double CashSales { get; set; } = 0;
    public double CardSales { get; set; } = 0;
    public double TransferSales { get; set; } = 0;
    public double CashExpenses { get; set; } = 0;
    public double ExpectedCash { get; set; } = 0;
    public double CountedCash { get; set; } = 0;
    public double DifferenceCash { get; set; } = 0;
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
