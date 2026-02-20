using Management.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Management.WorkerListenMessage.Services;

public sealed class TelegramStartBotWorker : BackgroundService
{
    private readonly ILogger<TelegramStartBotWorker> _logger;
    private readonly TelegramUploaderOptions _uploaderOpts;
    private readonly TelegramPostRepository _postRepo;
    private TelegramBotClient? _bot;

    public TelegramStartBotWorker(
        ILogger<TelegramStartBotWorker> logger,
        IOptions<TelegramUploaderOptions> uploaderOpts,
        TelegramPostRepository postRepo)
    {
        _logger = logger;
        _uploaderOpts = uploaderOpts.Value;
        _postRepo = postRepo;

        if (!string.IsNullOrWhiteSpace(_uploaderOpts.BotToken))
        {
            _bot = new TelegramBotClient(_uploaderOpts.BotToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_bot is null)
        {
            _logger.LogWarning("TelegramStartBotWorker is not configured (missing TelegramUploader:BotToken)");
            return;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        var me = await _bot.GetMe(stoppingToken);
        _logger.LogInformation("TelegramStartBotWorker started as @{Username}", me.Username);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message) return;
        var msg = update.Message;
        if (msg is null) return;
        if (msg.Type != MessageType.Text) return;

        var text = msg.Text ?? string.Empty;
        if (!text.StartsWith("/start", StringComparison.OrdinalIgnoreCase)) return;

        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            await bot.SendMessage(msg.Chat.Id, "Send a valid start parameter.", cancellationToken: cancellationToken);
            return;
        }

        var payload = parts[1];
        if (!payload.StartsWith("post_", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessage(msg.Chat.Id, "Invalid payload.", cancellationToken: cancellationToken);
            return;
        }

        var idPart = payload["post_".Length..];
        if (!Guid.TryParseExact(idPart, "N", out var postId))
        {
            await bot.SendMessage(msg.Chat.Id, "Invalid post id.", cancellationToken: cancellationToken);
            return;
        }

        var fileId = await _postRepo.GetFullVideoTelegramFileId(postId, cancellationToken);
        if (string.IsNullOrWhiteSpace(fileId))
        {
            await bot.SendMessage(msg.Chat.Id, "Video not found.", cancellationToken: cancellationToken);
            return;
        }

        await bot.SendVideo(
            chatId: msg.Chat.Id,
            video: Telegram.Bot.Types.InputFile.FromFileId(fileId),
            supportsStreaming: true,
            cancellationToken: cancellationToken);
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        var err = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error [{apiEx.ErrorCode}]: {apiEx.Message}",
            _ => exception.ToString()
        };

        _logger.LogWarning("TelegramStartBotWorker polling error: {Error}", err);
        return Task.CompletedTask;
    }
}
