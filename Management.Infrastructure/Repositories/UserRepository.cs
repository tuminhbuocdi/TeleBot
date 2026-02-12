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

        string sql = "SELECT * FROM Users WHERE Username=@username AND IsDeleted=0";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { username });
    }

    public async Task<User?> GetByEmail(string email)
    {
        using var conn = CreateConnection();

        string sql = "SELECT * FROM Users WHERE Email=@email AND IsDeleted=0";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { email });
    }

    public async Task<User?> GetByPhone(string phone)
    {
        using var conn = CreateConnection();

        string sql = "SELECT * FROM Users WHERE Phone=@phone AND IsDeleted=0";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { phone });
    }

    public async Task<User?> GetByLogin(string login)
    {
        using var conn = CreateConnection();

        string sql = @"SELECT TOP 1 *
FROM Users
WHERE IsDeleted=0
  AND (Username=@login OR Email=@login OR Phone=@login)";

        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { login });
    }

    public async Task<Guid> Insert(User user)
    {
        using var conn = CreateConnection();

        string sql = @"
INSERT INTO Users
(
    UserId,
    Username,
    Email,
    Phone,
    PasswordHash,
    FullName,
    AvatarUrl,
    IsActive,
    IsLocked,
    IsDeleted,
    UserRole,
    LastLoginAt,
    LastLoginIp,
    EmailVerified,
    PhoneVerified,
    FailedLoginCount,
    LockoutEnd,
    CreatedAt,
    UpdatedAt
)
VALUES
(
    @UserId,
    @Username,
    @Email,
    @Phone,
    @PasswordHash,
    @FullName,
    @AvatarUrl,
    @IsActive,
    @IsLocked,
    @IsDeleted,
    @UserRole,
    @LastLoginAt,
    @LastLoginIp,
    @EmailVerified,
    @PhoneVerified,
    @FailedLoginCount,
    @LockoutEnd,
    @CreatedAt,
    @UpdatedAt
);
SELECT @UserId";

        return await conn.ExecuteScalarAsync<Guid>(sql, user);
    }

    public async Task UpdateLoginSuccess(Guid userId, DateTime lastLoginAtUtc, string? lastLoginIp)
    {
        using var conn = CreateConnection();

        string sql = @"
UPDATE Users
SET LastLoginAt=@lastLoginAtUtc,
    LastLoginIp=@lastLoginIp,
    FailedLoginCount=0,
    IsLocked=0,
    LockoutEnd=NULL,
    UpdatedAt=@lastLoginAtUtc
WHERE UserId=@userId";

        await conn.ExecuteAsync(sql, new { userId, lastLoginAtUtc, lastLoginIp });
    }

    public async Task UpdateLoginFailure(Guid userId, int failedLoginCount, DateTime? lockoutEndUtc, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();

        string sql = @"
UPDATE Users
SET FailedLoginCount=@failedLoginCount,
    IsLocked=CASE WHEN @lockoutEndUtc IS NULL THEN IsLocked ELSE 1 END,
    LockoutEnd=@lockoutEndUtc,
    UpdatedAt=@updatedAtUtc
WHERE UserId=@userId";

        await conn.ExecuteAsync(sql, new { userId, failedLoginCount, lockoutEndUtc, updatedAtUtc });
    }
}
