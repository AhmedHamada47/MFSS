using MFSS.Models;

namespace MFSS.Abstractions;

public interface IDestinationDbService
{
    Task<string> GetLogTableNameAsync(string sourceTableName);
    Task<List<string>> GetAllLogTableNamesAsync(List<SourceTableConfig> sourceTables);
    Task<bool> TableExistsAsync(string tableName);
    Task<bool> AllTablesExistAsync(List<SourceTableConfig> sourceTables);
    Task CreateAllTablesAsync(List<SourceTableConfig> sourceTables);
    Task DropAllTablesAsync(List<SourceTableConfig> sourceTables);
    Task<int> InsertBatchAsync(List<MediaRecord> records);
    Task<int> DetectAndResetUrlChangesAsync(List<MediaRecord> records);
    Task<List<MediaRecord>> GetAllPendingRecordsAsync(List<SourceTableConfig> sourceTables, int maxRetries);
    Task<(long Id, string? DestinationUrl, long? FileSize)?> FindExistingByHashAsync(string hash, List<SourceTableConfig> sourceTables);
    Task MarkSuccessAsync(long id, string sourceTable, string destinationUrl, long fileSize, string hash);
    Task MarkFailedAsync(long id, string sourceTable, string errorMessage, int retryCount);
    Task<List<(long Id, string Url)>> GetSuccessRecordsAsync(List<SourceTableConfig> sourceTables);
    Task<Dictionary<string, int>> GetSummaryAsync(List<SourceTableConfig> sourceTables);
}
