using Microsoft.Data.SqlClient;
using MFSS.Models;
using MFSS.Services;
using MFSS.Tests.Helpers;

namespace MFSS.Tests;

[Trait("Category", "Integration")]
public class DestinationDbServiceTests : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly DestinationDbService _service;
    private readonly DestinationDbConfig _config;

    public DestinationDbServiceTests()
    {
        _connectionString = TestDatabaseHelper.CreateTestDatabase("DestDb");
        _config = new DestinationDbConfig { ConnectionString = _connectionString, SeparateTablesPerSource = true };
        _service = new DestinationDbService(_config);
        _service.CreateAllTablesAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } }).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        TestDatabaseHelper.DropTestDatabase(_connectionString);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetLogTableName_SeparateTables_ReturnsPrefixedName()
    {
        var config = new DestinationDbConfig { SeparateTablesPerSource = true };
        var service = new DestinationDbService(config);
        Assert.Equal("MigrationLog_MediaFiles", await service.GetLogTableNameAsync("MediaFiles"));
    }

    [Fact]
    public async Task GetLogTableName_SingleTable_ReturnsMigrationLog()
    {
        var config = new DestinationDbConfig { SeparateTablesPerSource = false };
        var service = new DestinationDbService(config);
        Assert.Equal("MigrationLog", await service.GetLogTableNameAsync("MediaFiles"));
    }

    [Fact]
    public async Task GetAllLogTableNames_MultipleTables_ReturnsAllNames()
    {
        var tables = new List<SourceTableConfig>
        {
            new() { TableName = "Table1" },
            new() { TableName = "Table2" }
        };
        var names = await _service.GetAllLogTableNamesAsync(tables);
        Assert.Equal(2, names.Count);
        Assert.Contains("MigrationLog_Table1", names);
        Assert.Contains("MigrationLog_Table2", names);
    }

    [Fact]
    public async Task TableExists_ExistingTable_ReturnsTrue()
    {
        Assert.True(await _service.TableExistsAsync("MigrationLog_TestTable"));
    }

    [Fact]
    public async Task TableExists_NonexistentTable_ReturnsFalse()
    {
        Assert.False(await _service.TableExistsAsync("NonexistentTable"));
    }

    [Fact]
    public async Task AllTablesExist_AllExist_ReturnsTrue()
    {
        var tables = new List<SourceTableConfig> { new() { TableName = "TestTable" } };
        Assert.True(await _service.AllTablesExistAsync(tables));
    }

    [Fact]
    public async Task AllTablesExist_SomeMissing_ReturnsFalse()
    {
        var tables = new List<SourceTableConfig>
        {
            new() { TableName = "TestTable" },
            new() { TableName = "MissingTable" }
        };
        Assert.False(await _service.AllTablesExistAsync(tables));
    }

    [Fact]
    public async Task InsertBatch_NewRecords_InsertsAll()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 1, SourceTable = "TestTable", SourceUrl = "https://example.com/1.jpg" },
            new() { Id = 2, SourceTable = "TestTable", SourceUrl = "https://example.com/2.jpg" }
        };
        var inserted = await _service.InsertBatchAsync(records);
        Assert.Equal(2, inserted);
    }

    [Fact]
    public async Task InsertBatch_DuplicateRecords_UpdatesExisting()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 1, SourceTable = "TestTable", SourceUrl = "https://example.com/1.jpg" }
        };
        await _service.InsertBatchAsync(records);

        var updated = await _service.InsertBatchAsync(records);
        Assert.Equal(0, updated);
    }

    [Fact]
    public async Task GetAllPendingRecords_ReturnsOnlyPending()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 10, SourceTable = "TestTable", SourceUrl = "https://example.com/10.jpg" },
            new() { Id = 11, SourceTable = "TestTable", SourceUrl = "https://example.com/11.jpg" }
        };
        await _service.InsertBatchAsync(records);

        var pending = await _service.GetAllPendingRecordsAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } }, 3);
        Assert.Equal(2, pending.Count);
        Assert.All(pending, r => Assert.Equal("pending", r.Status));
    }

    [Fact]
    public async Task MarkSuccess_UpdatesStatus()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 20, SourceTable = "TestTable", SourceUrl = "https://example.com/20.jpg" }
        };
        await _service.InsertBatchAsync(records);

        await _service.MarkSuccessAsync(20, "TestTable", "https://dest.com/20.jpg", 1024, "abc123");

        var pending = await _service.GetAllPendingRecordsAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } }, 3);
        Assert.Empty(pending);

        var success = await _service.GetSuccessRecordsAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.Single(success);
        Assert.Equal("https://dest.com/20.jpg", success[0].Url);
    }

    [Fact]
    public async Task MarkFailed_UpdatesStatus()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 30, SourceTable = "TestTable", SourceUrl = "https://example.com/30.jpg" }
        };
        await _service.InsertBatchAsync(records);

        await _service.MarkFailedAsync(30, "TestTable", "Transfer failed", 3);

        var summary = await _service.GetSummaryAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.Equal(1, summary["failed"]);
    }

    [Fact]
    public async Task FindExistingByHash_ExistingHash_ReturnsRecord()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 40, SourceTable = "TestTable", SourceUrl = "https://example.com/40.jpg" }
        };
        await _service.InsertBatchAsync(records);
        await _service.MarkSuccessAsync(40, "TestTable", "https://dest.com/40.jpg", 2048, "hash123");

        var result = await _service.FindExistingByHashAsync("hash123", new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.NotNull(result);
        Assert.Equal(40, result!.Value.Id);
        Assert.Equal("https://dest.com/40.jpg", result.Value.DestinationUrl);
    }

    [Fact]
    public async Task FindExistingByHash_NonexistentHash_ReturnsNull()
    {
        var result = await _service.FindExistingByHashAsync("nothash", new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummary_ReturnsCorrectCounts()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 50, SourceTable = "TestTable", SourceUrl = "https://example.com/50.jpg" },
            new() { Id = 51, SourceTable = "TestTable", SourceUrl = "https://example.com/51.jpg" },
            new() { Id = 52, SourceTable = "TestTable", SourceUrl = "https://example.com/52.jpg" }
        };
        await _service.InsertBatchAsync(records);
        await _service.MarkSuccessAsync(50, "TestTable", "https://dest.com/50.jpg", 100, "h1");
        await _service.MarkFailedAsync(51, "TestTable", "error", 3);

        var summary = await _service.GetSummaryAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.Equal(1, summary["success"]);
        Assert.Equal(1, summary["failed"]);
        Assert.Equal(1, summary["pending"]);
    }

    [Fact]
    public async Task DetectAndResetUrlChanges_ChangedUrl_ResetsToPending()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 60, SourceTable = "TestTable", SourceUrl = "https://old.com/60.jpg" }
        };
        await _service.InsertBatchAsync(records);
        await _service.MarkSuccessAsync(60, "TestTable", "https://dest.com/60.jpg", 100, "h1");

        var changed = new List<MediaRecord>
        {
            new() { Id = 60, SourceTable = "TestTable", SourceUrl = "https://new.com/60.jpg" }
        };
        var resets = await _service.DetectAndResetUrlChangesAsync(changed);
        Assert.Equal(1, resets);

        var pending = await _service.GetAllPendingRecordsAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } }, 3);
        Assert.Single(pending);
    }

    [Fact]
    public async Task DetectAndResetUrlChanges_SameUrl_DoesNotReset()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 70, SourceTable = "TestTable", SourceUrl = "https://same.com/70.jpg" }
        };
        await _service.InsertBatchAsync(records);
        await _service.MarkSuccessAsync(70, "TestTable", "https://dest.com/70.jpg", 100, "h1");

        var resets = await _service.DetectAndResetUrlChangesAsync(records);
        Assert.Equal(0, resets);
    }

    [Fact]
    public async Task GetSuccessRecords_ReturnsOnlySuccessful()
    {
        var records = new List<MediaRecord>
        {
            new() { Id = 80, SourceTable = "TestTable", SourceUrl = "https://example.com/80.jpg" },
            new() { Id = 81, SourceTable = "TestTable", SourceUrl = "https://example.com/81.jpg" }
        };
        await _service.InsertBatchAsync(records);
        await _service.MarkSuccessAsync(80, "TestTable", "https://dest.com/80.jpg", 100, "h1");
        await _service.MarkFailedAsync(81, "TestTable", "error", 3);

        var success = await _service.GetSuccessRecordsAsync(new List<SourceTableConfig> { new() { TableName = "TestTable" } });
        Assert.Single(success);
        Assert.Equal(80, success[0].Id);
    }
}
