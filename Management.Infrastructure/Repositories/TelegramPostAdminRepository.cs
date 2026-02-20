using Dapper;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public sealed class TelegramPostAdminRepository : BaseRepository
{
    protected override string ConnectionName => "ManagementShortUrl";

    public TelegramPostAdminRepository(DbConnectionFactory factory) : base(factory)
    {
    }

    public sealed record TelegramPostListRow(
        Guid PostId,
        string? Title,
        string? Content,
        int ViewCount,
        int LikeCount,
        bool IsActive,
        DateTime? CreatedAt,
        DateTime? UpdatedAt,
        int MediaCount,
        string? FirstMediaType,
        string? FirstTelegramFileId);

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
        bool IsActive,
        DateTime? CreatedAt);

    public async Task<IReadOnlyList<TelegramPostListRow>> List(int page, int pageSize, bool? isActive, string? q, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var offset = (page - 1) * pageSize;

        const string sql = @"
WITH pm AS (
    SELECT
        m.PostId,
        COUNT(1) AS MediaCount,
        MIN(m.SortOrder) AS MinSort
    FROM TelegramPostMedias m
    WHERE m.IsActive=1
    GROUP BY m.PostId
), fm AS (
    SELECT m.PostId, m.MediaType AS FirstMediaType, m.TelegramFileId AS FirstTelegramFileId
    FROM TelegramPostMedias m
    INNER JOIN pm ON pm.PostId = m.PostId AND pm.MinSort = m.SortOrder
)
SELECT
    p.PostId,
    p.Title,
    p.Content,
    ISNULL(p.ViewCount, 0) AS ViewCount,
    ISNULL(p.LikeCount, 0) AS LikeCount,
    ISNULL(p.IsActive, 1) AS IsActive,
    p.CreatedAt,
    p.UpdatedAt,
    ISNULL(pm.MediaCount, 0) AS MediaCount,
    fm.FirstMediaType,
    fm.FirstTelegramFileId
FROM TelegramPosts p
LEFT JOIN pm ON pm.PostId = p.PostId
LEFT JOIN fm ON fm.PostId = p.PostId
WHERE (@isActive IS NULL OR p.IsActive = @isActive)
  AND (
        @q IS NULL OR @q = '' OR
        (p.Title LIKE '%' + @q + '%') OR
        (p.Content LIKE '%' + @q + '%')
      )
ORDER BY p.CreatedAt DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        var rows = await conn.QueryAsync<TelegramPostListRow>(new CommandDefinition(
            sql,
            new { offset, pageSize, isActive, q },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<IReadOnlyList<TelegramPostMediaRow>> GetMedias(Guid postId, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();
        const string sql = @"
SELECT
    MediaId,
    PostId,
    MediaType,
    FileUrl,
    TelegramFileId,
    Duration,
    FileSize,
    ThumbnailUrl,
    SortOrder,
    ISNULL(IsActive, 1) AS IsActive,
    CreatedAt
FROM TelegramPostMedias
WHERE PostId=@postId
ORDER BY SortOrder ASC, CreatedAt ASC";

        var rows = await conn.QueryAsync<TelegramPostMediaRow>(new CommandDefinition(sql, new { postId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<int> SetActive(Guid postId, bool isActive, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();
        const string sql = @"
UPDATE TelegramPosts
SET IsActive=@isActive,
    UpdatedAt=@updatedAtUtc
WHERE PostId=@postId";

        return await conn.ExecuteAsync(new CommandDefinition(sql, new { postId, isActive, updatedAtUtc }, cancellationToken: cancellationToken));
    }
}
