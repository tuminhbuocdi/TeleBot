namespace Management.Worker;

public sealed class TelegramCrawlJobOptions
{
    public int RunIntervalMinutes { get; init; } = 5;
    public int BatchSize { get; init; } = 200;
}
