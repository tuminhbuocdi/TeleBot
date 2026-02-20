using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Management.Infrastructure.Db;

public class DbConnectionFactory
{
    private readonly IConfiguration _config;

    public DbConnectionFactory(IConfiguration config)
    {
        _config = config;
    }

    public IDbConnection Create(string connectionName = "Default")
    {
        var connStr = _config.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException($"Missing connection string: ConnectionStrings:{connectionName}");
        }

        return new SqlConnection(connStr);
    }
}
