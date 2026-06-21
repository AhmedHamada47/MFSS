using System.Data;

namespace MFSS.Services.Database;

public interface IDbProvider
{
    IDbConnection CreateConnection(string cs);
    string Wrap(string name);
    string GetCreateTableSql(string tableName);
    string GetDropTableSql(string tableName);
    string GetTableExistsSql();
    string GetUpsertSql(string tableName);
    string GetBackupSql(string tableName, string backupName);
}
