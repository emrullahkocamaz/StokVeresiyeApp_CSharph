namespace StokVeresiyeApp.Models;

public class LicenseInfo
{
    public long Id { get; set; }
    public string MachineCode { get; set; } = string.Empty;
    public string LicenseKey { get; set; } = string.Empty;
    public DateTime ActivatedDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public string LicensedTo { get; set; } = "Firma";
    public bool IsValid { get; set; }
    public int DaysRemaining => (int)Math.Max(0, (ExpireDate.Date - DateTime.Today).TotalDays);
    public string StatusMessage { get; set; } = string.Empty;
}
