namespace Management.Worker;

public sealed class TelegramVideoDemoOptions
{
    public bool Enabled { get; init; } = true;
    public int MinFullSeconds { get; init; } = 40;
    public int DemoSeconds { get; init; } = 18;
    public string FfmpegPath { get; init; } = "ffmpeg";
}
