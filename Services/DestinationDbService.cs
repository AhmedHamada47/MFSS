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
            return $"MigrationLog_{SanitizeTableName(sourceTableName)}";
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

    public void CreateAllTables(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        foreach (var tableName in tableNames)
        {
            var sql = $@"
                CREATE TABLE [{tableName}] (
                    [Id] BIGINT NOT NULL,
                    [SourceTable] NVARCHAR(255) NOT NULL,
                    [SourceUrl] NVARCHAR(2048) NULL,
                    [DestinationUrl] NVARCHAR(2048) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT 'pending',
                    [FileSize] BIGINT NULL,
                    [FileHash] NVARCHAR(128) NULL,
                    [ErrorMessage] NVARCHAR(MAX) NULL,
                    [RetryCount] INT NOT NULL DEFAULT 0,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_{tableName}] PRIMARY KEY ([Id], [SourceTable])
                )";
            using var cmd = new SqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }

    public void DropAllTables(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        foreach (var tableName in tableNames)
        {
            using var cmd = new SqlCommand($"IF OBJECT_ID('{tableName}', 'U') IS NOT NULL DROP TABLE [{tableName}]", conn);
            cmd.ExecuteNonQuery();
        }
    }

    public int InsertBatch(List<MediaRecord> records)
    {
        int inserted = 0;
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        foreach (var record in records)
        {
            var tableName = GetLogTableName(record.SourceTable);
            var sql = $@"
                IF NOT EXISTS (SELECT 1 FROM [{tableName}] WHERE [Id] = @Id AND [SourceTable] = @SourceTable)
                BEGIN
                    INSERT INTO [{tableName}] ([Id], [SourceTable], [SourceUrl], [Status])
                    VALUES (@Id, @SourceTable, @SourceUrl, 'pending');
                    SET @Inserted = 1;
                END
                ELSE
                BEGIN
                    UPDATE [{tableName}] SET [SourceUrl] = @SourceUrl, [UpdatedAt] = GETUTCDATE()
                    WHERE [Id] = @Id AND [SourceTable] = @SourceTable;
                    SET @Inserted = 0;
                END";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", record.Id);
            cmd.Parameters.AddWithValue("@SourceTable", record.SourceTable);
            cmd.Parameters.AddWithValue("@SourceUrl", record.SourceUrl);
            var insertedParam = new SqlParameter("@Inserted", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
            cmd.Parameters.Add(insertedParam);
            cmd.ExecuteNonQuery();
            inserted += Convert.ToInt32(insertedParam.Value);
        }
        return inserted;
    }

    public int DetectAndResetUrlChanges(List<MediaRecord> records)
    {
        int resets = 0;
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        foreach (var record in records)
        {
            var tableName = GetLogTableName(record.SourceTable);
            var sql = $@"
                UPDATE [{tableName}]
                SET [Status] = 'pending', [DestinationUrl] = NULL, [UpdatedAt] = GETUTCDATE()
                WHERE [Id] = @Id AND [SourceTable] = @SourceTable
                  AND [SourceUrl] != @SourceUrl AND [Status] = 'success'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", record.Id);
            cmd.Parameters.AddWithValue("@SourceTable", record.SourceTable);
            cmd.Parameters.AddWithValue("@SourceUrl", record.SourceUrl);
            resets += cmd.ExecuteNonQuery();
        }
        return resets;
    }

    public List<MediaRecord> GetAllPendingRecords(List<SourceTableConfig> sourceTables, int maxRetries)
    {
        var results = new List<MediaRecord>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Id], [SourceTable], [SourceUrl] FROM [{tableName}]
                         WHERE [Status] = 'pending' AND [RetryCount] < @MaxRetries";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaxRetries", maxRetries);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new MediaRecord
                {
                    Id = reader.GetInt64(0),
                    SourceTable = reader.GetString(1),
                    SourceUrl = reader.GetString(2)
                });
            }
        }
        return results;
    }

    public (long Id, string? DestinationUrl, long? FileSize)? FindExistingByHash(string hash, List<SourceTableConfig> sourceTables)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT TOP 1 [Id], [DestinationUrl], [FileSize] FROM [{tableName}]
                         WHERE [FileHash] = @Hash AND [Status] = 'success'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Hash", hash);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return (reader.GetInt64(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt64(2));
            }
        }
        return null;
    }

    public void MarkSuccess(long id, string sourceTable, string destinationUrl, long fileSize, string hash)
    {
        var tableName = GetLogTableName(sourceTable);
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var sql = $@"UPDATE [{tableName}]
                     SET [Status] = 'success', [DestinationUrl] = @DestUrl, [FileSize] = @Size,
                         [FileHash] = @Hash, [UpdatedAt] = GETUTCDATE()
                     WHERE [Id] = @Id AND [SourceTable] = @SourceTable";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@SourceTable", sourceTable);
        cmd.Parameters.AddWithValue("@DestUrl", destinationUrl);
        cmd.Parameters.AddWithValue("@Size", fileSize);
        cmd.Parameters.AddWithValue("@Hash", hash);
        cmd.ExecuteNonQuery();
    }

    public void MarkFailed(long id, string sourceTable, string errorMessage, int retryCount)
    {
        var tableName = GetLogTableName(sourceTable);
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var sql = $@"UPDATE [{tableName}]
                     SET [Status] = 'failed', [ErrorMessage] = @Error, [RetryCount] = @Retries, [UpdatedAt] = GETUTCDATE()
                     WHERE [Id] = @Id AND [SourceTable] = @SourceTable";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@SourceTable", sourceTable);
        cmd.Parameters.AddWithValue("@Error", errorMessage);
        cmd.Parameters.AddWithValue("@Retries", retryCount);
        cmd.ExecuteNonQuery();
    }

    public List<(long Id, string Url)> GetSuccessRecords(List<SourceTableConfig> sourceTables)
    {
        var results = new List<(long, string)>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Id], [DestinationUrl] FROM [{tableName}] WHERE [Status] = 'success'";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                    results.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }
        return results;
    }

    public Dictionary<string, int> GetSummary(List<SourceTableConfig> sourceTables)
    {
        var summary = new Dictionary<string, int>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Status], COUNT(*) FROM [{tableName}] GROUP BY [Status]";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var status = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (summary.ContainsKey(status))
                    summary[status] += count;
                else
                    summary[status] = count;
            }
        }
        return summary;
    }

    private static string SanitizeTableName(string name)
    {
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }
}
