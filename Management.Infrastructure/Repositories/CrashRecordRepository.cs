using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public class CrashRecordRepository : BaseRepository
{
    protected override string ConnectionName => "NotifyCrash";

    public CrashRecordRepository(DbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<CrashRecord>> GetAll()
    {
        using var conn = CreateConnection();
        const string sql = "SELECT GameId, Rate FROM CrashRecords";
        return await conn.QueryAsync<CrashRecord>(sql);
    }

    public async Task<IEnumerable<CrashRecord>> GetTop(long take)
    {
        using var conn = CreateConnection();
        const string sql = @"SELECT TOP (@take) GameId, Rate
FROM CrashRecords
ORDER BY GameId DESC";
        return await conn.QueryAsync<CrashRecord>(sql, new { take });
    }
}
