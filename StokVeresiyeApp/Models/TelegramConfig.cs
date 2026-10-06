namespace StokVeresiyeApp.Models;

public class TelegramConfig
{
    public bool IsEnabled { get; set; } = false;
    public string BotToken { get; set; } = string.Empty;
    public long AuthorizedChatId { get; set; } = 0;
    public string AuthorizedUserName { get; set; } = string.Empty;
    public bool NotifyOnCriticalStock { get; set; } = true;
    public bool NotifyDailySummary { get; set; } = true;
    public DateTime? LastActiveTime { get; set; }
}
