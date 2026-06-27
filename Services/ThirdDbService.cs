using MySql.Data.MySqlClient;
using MFSS.Models;

namespace MFSS.Services;

/// <summary>
/// Updates a third-party database with new file URLs after successful migration.
/// Uses atomic transactions — either all updates succeed or none are applied.
/// </summary>
public class ThirdDbService
{
    private readonly ThirdDbConfig _config;
    private readonly Logger _log;

    public ThirdDbService(ThirdDbConfig config, Logger log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Updates records in the third-party database within a single transaction.
    /// If any update fails, the entire transaction is rolled back.
    /// Returns the count of successful and failed updates.
    /// </summary>
    public (int Success, int Failed) UpdateWithTransaction(List<(long Id, string Url)> records)
    {
        int success = 0, failed = 0;

        using var conn = new MySqlConnection(_config.ConnectionString);
        conn.Open();
        using var transaction = conn.BeginTransaction();

        try
        {
            foreach (var (id, url) in records)
            {
                try
                {
                    var sql = _config.UpdateQuery
                        .Replace("{id}", "@id")
                        .Replace("{url}", "@url");

                    using var cmd = new MySqlCommand(sql, conn, transaction);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@url", url);
                    cmd.ExecuteNonQuery();
                    success++;
                }
                catch (Exception ex)
                {
                    _log.Error($"  ❌ Third DB update failed for id={id}: {ex.Message}");
                    failed++;
                    // Roll back entire transaction on any failure for data consistency
                    transaction.Rollback();
                    _log.Warning($"  ⚠️ Transaction rolled back due to failure. {success} previously successful updates were reverted.");
                    return (0, failed + success);
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            _log.Error($"  ❌ Transaction commit failed: {ex.Message}");
            try { transaction.Rollback(); } catch { }
            return (0, records.Count);
        }

        return (success, failed);
    }
}
