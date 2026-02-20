using Dapper;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public sealed class TelegramCrawlRepository : BaseRepository
{
    protected override string ConnectionName => "ManagementShortUrl";

    public TelegramCrawlRepository(DbConnectionFactory factory) : base(factory)
    {
    }

    public sealed record CrawlSourceRow(
        Guid SourceId,
        string PeerType,
        long PeerId,
        long? AccessHash,
        string? PeerUsername,
        string? Title,
        bool IsEnabled,
        bool IsHidden);

    public sealed record CrawlSourceUpsertRow(
        string PeerType,
        long PeerId,
        long? AccessHash,
        string? PeerUsername,
        string? Title);

    public async Task<IReadOnlyList<CrawlSourceRow>> ListSources(bool? isEnabled, bool includeHidden, string? q, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        const string sql = @"SELECT SourceId, PeerType, PeerId, AccessHash, PeerUsername, Title, IsEnabled, IsHidden
FROM TelegramCrawlSources
WHERE (@isEnabled IS NULL OR IsEnabled=@isEnabled)
  AND (@includeHidden = 1 OR IsHidden = 0)
  AND (
        @q IS NULL OR @q = '' OR
        (PeerUsername LIKE '%' + @q + '%') OR
        (Title LIKE '%' + @q + '%')
      )
ORDER BY IsHidden ASC, IsEnabled DESC, Title ASC";

        var rows = await conn.QueryAsync<CrawlSourceRow>(new CommandDefinition(
            sql,
            new { isEnabled, includeHidden, q },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<int> SetEnabled(Guid sourceId, bool isEnabled, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        const string sql = @"UPDATE TelegramCrawlSources
SET IsEnabled=@isEnabled,
    UpdatedAt=@updatedAtUtc
WHERE SourceId=@sourceId";

        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { sourceId, isEnabled, updatedAtUtc },
            cancellationToken: cancellationToken));
    }

    public async Task<int> SetHidden(Guid sourceId, bool isHidden, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        const string sql = @"UPDATE TelegramCrawlSources
SET IsHidden=@isHidden,
    UpdatedAt=@updatedAtUtc
WHERE SourceId=@sourceId";

        return await conn.ExecuteAsync(new CommandDefinition(
            sql,
            new { sourceId, isHidden, updatedAtUtc },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CrawlSourceRow>> GetEnabledSources(CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();
        const string sql = @"SELECT SourceId, PeerType, PeerId, AccessHash, PeerUsername, Title, IsEnabled, IsHidden
FROM TelegramCrawlSources
WHERE IsEnabled=1
  AND IsHidden=0";

        var rows = await conn.QueryAsync<CrawlSourceRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task UpsertSources(IReadOnlyList<CrawlSourceUpsertRow> sources, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        if (sources.Count == 0) return;

        using var conn = CreateConnection();

        const string sql = @"
MERGE TelegramCrawlSources AS target
USING (SELECT @PeerType AS PeerType,
              @PeerId AS PeerId,
              @AccessHash AS AccessHash,
              @PeerUsername AS PeerUsername,
              @Title AS Title) AS src
ON (target.PeerType = src.PeerType AND target.PeerId = src.PeerId)
WHEN MATCHED THEN
    UPDATE SET
        AccessHash = src.AccessHash,
        PeerUsername = src.PeerUsername,
        Title = src.Title,
        UpdatedAt = @updatedAtUtc
WHEN NOT MATCHED THEN
    INSERT (SourceId, PeerType, PeerId, AccessHash, PeerUsername, Title, IsEnabled, IsHidden, CreatedAt, UpdatedAt)
    VALUES (NEWID(), src.PeerType, src.PeerId, src.AccessHash, src.PeerUsername, src.Title, 0, 0, @updatedAtUtc, @updatedAtUtc);
";

        foreach (var s in sources)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    s.PeerType,
                    s.PeerId,
                    s.AccessHash,
                    s.PeerUsername,
                    s.Title,
                    updatedAtUtc
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<int> GetLastMessageId(string peerType, long peerId, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();
        const string sql = @"SELECT TOP 1 LastMessageId
FROM TelegramCrawlOffsets
WHERE PeerType=@peerType AND PeerId=@peerId";

        var x = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(sql, new { peerType, peerId }, cancellationToken: cancellationToken));
        return x ?? 0;
    }

    public async Task UpsertLastMessageId(string peerType, long peerId, int lastMessageId, DateTime updatedAtUtc, CancellationToken cancellationToken)
    {
        using var conn = CreateConnection();

        const string sql = @"
IF EXISTS (SELECT 1 FROM TelegramCrawlOffsets WHERE PeerType=@peerType AND PeerId=@peerId)
BEGIN
    UPDATE TelegramCrawlOffsets
    SET LastMessageId=@lastMessageId,
        UpdatedAt=@updatedAtUtc
    WHERE PeerType=@peerType AND PeerId=@peerId;
END
ELSE
BEGIN
    INSERT INTO TelegramCrawlOffsets(OffsetId, PeerType, PeerId, LastMessageId, UpdatedAt)
    VALUES (NEWID(), @peerType, @peerId, @lastMessageId, @updatedAtUtc);
END";

        await conn.ExecuteAsync(new CommandDefinition(sql, new { peerType, peerId, lastMessageId, updatedAtUtc }, cancellationToken: cancellationToken));
    }
}
