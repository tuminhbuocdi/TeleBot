using Dapper;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public sealed class TelegramPostRepository : BaseRepository
{
    protected override string ConnectionName => "ManagementShortUrl";

    public TelegramPostRepository(DbConnectionFactory factory) : base(factory)
    {
    }

    public sealed record TelegramPostInsertResult(Guid PostId);

    public async Task<bool> ExistsByTelegramMessage(long telegramChatId, long telegramMessageId)
    {
        using var conn = CreateConnection();
        const string sql = @"SELECT TOP 1 1
FROM TelegramPosts
WHERE TelegramChatId=@telegramChatId
  AND TelegramMessageId=@telegramMessageId";

        var x = await conn.ExecuteScalarAsync<int?>(sql, new { telegramChatId, telegramMessageId });
        return x.HasValue;
    }

    public async Task<string?> GetFullVideoTelegramFileId(Guid postId, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();
        const string sql = @"
SELECT TOP 1 TelegramFileId
FROM TelegramPostMedias
WHERE PostId=@postId
  AND IsActive=1
  AND MediaType='video_full'
  AND TelegramFileId IS NOT NULL
ORDER BY SortOrder ASC, CreatedAt ASC";

        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new { postId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> InsertPostWithMedias(
        Guid? postId,
        long telegramChatId,
        long telegramMessageId,
        string? title,
        string? originalContent,
        string? content,
        IReadOnlyList<TelegramPostMediaRow> medias,
        CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        var finalPostId = postId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        const string insertPostSql = @"
INSERT INTO TelegramPosts
(
    PostId,
    TelegramMessageId,
    TelegramChatId,
    Title,
    OriginalContent,
    Content,
    ViewCount,
    LikeCount,
    IsActive,
    CreatedAt,
    UpdatedAt
)
VALUES
(
    @postId,
    @telegramMessageId,
    @telegramChatId,
    @title,
    @originalContent,
    @content,
    0,
    0,
    1,
    @now,
    @now
);";

        await conn.ExecuteAsync(new CommandDefinition(
            insertPostSql,
            new { postId = finalPostId, telegramMessageId, telegramChatId, title, originalContent, content, now },
            cancellationToken: cancellationToken));

        const string insertMediaSql = @"
INSERT INTO TelegramPostMedias
(
    MediaId,
    PostId,
    MediaType,
    FileUrl,
    TelegramFileId,
    Duration,
    FileSize,
    ThumbnailUrl,
    SortOrder,
    IsActive,
    CreatedAt
)
VALUES
(
    @MediaId,
    @PostId,
    @MediaType,
    @FileUrl,
    @TelegramFileId,
    @Duration,
    @FileSize,
    @ThumbnailUrl,
    @SortOrder,
    1,
    @CreatedAt
);";

        if (medias.Count > 0)
        {
            foreach (var m in medias)
            {
                var row = m with
                {
                    MediaId = m.MediaId == Guid.Empty ? Guid.NewGuid() : m.MediaId,
                    PostId = finalPostId,
                    CreatedAt = m.CreatedAt == default ? now : m.CreatedAt
                };

                await conn.ExecuteAsync(new CommandDefinition(
                    insertMediaSql,
                    row,
                    cancellationToken: cancellationToken));
            }
        }

        return finalPostId;
    }
}

public sealed record TelegramPostMediaRow(
    Guid MediaId,
    Guid PostId,
    string MediaType,
    string FileUrl,
    string? TelegramFileId,
    int? Duration,
    long? FileSize,
    string? ThumbnailUrl,
    int SortOrder,
    DateTime CreatedAt);
