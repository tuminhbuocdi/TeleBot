namespace Management.Worker;

public sealed class TelegramMtProtoOptions
{
    public int ApiId { get; init; }
    public string ApiHash { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string TwoFactorPassword { get; init; } = string.Empty;
    public string SessionPath { get; init; } = "data/wtelegram.session";
}
