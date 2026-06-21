using System.Data;
using System.Data.SqlClient;

namespace MFSS.Services.Database;

public class SqlServerProvider : IDbProvider
{
    public IDbConnection CreateConnection(string cs) => new SqlConnection(cs);
    public string Wrap(string name) => $"[{SqlSanitizer.SanitizeIdentifier(name)}]";

    public string GetCreateTableSql(string t)
    {
        var s = SqlSanitizer.SanitizeIdentifier(t);
        return $@"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='{s}')
        CREATE TABLE [{s}] (
            Id INT IDENTITY(1,1) PRIMARY KEY, SourceId INT NOT NULL UNIQUE,
            SourceUrl NVARCHAR(2000) NOT NULL, DestinationUrl NVARCHAR(2000) NULL,
            Status NVARCHAR(50) DEFAULT 'Pending', RetryCount INT DEFAULT 0,
            FileSize BIGINT DEFAULT 0, Checksum NVARCHAR(100) NULL,
            LastError NVARCHAR(MAX) NULL,
            CreatedAt DATETIME2 DEFAULT GETDATE(), UpdatedAt DATETIME2 DEFAULT GETDATE()
        )";
    }

    public string GetDropTableSql(string t) => $"DROP TABLE IF EXISTS [{SqlSanitizer.SanitizeIdentifier(t)}]";
    public string GetTableExistsSql() => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@T";
    public string GetUpsertSql(string t)
    {
        var s = SqlSanitizer.SanitizeIdentifier(t);
        return $"IF NOT EXISTS(SELECT 1 FROM [{s}] WHERE SourceId=@S) INSERT INTO [{s}](SourceId,SourceUrl,Status) VALUES(@S,@U,'Pending')";
    }
    public string GetBackupSql(string t, string b) => $"SELECT * INTO [{SqlSanitizer.SanitizeIdentifier(b)}] FROM [{SqlSanitizer.SanitizeIdentifier(t)}]";
}
