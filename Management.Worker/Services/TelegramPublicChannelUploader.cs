using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Net.Sockets;

namespace Management.Worker.Services;

public sealed class TelegramPublicChannelUploader
{
    private readonly TelegramUploaderOptions _opts;
    private readonly ILogger<TelegramPublicChannelUploader> _logger;
    private readonly TelegramBotClient? _bot;

    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(10);
    private const int UploadRetries = 1;

    public TelegramPublicChannelUploader(
        IOptions<TelegramUploaderOptions> opts,
        ILogger<TelegramPublicChannelUploader> logger)
    {
        _opts = opts.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_opts.BotToken))
        {
            var http = new HttpClient
            {
                Timeout = UploadTimeout
            };
            _bot = new TelegramBotClient(_opts.BotToken, httpClient: http);
        }
    }

    private async Task<T> WithUploadRetry<T>(Func<Task<T>> action, string opName, string localPath, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= UploadRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                long? size = null;
                try { size = new FileInfo(localPath).Length; } catch { /* ignore */ }

                _logger.LogInformation("{Op} starting. attempt={Attempt}/{MaxAttempt} file={File} size={Size}", opName, attempt + 1, UploadRetries + 1, Path.GetFileName(localPath), size);
                var result = await action();
                _logger.LogInformation("{Op} succeeded. attempt={Attempt}/{MaxAttempt} file={File}", opName, attempt + 1, UploadRetries + 1, Path.GetFileName(localPath));
                return result;
            }
            catch (Exception ex) when (
                attempt < UploadRetries
                && (ex is TaskCanceledException
                    || ex is HttpRequestException
                    || ex is RequestException
                    || ex is IOException
                    || ex is SocketException))
            {
                var delay = TimeSpan.FromSeconds(2 * (attempt + 1));
                _logger.LogWarning(ex, "{Op} timed out/network error. retrying in {Delay}s (attempt={Attempt}/{MaxAttempt})", opName, delay.TotalSeconds, attempt + 1, UploadRetries + 1);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    public bool IsConfigured => _bot is not null && _opts.PublicChannelId != 0;

    public async Task<string?> UploadPhotoAsync(
        long? chatId,
        string localPath,
        string fileName,
        string? caption,
        string? buttonText,
        string? buttonUrl,
        CancellationToken cancellationToken)
    {
        if (_bot is null) throw new InvalidOperationException("TelegramUploader is not configured (missing BotToken)");

        InlineKeyboardMarkup? markup = null;
        if (!string.IsNullOrWhiteSpace(buttonUrl))
        {
            markup = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    string.IsNullOrWhiteSpace(buttonText) ? "VIEW FULL HERE" : buttonText,
                    buttonUrl));
        }

        var sent = await WithUploadRetry(
            async () =>
            {
                await using var stream = System.IO.File.OpenRead(localPath);
                return await _bot.SendPhoto(
                    chatId: chatId ?? _opts.PublicChannelId,
                    photo: InputFile.FromStream(stream, fileName),
                    caption: string.IsNullOrWhiteSpace(caption) ? null : caption,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken);
            },
            opName: "SendPhoto",
            localPath: localPath,
            cancellationToken: cancellationToken);

        return sent.Photo?.OrderByDescending(p => p.FileSize ?? 0).FirstOrDefault()?.FileId;
    }

    public async Task<string?> UploadVideoAsync(
        long? chatId,
        string localPath,
        string fileName,
        string? caption,
        int? duration,
        string? buttonText,
        string? buttonUrl,
        CancellationToken cancellationToken)
    {
        if (_bot is null) throw new InvalidOperationException("TelegramUploader is not configured (missing BotToken)");

        InlineKeyboardMarkup? markup = null;
        if (!string.IsNullOrWhiteSpace(buttonUrl))
        {
            markup = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithUrl(
                    string.IsNullOrWhiteSpace(buttonText) ? "VIEW FULL HERE" : buttonText,
                    buttonUrl));
        }

        var sent = await WithUploadRetry(
            async () =>
            {
                await using var stream = System.IO.File.OpenRead(localPath);
                return await _bot.SendVideo(
                    chatId: chatId ?? _opts.PublicChannelId,
                    video: InputFile.FromStream(stream, fileName),
                    caption: string.IsNullOrWhiteSpace(caption) ? null : caption,
                    duration: duration,
                    supportsStreaming: true,
                    replyMarkup: markup,
                    cancellationToken: cancellationToken);
            },
            opName: "SendVideo",
            localPath: localPath,
            cancellationToken: cancellationToken);

        return sent.Video?.FileId;
    }

    public void EnsureTempFolder()
    {
        try
        {
            Directory.CreateDirectory(_opts.TempFolder);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create TempFolder {TempFolder}", _opts.TempFolder);
        }
    }

    public string GetTempFilePath(string fileNameHint)
        => Path.Combine(_opts.TempFolder, $"{Guid.NewGuid()}_{fileNameHint}");
}
