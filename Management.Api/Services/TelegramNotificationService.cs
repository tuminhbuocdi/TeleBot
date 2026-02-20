using System.Net.Http.Json;

namespace Management.Api.Services;

public sealed class TelegramNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public TelegramNotificationService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        var botToken = _configuration["Telegram:BotToken"];
        var chatId = _configuration["Telegram:ChatId"];

        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException("Missing configuration: Telegram:BotToken");
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            throw new InvalidOperationException("Missing configuration: Telegram:ChatId");
        }

        var client = _httpClientFactory.CreateClient();

        var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

        var payload = new
        {
            chat_id = chatId,
            text = message,
            disable_web_page_preview = true
        };

        using var resp = await client.PostAsJsonAsync(url, payload, cancellationToken);
        resp.EnsureSuccessStatusCode();
    }
}
