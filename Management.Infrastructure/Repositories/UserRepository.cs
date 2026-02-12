using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using Management.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Management.Infrastructure.Repositories;

public class UserRepository : BaseRepository
{
    public UserRepository(DbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<User?> GetByUsername(string username)
    {
        using var conn = CreateConnection();

        string sql = "SELECT * FROM Users WHERE Username=@username";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { username });
    }

    public async Task<int> Insert(User user)
    {
        using var conn = CreateConnection();

        string sql = @"
INSERT INTO Users(Username,PasswordHash,CreatedAt)
VALUES(@Username,@PasswordHash,@CreatedAt);
SELECT CAST(SCOPE_IDENTITY() as int)";

        return await conn.ExecuteScalarAsync<int>(sql, user);
    }
}
