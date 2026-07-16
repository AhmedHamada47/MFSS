using MFSS.Models;
using MFSS.Services;
using MFSS.StorageProviders;
using MFSS.Tests.Helpers;

namespace MFSS.Tests;

[Trait("Category", "Integration")]
public class MigrationIntegrationTests : IDisposable
{
    private readonly string _connectionString;

    public MigrationIntegrationTests()
    {
        _connectionString = TestDatabaseHelper.CreateTestDatabase("Integration");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            CREATE TABLE MediaFiles (
                MediaId BIGINT PRIMARY KEY,
                FilePath NVARCHAR(2048) NULL
            )");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            INSERT INTO MediaFiles (MediaId, FilePath) VALUES
            (1, 'https://picsum.photos/200/300'),
            (2, 'https://picsum.photos/400/500'),
            (3, 'https://picsum.photos/600/700'),
            (4, NULL),
            (5, 'https://picsum.photos/800/900')");
    }

    public void Dispose()
    {
        TestDatabaseHelper.DropTestDatabase(_connectionString);
    }

    private (SourceDbService src, DestinationDbService dest) CreateServices(bool separateTables = true)
    {
        var srcConfig = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "MediaFiles", UrlColumn = "FilePath", IdColumn = "MediaId" }
            }
        };
        var destConfig = new DestinationDbConfig
        {
            ConnectionString = _connectionString,
            SeparateTablesPerSource = separateTables
        };
        return (new SourceDbService(srcConfig), new DestinationDbService(destConfig));
    }

    [Fact]
    public async Task FullWorkflow_FetchAndRegister_CompletesSuccessfully()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        Assert.Equal(4, records.Count);

        var inserted = await dest.InsertBatchAsync(records.ToList());
        Assert.Equal(4, inserted);

        var pending = await dest.GetAllPendingRecordsAsync(tables, 3);
        Assert.Equal(4, pending.Count);
    }

    [Fact]
    public async Task FullWorkflow_DuplicateInsert_UpdatesNotDuplicates()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        await dest.InsertBatchAsync(records.ToList());

        var secondInsert = await dest.InsertBatchAsync(records.ToList());
        Assert.Equal(0, secondInsert);

        var pending = await dest.GetAllPendingRecordsAsync(tables, 3);
        Assert.Equal(4, pending.Count);
    }

    [Fact]
    public async Task FullWorkflow_MarkSuccess_UpdatesCorrectly()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        await dest.InsertBatchAsync(records.ToList());

        await dest.MarkSuccessAsync(1, "MediaFiles", "https://dest.com/1.jpg", 5000, "hash1");
        await dest.MarkSuccessAsync(2, "MediaFiles", "https://dest.com/2.jpg", 6000, "hash2");

        var summary = await dest.GetSummaryAsync(tables);
        Assert.Equal(2, summary["success"]);
        Assert.Equal(2, summary["pending"]);
    }

    [Fact]
    public async Task FullWorkflow_MarkFailed_UpdatesCorrectly()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        await dest.InsertBatchAsync(records.ToList());

        await dest.MarkFailedAsync(1, "MediaFiles", "Connection timeout", 3);

        var summary = await dest.GetSummaryAsync(tables);
        Assert.Equal(1, summary["failed"]);
        Assert.Equal(3, summary["pending"]);
    }

    [Fact]
    public async Task FullWorkflow_Deduplication_ReusesExistingHash()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        await dest.InsertBatchAsync(records.ToList());

        await dest.MarkSuccessAsync(1, "MediaFiles", "https://dest.com/1.jpg", 5000, "samehash");
        await dest.MarkSuccessAsync(2, "MediaFiles", "https://dest.com/2.jpg", 5000, "samehash");

        var existing = await dest.FindExistingByHashAsync("samehash", tables);
        Assert.NotNull(existing);
        Assert.Equal("https://dest.com/1.jpg", existing!.Value.DestinationUrl);
    }

    [Fact]
    public async Task FullWorkflow_UrlChangeDetection_ResetsRecord()
    {
        var (src, dest) = CreateServices();
        var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };

        await dest.CreateAllTablesAsync(tables);
        var records = await src.FetchRecordsAsync();
        await dest.InsertBatchAsync(records.ToList());

        await dest.MarkSuccessAsync(1, "MediaFiles", "https://dest.com/1.jpg", 5000, "hash1");

        var changedRecords = new List<MediaRecord>
        {
            new() { Id = 1, SourceTable = "MediaFiles", SourceUrl = "https://new-url.com/1.jpg" }
        };
        var resets = await dest.DetectAndResetUrlChangesAsync(changedRecords);
        Assert.Equal(1, resets);

        var pending = await dest.GetAllPendingRecordsAsync(tables, 3);
        Assert.Contains(pending, r => r.Id == 1);
    }

    [Fact]
    public async Task FullWorkflow_SeparateTablesPerSource()
    {
        var srcConfig = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "MediaFiles", UrlColumn = "FilePath", IdColumn = "MediaId" }
            }
        };
        var destConfig = new DestinationDbConfig
        {
            ConnectionString = _connectionString,
            SeparateTablesPerSource = true
        };
        var dest = new DestinationDbService(destConfig);
        var tables = srcConfig.GetEffectiveTables();

        await dest.CreateAllTablesAsync(tables);
        var logTable = await dest.GetLogTableNameAsync("MediaFiles");
        Assert.Equal("MigrationLog_MediaFiles", logTable);
        Assert.True(await dest.TableExistsAsync(logTable));
    }

    [Fact]
    public async Task FullWorkflow_SingleTableMode()
    {
        var destConfig = new DestinationDbConfig
        {
            ConnectionString = _connectionString,
            SeparateTablesPerSource = false
        };
        var dest = new DestinationDbService(destConfig);

        Assert.Equal("MigrationLog", await dest.GetLogTableNameAsync("MediaFiles"));
    }

    [Fact]
    public async Task FullWorkflow_LocalFileTransfer_CompletesSuccessfully()
    {
        var srcDir = Path.Combine(Path.GetTempPath(), "mfss_integ_src_" + Guid.NewGuid().ToString("N"));
        var destDir = Path.Combine(Path.GetTempPath(), "mfss_integ_dest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(srcDir);
            Directory.CreateDirectory(destDir);
            File.WriteAllText(Path.Combine(srcDir, "test.jpg"), "fake image content");

            var srcFs = new FileSystemConfig { Type = "local", BasePath = srcDir };
            var destFs = new FileSystemConfig { Type = "local", BasePath = destDir };
            var srcProvider = new LocalStorageProvider(srcFs);
            var destProvider = new LocalStorageProvider(destFs);

            using var transfer = new FileTransferService(srcProvider, destProvider, 10, 100);
            var (newUrl, size, hash) = await transfer.TransferAsync("test.jpg");

            Assert.True(File.Exists(newUrl));
            Assert.Equal(18, size);
            Assert.NotEmpty(hash);

            var (src, dest) = CreateServices();
            var tables = new List<SourceTableConfig> { new() { TableName = "MediaFiles" } };
            await dest.CreateAllTablesAsync(tables);

            var records = await src.FetchRecordsAsync();
            await dest.InsertBatchAsync(records.ToList());
            await dest.MarkSuccessAsync(1, "MediaFiles", newUrl, size, hash);

            var existing = await dest.FindExistingByHashAsync(hash, tables);
            Assert.NotNull(existing);
        }
        finally
        {
            if (Directory.Exists(srcDir)) Directory.Delete(srcDir, true);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
        }
    }
}
