namespace Management.Worker;

public sealed class TelegramUploaderOptions
{
    public string BotToken { get; init; } = string.Empty;
    public long PublicChannelId { get; init; }
    public long StorageChannelId { get; init; }
    public string BotUsername { get; init; } = string.Empty;
    public string TempFolder { get; init; } = "data/telegram-temp";
}
