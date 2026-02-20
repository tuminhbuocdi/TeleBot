using System.Data;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Db;

public class UnitOfWork : IDisposable
{
    private readonly IDbConnection _conn;
    private readonly IDbTransaction _tran;

    public IDbConnection Connection => _conn;
    public IDbTransaction Transaction => _tran;

    public UnitOfWork(DbConnectionFactory factory, string connectionName = "Default")
    {
        _conn = factory.Create(connectionName);
        _conn.Open();
        _tran = _conn.BeginTransaction();
    }

    public void Commit() => _tran.Commit();
    public void Rollback() => _tran.Rollback();

    public void Dispose()
    {
        _tran?.Dispose();
        _conn?.Dispose();
    }
}
