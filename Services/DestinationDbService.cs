using MySql.Data.MySqlClient;
using MFSS.Models;

namespace MFSS.Services;

/// <summary>
/// Manages migration log tables in the destination database.
/// Supports creating separate tables per source table when SeparateTablesPerSource is enabled.
/// </summary>
public class DestinationDbService
{
    private readonly DestinationDbConfig _config;
    private readonly string _connectionString;

    public DestinationDbService(DestinationDbConfig config)
    {
        _config = config;
        _connectionString = config.ConnectionString;
    }

    /// <summary>
    /// Gets the migration log table name for a given source table.
    /// If SeparateTablesPerSource is true, returns "MigrationLog_{sourceTableName}".
    /// Otherwise returns "MigrationLog".
    /// </summary>
    public string GetLogTableName(string sourceTableName)
    {
        if (_config.SeparateTablesPerSource && !string.IsNullOrEmpty(sourceTableName))
            return $"MigrationLog_{SanitizeTableName(sourceTableName)}";
        return "MigrationLog";
    }

    /// <summary>
    /// Gets all distinct log table names based on the source tables configuration.
    /// </summary>
    public List<string> GetAllLogTableNames(List<SourceTableConfig> sourceTables)
    {
        if (!_config.SeparateTablesPerSource)
            return new List<string> { "MigrationLog" };

        return sourceTables
            .Select(t => GetLogTableName(t.TableName))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Checks if a specific migration log table exists.
    /// </summary>
    public bool TableExists(string tableName)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = new MySqlCommand(
            @"SELECT COUNT(*) FROM information_schema.tables 
              WHERE table_schema = DATABASE() AND table_name = @tableName", conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// Checks if all required migration log tables exist.
    /// </summary>
    public bool AllTablesExist(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        return tableNames.All(TableExists);
    }

    /// <summary>
    /// Creates a migration log table with the given name.
    /// </summary>
    public void CreateTable(string tableName)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        var sql = $@"
            CREATE TABLE IF NOT EXISTS `{SanitizeTableName(tableName)}` (
                Id BIGINT PRIMARY KEY AUTO_INCREMENT,
                SourceId BIGINT NOT NULL,
                SourceTable VARCHAR(255) NOT NULL,
                SourceUrl TEXT NOT NULL,
                DestinationUrl TEXT NULL,
                FileSize BIGINT NULL,
                FileHash VARCHAR(128) NULL,
                Status VARCHAR(50) NOT NULL DEFAULT 'pending',
                RetryCount INT NOT NULL DEFAULT 0,
                ErrorMessage TEXT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_status (Status),
                INDEX idx_source_id (SourceId)
            )";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates all required migration log tables for the given source tables.
    /// </summary>
    public void CreateAllTables(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            CreateTable(tableName);
        }
    }

    /// <summary>
    /// Drops a specific migration log table.
    /// </summary>
    public void DropTable(string tableName)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = new MySqlCommand($"DROP TABLE IF EXISTS `{SanitizeTableName(tableName)}`", conn);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Drops all migration log tables for the given source tables.
    /// </summary>
    public void DropAllTables(List<SourceTableConfig> sourceTables)
    {
        var tableNames = GetAllLogTableNames(sourceTables);
        foreach (var tableName in tableNames)
        {
            DropTable(tableName);
        }
    }

    /// <summary>
    /// Inserts a batch of records into their respective migration log tables.
    /// Records are grouped by SourceTable and inserted into the correct log table.
    /// </summary>
    public int InsertBatch(List<MediaRecord> records)
    {
        int totalInserted = 0;

        // Group records by source table for per-table insertion
        var grouped = records.GroupBy(r => r.SourceTable);

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        foreach (var group in grouped)
        {
            var logTable = GetLogTableName(group.Key);

            foreach (var record in group)
            {
                var sql = $@"INSERT INTO `{SanitizeTableName(logTable)}` 
                    (SourceId, SourceTable, SourceUrl, Status) 
                    VALUES (@sourceId, @sourceTable, @sourceUrl, 'pending')
                    ON DUPLICATE KEY UPDATE SourceId = SourceId";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@sourceId", record.Id);
                cmd.Parameters.AddWithValue("@sourceTable", record.SourceTable);
                cmd.Parameters.AddWithValue("@sourceUrl", record.SourceUrl);
                totalInserted += cmd.ExecuteNonQuery();
            }
        }

        return totalInserted;
    }

    /// <summary>
    /// Gets pending records from a specific log table.
    /// </summary>
    public List<MediaRecord> GetPendingRecords(string tableName, int maxRetries)
    {
        var results = new List<MediaRecord>();
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        var sql = $@"SELECT Id, SourceId, SourceUrl, SourceTable, RetryCount 
                     FROM `{SanitizeTableName(tableName)}` 
                     WHERE Status = 'pending' AND RetryCount < @maxRetries";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@maxRetries", maxRetries);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            results.Add(new MediaRecord
            {
                Id = reader.GetInt64("Id"),
                SourceUrl = reader.GetString("SourceUrl"),
                SourceTable = reader.GetString("SourceTable"),
                RetryCount = reader.GetInt32("RetryCount")
            });
        }

        return results;
    }

    /// <summary>
    /// Gets all pending records across all log tables for the given source tables.
    /// </summary>
    public List<MediaRecord> GetAllPendingRecords(List<SourceTableConfig> sourceTables, int maxRetries)
    {
        var allRecords = new List<MediaRecord>();
        var tableNames = GetAllLogTableNames(sourceTables);

        foreach (var tableName in tableNames)
        {
            if (TableExists(tableName))
            {
                allRecords.AddRange(GetPendingRecords(tableName, maxRetries));
            }
        }

        return allRecords;
    }

    /// <summary>
    /// Marks a record as successfully migrated.
    /// </summary>
    public void MarkSuccess(long recordId, string sourceTable, string destinationUrl, long fileSize, string fileHash)
    {
        var logTable = GetLogTableName(sourceTable);
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        var sql = $@"UPDATE `{SanitizeTableName(logTable)}` 
                     SET Status = 'success', DestinationUrl = @destUrl, FileSize = @size, FileHash = @hash 
                     WHERE Id = @id";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", recordId);
        cmd.Parameters.AddWithValue("@destUrl", destinationUrl);
        cmd.Parameters.AddWithValue("@size", fileSize);
        cmd.Parameters.AddWithValue("@hash", fileHash);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Marks a record as failed.
    /// </summary>
    public void MarkFailed(long recordId, string sourceTable, string errorMessage, int maxRetries)
    {
        var logTable = GetLogTableName(sourceTable);
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        var sql = $@"UPDATE `{SanitizeTableName(logTable)}` 
                     SET Status = CASE WHEN RetryCount >= @maxRetries - 1 THEN 'failed' ELSE 'pending' END,
                         RetryCount = RetryCount + 1,
                         ErrorMessage = @error 
                     WHERE Id = @id";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", recordId);
        cmd.Parameters.AddWithValue("@maxRetries", maxRetries);
        cmd.Parameters.AddWithValue("@error", errorMessage);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets all successful records across all log tables.
    /// </summary>
    public List<(long Id, string Url)> GetSuccessRecords(List<SourceTableConfig> sourceTables)
    {
        var results = new List<(long, string)>();
        var tableNames = GetAllLogTableNames(sourceTables);

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        foreach (var tableName in tableNames)
        {
            if (!TableExists(tableName)) continue;

            var sql = $@"SELECT SourceId, DestinationUrl FROM `{SanitizeTableName(tableName)}` WHERE Status = 'success'";
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                results.Add((reader.GetInt64("SourceId"), reader.GetString("DestinationUrl")));
            }
        }

        return results;
    }

    /// <summary>
    /// Gets migration summary across all log tables.
    /// </summary>
    public Dictionary<string, int> GetSummary(List<SourceTableConfig> sourceTables)
    {
        var summary = new Dictionary<string, int>
        {
            ["Total"] = 0,
            ["Success"] = 0,
            ["Failed"] = 0,
            ["Pending"] = 0
        };

        var tableNames = GetAllLogTableNames(sourceTables);
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();

        foreach (var tableName in tableNames)
        {
            if (!TableExists(tableName)) continue;

            var sql = $@"SELECT Status, COUNT(*) as Cnt FROM `{SanitizeTableName(tableName)}` GROUP BY Status";
            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var status = reader.GetString("Status");
                var count = reader.GetInt32("Cnt");
                summary["Total"] += count;

                if (status == "success") summary["Success"] += count;
                else if (status == "failed") summary["Failed"] += count;
                else summary["Pending"] += count;
            }
        }

        return summary;
    }

    /// <summary>
    /// Sanitizes a table name to prevent SQL injection.
    /// Only allows alphanumeric characters and underscores.
    /// </summary>
    private static string SanitizeTableName(string name)
    {
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }
}
