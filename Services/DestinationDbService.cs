using Microsoft.Data.SqlClient;
using MFSS.Abstractions;
using MFSS.Models;
using static MFSS.Models.MigrationStatus;

namespace MFSS.Services;

public class DestinationDbService : IDestinationDbService
{
    private readonly DestinationDbConfig _config;
    private readonly string _connectionString;

    public DestinationDbService(DestinationDbConfig config)
    {
        _config = config;
        _connectionString = config.ConnectionString;
    }

    public Task<string> GetLogTableNameAsync(string sourceTableName)
    {
        if (_config.SeparateTablesPerSource && !string.IsNullOrEmpty(sourceTableName))
            return Task.FromResult($"MigrationLog_{SanitizeTableName(sourceTableName)}");
        return Task.FromResult("MigrationLog");
    }

    public Task<List<string>> GetAllLogTableNamesAsync(List<SourceTableConfig> sourceTables)
    {
        if (!_config.SeparateTablesPerSource)
            return Task.FromResult(new List<string> { "MigrationLog" });

        return Task.FromResult(sourceTables
            .Select(t => $"MigrationLog_{SanitizeTableName(t.TableName)}")
            .Distinct()
            .ToList());
    }

    public async Task<bool> TableExistsAsync(string tableName)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName", conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<bool> AllTablesExistAsync(List<SourceTableConfig> sourceTables)
    {
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        foreach (var name in tableNames)
            if (!await TableExistsAsync(name)) return false;
        return true;
    }

    public async Task CreateAllTablesAsync(List<SourceTableConfig> sourceTables)
    {
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
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
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DropAllTablesAsync(List<SourceTableConfig> sourceTables)
    {
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var tableName in tableNames)
        {
            var safeName = SanitizeTableName(tableName);
            using var cmd = new SqlCommand($"IF OBJECT_ID('{safeName}', 'U') IS NOT NULL DROP TABLE [{safeName}]", conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<int> InsertBatchAsync(List<MediaRecord> records)
    {
        int inserted = 0;
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var record in records)
        {
            var tableName = await GetLogTableNameAsync(record.SourceTable);
            var sql = $@"
                IF NOT EXISTS (SELECT 1 FROM [{tableName}] WHERE [Id] = @Id AND [SourceTable] = @SourceTable)
                BEGIN
                    INSERT INTO [{tableName}] ([Id], [SourceTable], [SourceUrl], [Status])
                    VALUES (@Id, @SourceTable, @SourceUrl, '{Pending}');
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
            await cmd.ExecuteNonQueryAsync();
            inserted += Convert.ToInt32(insertedParam.Value);
        }
        return inserted;
    }

    public async Task<int> DetectAndResetUrlChangesAsync(List<MediaRecord> records)
    {
        int resets = 0;
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        foreach (var record in records)
        {
            var tableName = await GetLogTableNameAsync(record.SourceTable);
            var sql = $@"
                UPDATE [{tableName}]
                SET [Status] = '{Pending}', [DestinationUrl] = NULL, [UpdatedAt] = GETUTCDATE()
                  WHERE [Id] = @Id AND [SourceTable] = @SourceTable
                  AND [SourceUrl] != @SourceUrl AND [Status] = '{Success}'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", record.Id);
            cmd.Parameters.AddWithValue("@SourceTable", record.SourceTable);
            cmd.Parameters.AddWithValue("@SourceUrl", record.SourceUrl);
            resets += await cmd.ExecuteNonQueryAsync();
        }
        return resets;
    }

    public async Task<List<MediaRecord>> GetAllPendingRecordsAsync(List<SourceTableConfig> sourceTables, int maxRetries)
    {
        var results = new List<MediaRecord>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Id], [SourceTable], [SourceUrl] FROM [{tableName}]
                         WHERE [Status] = '{Pending}' AND [RetryCount] < @MaxRetries";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaxRetries", maxRetries);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
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

    public async Task<(long Id, string? DestinationUrl, long? FileSize)?> FindExistingByHashAsync(string hash, List<SourceTableConfig> sourceTables)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT TOP 1 [Id], [DestinationUrl], [FileSize] FROM [{tableName}]
                         WHERE [FileHash] = @Hash AND [Status] = '{Success}'";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Hash", hash);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt64(0),
                        reader.IsDBNull(1) ? null : reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetInt64(2));
            }
        }
        return null;
    }

    public async Task MarkSuccessAsync(long id, string sourceTable, string destinationUrl, long fileSize, string hash)
    {
        var tableName = await GetLogTableNameAsync(sourceTable);
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = $@"UPDATE [{tableName}]
                     SET [Status] = '{Success}', [DestinationUrl] = @DestUrl, [FileSize] = @Size,
                         [FileHash] = @Hash, [UpdatedAt] = GETUTCDATE()
                     WHERE [Id] = @Id AND [SourceTable] = @SourceTable";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@SourceTable", sourceTable);
        cmd.Parameters.AddWithValue("@DestUrl", destinationUrl);
        cmd.Parameters.AddWithValue("@Size", fileSize);
        cmd.Parameters.AddWithValue("@Hash", hash);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkFailedAsync(long id, string sourceTable, string errorMessage, int retryCount)
    {
        var tableName = await GetLogTableNameAsync(sourceTable);
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = $@"UPDATE [{tableName}]
                     SET [Status] = '{Failed}', [ErrorMessage] = @Error, [RetryCount] = @Retries, [UpdatedAt] = GETUTCDATE()
                     WHERE [Id] = @Id AND [SourceTable] = @SourceTable";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@SourceTable", sourceTable);
        cmd.Parameters.AddWithValue("@Error", errorMessage);
        cmd.Parameters.AddWithValue("@Retries", retryCount);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(long Id, string Url)>> GetSuccessRecordsAsync(List<SourceTableConfig> sourceTables)
    {
        var results = new List<(long, string)>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Id], [DestinationUrl] FROM [{tableName}] WHERE [Status] = '{Success}'";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(1))
                    results.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }
        return results;
    }

    public async Task<Dictionary<string, int>> GetSummaryAsync(List<SourceTableConfig> sourceTables)
    {
        var summary = new Dictionary<string, int>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var tableNames = await GetAllLogTableNamesAsync(sourceTables);
        foreach (var tableName in tableNames)
        {
            var sql = $@"SELECT [Status], COUNT(*) FROM [{tableName}] GROUP BY [Status]";
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
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
