using MySql.Data.MySqlClient;
using MFSS.Models;

namespace MFSS.Services;

public class ThirdDbService
{
    private readonly ThirdDbConfig _config;
    private readonly Logger _log;

    public ThirdDbService(ThirdDbConfig config, Logger log)
    {
        _config = config;
        _log = log;
    }

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
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return (success, failed);
    }
}
