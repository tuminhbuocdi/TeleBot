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

    public IDbConnection Create()
    {
        return new SqlConnection(_config.GetConnectionString("Default"));
    }
}
