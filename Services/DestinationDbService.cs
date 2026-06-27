using Microsoft.Data.SqlClient;
using MFSS.Models;

namespace MFSS.Services;

public class DestinationDbService
{
    private readonly DestinationDbConfig _config;
    private readonly string _connectionString;

    public DestinationDbService(DestinationDbConfig config)
    {
        _config = config;
        _connectionString = config.ConnectionString;
    }

    public string GetLogTableName(string sourceTableName)
    {
        if (_config.SeparateTablesPerSource && !string.IsNullOrEmpty(sourceTableName))
            return `MigrationLog_{SanitizeTableName(sourceTableName)}`;
        return "MigrationLog";
    }

    public List<string> GetAllLogTableNames(List<SourceTableConfig> sourceTables)
    {
        if (!_config.SeparateTablesPerSource)
            return new List<string> { "MigrationLog" };

        return sourceTables
            .Select(t => GetLogTableName(t.TableName))
            .Distinct()
            .ToList();
    }

    public bool TableExists(string tableName)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(
            @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName", conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool AllTablesExist(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        return tableNames.All(TableExists);
    }
}
