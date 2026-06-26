using MySql.Data.MySqlClient;
using MFSS.Models;

namespace MFSS.Services;

public class SourceDbService
{
    private readonly SourceDbConfig _config;

    public SourceDbService(SourceDbConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Fetches records from all configured source tables.
    /// Each record includes the SourceTable name so it can be routed to the correct log table.
    /// </summary>
    public List<MediaRecord> FetchRecords()
    {
        var results = new List<MediaRecord>();
        var tables = _config.GetEffectiveTables();

        using var conn = new MySqlConnection(_config.ConnectionString);
        conn.Open();

        foreach (var table in tables)
        {
            // Validate table/column names to prevent SQL injection (only allow alphanumeric + underscore)
            ValidateName(table.TableName);
            ValidateName(table.IdColumn);
            ValidateName(table.UrlColumn);

            var sql = $@"SELECT `{table.IdColumn}` AS Id, `{table.UrlColumn}` AS Url 
                         FROM `{table.TableName}` 
                         WHERE `{table.UrlColumn}` IS NOT NULL AND `{table.UrlColumn}` != ''";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                results.Add(new MediaRecord
                {
                    Id = reader.GetInt64("Id"),
                    SourceUrl = reader.GetString("Url"),
                    SourceTable = table.TableName  // Tag each record with its source table
                });
            }
        }

        return results;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw new ArgumentException($"Invalid identifier name: '{name}'. Only alphanumeric characters and underscores are allowed.");
    }
}
