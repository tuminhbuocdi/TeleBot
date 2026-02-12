using Management.Infrastructure.Db;
using System.Data;

namespace Management.Infrastructure.Repositories;

public class BaseRepository
{
    protected readonly DbConnectionFactory _factory;

    public BaseRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    protected IDbConnection CreateConnection()
        => _factory.Create();
}
