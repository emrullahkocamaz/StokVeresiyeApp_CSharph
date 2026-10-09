namespace StokVeresiyeApp.Models;

public class WhatsAppConfig
{
    public string AdminPhoneNumber { get; set; } = string.Empty;
    public bool AutoDailySummary { get; set; } = true;
    public bool AutoCriticalStockAlert { get; set; } = true;
    public string DailySummaryTime { get; set; } = "20:00";
    public string WebhookApiKey { get; set; } = string.Empty;
    public bool IsWebhookEnabled { get; set; } = true;
}
