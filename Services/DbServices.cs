using System.Data;
using MFSS.Models;
using MFSS.Services.Database;

namespace MFSS.Services;

public class SourceDbService
{
    private readonly SourceDbConfig _c;
    private readonly IDbProvider _db;
    public SourceDbService(SourceDbConfig c) { _c = c; _db = DbProviderFactory.Create(c.ConnectionString); }

    public List<MediaRecord> FetchRecords()
    {
        var list = new List<MediaRecord>();
        using var conn = _db.CreateConnection(_c.ConnectionString); conn.Open();
        var cmd = conn.CreateCommand();
        var filter = SqlSanitizer.SanitizeFilter(_c.Filter);
        cmd.CommandText = $"SELECT {_db.Wrap(_c.IdColumn)},{_db.Wrap(_c.UrlColumn)} FROM {_db.Wrap(_c.TableName)}" + (string.IsNullOrEmpty(filter) ? "" : $" WHERE {filter}");
        using var r = cmd.ExecuteReader();
        while (r.Read()) { var u = r.IsDBNull(1) ? "" : r.GetString(1); if (!string.IsNullOrEmpty(u)) list.Add(new() { Id = r.GetInt32(0), SourceUrl = u }); }
        return list;
    }
}

public class DestinationDbService
{
    private readonly DestinationDbConfig _c;
    private readonly IDbProvider _db;
    public DestinationDbService(DestinationDbConfig c) { _c = c; _db = DbProviderFactory.Create(c.ConnectionString); }
    private IDbConnection Open() { var c = _db.CreateConnection(_c.ConnectionString); c.Open(); return c; }
    private string T => _db.Wrap(_c.TableName);
    private void P(IDbCommand cmd, string n, object v) { var p = cmd.CreateParameter(); p.ParameterName = n; p.Value = v ?? DBNull.Value; cmd.Parameters.Add(p); }

    public void CreateTable() { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = _db.GetCreateTableSql(_c.TableName); cmd.ExecuteNonQuery(); }
    public void DropTable() { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = _db.GetDropTableSql(_c.TableName); cmd.ExecuteNonQuery(); }
    public bool TableExists() { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = _db.GetTableExistsSql(); P(cmd, "@T", _c.TableName); return Convert.ToInt32(cmd.ExecuteScalar()) > 0; }

    public int InsertBatch(List<MediaRecord> recs)
    { int n = 0; using var c = Open(); foreach (var r in recs) { var cmd = c.CreateCommand(); cmd.CommandText = _db.GetUpsertSql(_c.TableName); P(cmd, "@S", r.Id); P(cmd, "@U", r.SourceUrl); n += cmd.ExecuteNonQuery(); } return n; }

    public List<MediaRecord> GetPendingRecords(int max)
    {
        var list = new List<MediaRecord>(); using var c = Open(); var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {_db.Wrap("SourceId")},{_db.Wrap("SourceUrl")},{_db.Wrap("RetryCount")} FROM {T} WHERE {_db.Wrap("Status")} IN ('Pending','Failed') AND {_db.Wrap("RetryCount")}<@M ORDER BY {_db.Wrap("RetryCount")}";
        P(cmd, "@M", max); using var r = cmd.ExecuteReader(); while (r.Read()) list.Add(new() { Id = r.GetInt32(0), SourceUrl = r.GetString(1), RetryCount = r.GetInt32(2) }); return list;
    }

    public void MarkSuccess(int id, string url, long size, string hash)
    { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = $"UPDATE {T} SET {_db.Wrap("DestinationUrl")}=@D,{_db.Wrap("Status")}='Success',{_db.Wrap("FileSize")}=@Z,{_db.Wrap("Checksum")}=@C,{_db.Wrap("RetryCount")}={_db.Wrap("RetryCount")}+1,{_db.Wrap("UpdatedAt")}=@N WHERE {_db.Wrap("SourceId")}=@S";
    P(cmd,"@D",url);P(cmd,"@Z",size);P(cmd,"@C",hash);P(cmd,"@S",id);P(cmd,"@N",DateTime.UtcNow);cmd.ExecuteNonQuery(); }

    public void MarkFailed(int id, string err, int max)
    { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = $"UPDATE {T} SET {_db.Wrap("Status")}=CASE WHEN {_db.Wrap("RetryCount")}+1>=@M THEN 'DeadLetter' ELSE 'Failed' END,{_db.Wrap("LastError")}=@E,{_db.Wrap("RetryCount")}={_db.Wrap("RetryCount")}+1,{_db.Wrap("UpdatedAt")}=@N WHERE {_db.Wrap("SourceId")}=@S";
    P(cmd,"@E",err);P(cmd,"@S",id);P(cmd,"@M",max);P(cmd,"@N",DateTime.UtcNow);cmd.ExecuteNonQuery(); }

    public Dictionary<string, int> GetSummary()
    { var d = new Dictionary<string, int>(); using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = $"SELECT {_db.Wrap("Status")},COUNT(*) FROM {T} GROUP BY {_db.Wrap("Status")}"; using var r = cmd.ExecuteReader(); while (r.Read()) d[r.GetString(0)] = r.GetInt32(1); return d; }

    public List<(int Id, string Url)> GetSuccessRecords()
    { var l = new List<(int, string)>(); using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = $"SELECT {_db.Wrap("SourceId")},{_db.Wrap("DestinationUrl")} FROM {T} WHERE {_db.Wrap("Status")}='Success'"; using var r = cmd.ExecuteReader(); while (r.Read()) l.Add((r.GetInt32(0), r.GetString(1))); return l; }

    public void BackupTable() { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = _db.GetBackupSql(_c.TableName, $"{_c.TableName}_Bak_{DateTime.Now:yyyyMMdd_HHmmss}"); cmd.ExecuteNonQuery(); }

    public void ExportCsv(string path)
    { using var c = Open(); var cmd = c.CreateCommand(); cmd.CommandText = $"SELECT {_db.Wrap("SourceId")},{_db.Wrap("SourceUrl")},{_db.Wrap("DestinationUrl")},{_db.Wrap("Status")},{_db.Wrap("FileSize")},{_db.Wrap("Checksum")} FROM {T}";
    using var r = cmd.ExecuteReader(); var lines = new List<string> { "SourceId,SourceUrl,DestinationUrl,Status,FileSize,Checksum" };
    while (r.Read()) lines.Add($"\"{r.GetInt32(0)}\",\"{r.GetString(1)}\",\"{(r.IsDBNull(2)?"":r.GetString(2))}\",\"{r.GetString(3)}\",\"{r.GetInt64(4)}\",\"{(r.IsDBNull(5)?"":r.GetString(5))}\""); File.WriteAllLines(path, lines); }
}

public class ThirdDbService
{
    private readonly ThirdDbConfig _c; private readonly Logger _log; private readonly IDbProvider _db;
    public ThirdDbService(ThirdDbConfig c, Logger log) { _c = c; _log = log; _db = DbProviderFactory.Create(c.ConnectionString); }

    public (int ok, int fail) UpdateWithTransaction(List<(int Id, string Url)> recs)
    {
        if (!_c.Enabled) return (0, 0);
        using var conn = _db.CreateConnection(_c.ConnectionString); conn.Open(); using var tx = conn.BeginTransaction(); int ok = 0;
        try { foreach (var r in recs) { var cmd = conn.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = $"UPDATE {_db.Wrap(_c.TableName)} SET {_db.Wrap(_c.UrlColumn)}=@U WHERE {_db.Wrap(_c.IdColumn)}=@I";
        var p1 = cmd.CreateParameter(); p1.ParameterName="@U"; p1.Value=r.Url; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName="@I"; p2.Value=r.Id; cmd.Parameters.Add(p2); ok += cmd.ExecuteNonQuery(); }
        tx.Commit(); _log.Success($"  ✅ {ok} rows updated"); return (ok, 0); }
        catch (Exception ex) { _log.Error($"  ❌ {ex.Message}"); try { tx.Rollback(); } catch { } return (0, recs.Count); }
    }
}
