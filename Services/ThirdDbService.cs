using Microsoft.Data.SqlClient;
using MFSS.Abstractions;
using MFSS.Models;

namespace MFSS.Services;

public class ThirdDbService : IThirdDbService
{
    private readonly ThirdDbConfig _config;
    private readonly ILogger _log;

    public ThirdDbService(ThirdDbConfig config, ILogger log)
    {
        _config = config;
        _log = log;
    }

    public async Task<(int Success, int Failed)> UpdateWithTransactionAsync(List<(long Id, string Url)> records, CancellationToken ct = default)
    {
        if (records.Count == 0)
            return (0, 0);

        int success = 0, failed = 0;

        using var conn = new SqlConnection(_config.ConnectionString);
        await conn.OpenAsync(ct);
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var (id, url) in records)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var sql = _config.UpdateQuery
                        .Replace("{id}", "@id")
                        .Replace("{url}", "@url");

                    using var cmd = new SqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@url", url);
                    await cmd.ExecuteNonQueryAsync(ct);
                    success++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.Error($"  Third DB update failed for id={id}: {ex.Message}");
                    failed++;
                    transaction.Rollback();
                    _log.Warning($"  Transaction rolled back due to failure. {success} previously successful updates were reverted.");
                    return (0, failed + success);
                }
            }

            transaction.Commit();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Error($"  Transaction commit failed: {ex.Message}");
            try { transaction.Rollback(); }
            catch (Exception rollbackEx)
            {
                _log.Error($"  Rollback also failed: {rollbackEx.Message}");
            }
            return (0, records.Count);
        }

        return (success, failed);
    }
}
