using Microsoft.Data.SqlClient;
using MFSS.Abstractions;
using MFSS.Models;

namespace MFSS.Services;

public class SourceDbService : ISourceDbService
{
    private readonly SourceDbConfig _config;

    public SourceDbService(SourceDbConfig config)
    {
        _config = config;
    }

    public async Task<IReadOnlyList<MediaRecord>> FetchRecordsAsync(CancellationToken ct = default)
    {
        var results = new List<MediaRecord>();
        var tables = _config.GetEffectiveTables();

        using var conn = new SqlConnection(_config.ConnectionString);
        await conn.OpenAsync(ct);

        foreach (var table in tables)
        {
            ValidateName(table.TableName);
            ValidateName(table.IdColumn);
            ValidateName(table.UrlColumn);

            var sql = $@"SELECT [{table.IdColumn}] AS Id, [{table.UrlColumn}] AS Url 
                         FROM [{table.TableName}] 
                         WHERE [{table.UrlColumn}] IS NOT NULL AND [{table.UrlColumn}] != ''";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                results.Add(new MediaRecord
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    SourceUrl = reader.GetString(reader.GetOrdinal("Url")),
                    SourceTable = table.TableName
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
