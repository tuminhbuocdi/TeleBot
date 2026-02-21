using Management.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace Management.WorkerListenMessage.Services;

public interface ITelegramPostResendService
{
    Task ResendOldestPostAsync(CancellationToken cancellationToken);
}

public sealed class TelegramPostResendService : ITelegramPostResendService
{
    private readonly ILogger<TelegramPostResendService> _logger;
    private readonly TelegramUploaderOptions _uploaderOpts;
    private readonly TelegramPostAdminRepository _postRepo;
    private readonly TelegramPostRepository _postMediaRepo;
    private TelegramBotClient? _bot;

    // Track current position for cycling through posts
    private static int _currentPostIndex = 0;

    private string? GetBotDeepLink(Guid postId)
    {
        var username = _uploaderOpts.BotUsername;
        if (string.IsNullOrWhiteSpace(username)) return null;

        var start = $"post_{postId:N}";
        return $"https://t.me/{username}?start={start}";
    }

    public TelegramPostResendService(
        ILogger<TelegramPostResendService> logger,
        IOptions<TelegramUploaderOptions> uploaderOpts,
        TelegramPostAdminRepository postRepo,
        TelegramPostRepository postMediaRepo)
    {
        _logger = logger;
        _uploaderOpts = uploaderOpts.Value;
        _postRepo = postRepo;
        _postMediaRepo = postMediaRepo;

        if (!string.IsNullOrWhiteSpace(_uploaderOpts.BotToken))
        {
            _bot = new TelegramBotClient(_uploaderOpts.BotToken);
        }
    }

    public async Task ResendOldestPostAsync(CancellationToken cancellationToken)
    {
        if (_bot is null)
        {
            _logger.LogWarning("TelegramPostResendService is not configured (missing TelegramUploader:BotToken)");
            return;
        }

        try
        {
            // Get all active posts ordered by CreatedAt (oldest first)
            var posts = await _postRepo.List(page: 1, pageSize: 1000, isActive: true, q: null, cancellationToken);
            
            if (!posts.Any())
            {
                _logger.LogInformation("No active posts found to resend");
                return;
            }

            // Cycle through posts using modulo
            var postToSend = posts[_currentPostIndex % posts.Count];
            
            _logger.LogInformation("Resending post {PostIndex}/{TotalPosts}: {PostId} (Created: {CreatedAt})", 
                _currentPostIndex + 1, posts.Count, postToSend.PostId, postToSend.CreatedAt);

            // Get the preferred video file ID (demo first, full as fallback)
            var videoFileId = await _postMediaRepo.GetPreferredVideoTelegramFileId(postToSend.PostId, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(videoFileId))
            {
                _logger.LogWarning("Post {PostId} has no video media to send", postToSend.PostId);
                await MoveToNextIndex();
                return;
            }

            // Check which video type we're sending
            var demoFileId = await _postMediaRepo.GetDemoVideoTelegramFileId(postToSend.PostId, cancellationToken);
            var videoType = (!string.IsNullOrWhiteSpace(demoFileId) && demoFileId == videoFileId) ? "DEMO" : "FULL";
            _logger.LogInformation("Sending {VideoType} video for post {PostId}", videoType, postToSend.PostId);

            // Create inline button for "VIEW FULL HERE"
            var buttonUrl = GetBotDeepLink(postToSend.PostId);
            InlineKeyboardMarkup? markup = null;
            
            if (!string.IsNullOrWhiteSpace(buttonUrl))
            {
                markup = new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithUrl("VIEW FULL HERE", buttonUrl));
            }
            else
            {
                _logger.LogWarning("Cannot create inline button for post {PostId}: BotUsername is not configured", postToSend.PostId);
            }

            // Send to the public channel with inline button
            await _bot.SendVideo(
                chatId: _uploaderOpts.PublicChannelId,
                video: InputFile.FromFileId(videoFileId),
                caption: null,
                supportsStreaming: true,
                replyMarkup: markup,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully resent post {PostId} to channel {ChannelId}", 
                postToSend.PostId, _uploaderOpts.PublicChannelId);

            // Move to next post for next cycle
            await MoveToNextIndex();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend oldest post");
        }
    }

    private async Task MoveToNextIndex()
    {
        _currentPostIndex++;
        
        // Get total count to check if we need to reset
        var totalPosts = await _postRepo.List(page: 1, pageSize: 1000, isActive: true, q: null, CancellationToken.None);
        if (_currentPostIndex >= totalPosts.Count && totalPosts.Any())
        {
            _currentPostIndex = 0;
            _logger.LogInformation("Completed full cycle, resetting to oldest post");
        }
    }
}
