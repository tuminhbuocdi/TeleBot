using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NotifyCrashNet8.Data;

namespace NotifyCrashNet8.Services;

public sealed class CrashRecordProcService
{
    private readonly string _connectionString;

    public CrashRecordProcService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default");
    }

    public async Task UpsertAndTrimAsync(
        IReadOnlyCollection<CrashRecordInput> inputs,
        int maxRows,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return;
        }

        var tvp = new DataTable();
        tvp.Columns.Add("GameId", typeof(long));
        tvp.Columns.Add("Rate", typeof(decimal));

        foreach (var x in inputs)
        {
            tvp.Rows.Add(x.GameId, x.Rate);
        }

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "dbo.CrashRecords_UpsertAndTrim";

        var pRecords = cmd.Parameters.AddWithValue("@Records", tvp);
        pRecords.SqlDbType = SqlDbType.Structured;
        pRecords.TypeName = "dbo.CrashRecordTvp";

        cmd.Parameters.Add(new SqlParameter("@MaxRows", SqlDbType.Int) { Value = maxRows });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
