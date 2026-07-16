using Microsoft.Data.SqlClient;
using MFSS.Models;
using MFSS.Services;
using MFSS.Tests.Helpers;

namespace MFSS.Tests;

[Trait("Category", "Integration")]
public class SourceDbServiceTests : IDisposable
{
    private readonly string _connectionString;

    public SourceDbServiceTests()
    {
        _connectionString = TestDatabaseHelper.CreateTestDatabase("SrcDb");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            CREATE TABLE TestMedia (
                Id BIGINT PRIMARY KEY,
                Url NVARCHAR(2048) NULL
            )");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            INSERT INTO TestMedia (Id, Url) VALUES
            (1, 'https://example.com/image1.jpg'),
            (2, 'https://example.com/image2.png'),
            (3, NULL),
            (4, ''),
            (5, 'https://example.com/image5.gif')");
    }

    public void Dispose()
    {
        TestDatabaseHelper.DropTestDatabase(_connectionString);
    }

    private SourceDbService CreateService(string tableName = "TestMedia", string urlColumn = "Url", string idColumn = "Id")
    {
        var config = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = tableName, UrlColumn = urlColumn, IdColumn = idColumn }
            }
        };
        return new SourceDbService(config);
    }

    [Fact]
    public async Task FetchRecords_SingleTable_ReturnsValidRecords()
    {
        var service = CreateService();
        var records = await service.FetchRecordsAsync();

        Assert.Equal(3, records.Count);
        Assert.All(records, r => Assert.False(string.IsNullOrEmpty(r.SourceUrl)));
    }

    [Fact]
    public async Task FetchRecords_NullUrls_Excluded()
    {
        var service = CreateService();
        var records = await service.FetchRecordsAsync();

        Assert.DoesNotContain(records, r => r.SourceUrl == "https://example.com/null");
    }

    [Fact]
    public async Task FetchRecords_EmptyUrls_Excluded()
    {
        var service = CreateService();
        var records = await service.FetchRecordsAsync();

        Assert.All(records, r => Assert.False(string.IsNullOrWhiteSpace(r.SourceUrl)));
    }

    [Fact]
    public async Task FetchRecords_EmptyTable_ReturnsEmptyList()
    {
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, "CREATE TABLE EmptyTable (Id BIGINT, Url NVARCHAR(2048))");
        var config = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "EmptyTable", UrlColumn = "Url", IdColumn = "Id" }
            }
        };
        var service = new SourceDbService(config);

        var records = await service.FetchRecordsAsync();
        Assert.Empty(records);
    }

    [Fact]
    public async Task FetchRecords_SetsSourceTable()
    {
        var service = CreateService();
        var records = await service.FetchRecordsAsync();

        Assert.All(records, r => Assert.Equal("TestMedia", r.SourceTable));
    }

    [Fact]
    public async Task FetchRecords_InvalidTableName_ThrowsException()
    {
        var config = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "DROP TABLE;--", UrlColumn = "Url", IdColumn = "Id" }
            }
        };
        var service = new SourceDbService(config);
        await Assert.ThrowsAsync<ArgumentException>(() => service.FetchRecordsAsync());
    }
}
