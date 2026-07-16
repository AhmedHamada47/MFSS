using MFSS.Abstractions;
using MFSS.Models;
using MFSS.Orchestration;
using MFSS.Services;
using Moq;
using static MFSS.Models.MigrationStatus;

namespace MFSS.Tests;

public class MigrationOrchestratorTests
{
    private readonly Mock<ISourceDbService> _srcMock;
    private readonly Mock<IDestinationDbService> _destMock;
    private readonly Mock<IEnvConfigResolver> _envMock;
    private readonly Mock<IConfigValidator> _validatorMock;

    public MigrationOrchestratorTests()
    {
        _srcMock = new Mock<ISourceDbService>();
        _destMock = new Mock<IDestinationDbService>();
        _envMock = new Mock<IEnvConfigResolver>();
        _validatorMock = new Mock<IConfigValidator>();
    }

    private MigrationOrchestrator CreateOrchestrator(
        MigrationSettings? settings = null,
        bool verbose = false)
    {
        settings ??= new MigrationSettings
        {
            Name = "test",
            Mode = MigrateMode,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var sourceDb = new SourceDbConfig
        {
            ConnectionString = "Server=localhost;Database=test;",
            Tables = new List<SourceTableConfig> { new() { TableName = "Items", UrlColumn = "url", IdColumn = "id" } }
        };

        var srcFs = new FileSystemConfig { Type = "http" };
        var destFs = new FileSystemConfig { Type = "local", BasePath = "C:\\temp" };
        var destDb = new DestinationDbConfig
        {
            ConnectionString = "Server=localhost;Database=logs;",
            SeparateTablesPerSource = false
        };
        var thirdDb = new ThirdDbConfig { Enabled = false };

        return new MigrationOrchestrator(
            settings, sourceDb, srcFs, destFs, destDb, thirdDb,
            _envMock.Object, _validatorMock.Object,
            _srcMock.Object, _destMock.Object, verbose);
    }

    [Fact]
    public async Task RunAsync_ConfigValidationFails_Returns1()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string> { "Invalid config" });

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.RunAsync();

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task RunAsync_DryRun_Returns0()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        _destMock.Setup(d => d.AllTablesExistAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(true);

        _srcMock.Setup(s => s.FetchRecordsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaRecord>
            {
                new() { Id = 1, SourceTable = "Items", SourceUrl = "https://example.com/1.jpg" }
            });

        var settings = new MigrationSettings
        {
            Name = "test",
            Mode = MigrateMode,
            DryRun = true,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var orchestrator = CreateOrchestrator(settings);
        var result = await orchestrator.RunAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunAsync_NoRecords_Returns0()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        _destMock.Setup(d => d.AllTablesExistAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(true);

        _srcMock.Setup(s => s.FetchRecordsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaRecord>());

        var orchestrator = CreateOrchestrator();
        var result = await orchestrator.RunAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunAsync_RollbackMode_Returns0()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        _destMock.Setup(d => d.AllTablesExistAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(true);

        _destMock.Setup(d => d.GetSuccessRecordsAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(new List<(long Id, string Url)>());

        var settings = new MigrationSettings
        {
            Name = "test",
            Mode = RollbackMode,
            DryRun = true,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var orchestrator = CreateOrchestrator(settings);
        var result = await orchestrator.RunAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task RunAsync_FreshStart_CreatesTables()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        _destMock.Setup(d => d.AllTablesExistAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(true);

        _destMock.Setup(d => d.GetAllLogTableNamesAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(new List<string> { "MigrationLog" });

        _srcMock.Setup(s => s.FetchRecordsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaRecord>
            {
                new() { Id = 1, SourceTable = "Items", SourceUrl = "https://example.com/1.jpg" }
            });

        _destMock.Setup(d => d.InsertBatchAsync(It.IsAny<List<MediaRecord>>()))
            .ReturnsAsync(1);

        _destMock.Setup(d => d.DetectAndResetUrlChangesAsync(It.IsAny<List<MediaRecord>>()))
            .ReturnsAsync(0);

        _destMock.Setup(d => d.GetAllPendingRecordsAsync(
            It.IsAny<List<SourceTableConfig>>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MediaRecord>
            {
                new() { Id = 1, SourceTable = "Items", SourceUrl = "https://example.com/1.jpg" }
            });

        _destMock.Setup(d => d.GetSummaryAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(new Dictionary<string, int> { { "success", 0 }, { "failed", 0 } });

        var settings = new MigrationSettings
        {
            Name = "test",
            Mode = MigrateMode,
            FreshStart = true,
            DryRun = false,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var orchestrator = CreateOrchestrator(settings);
        var result = await orchestrator.RunAsync();

        _destMock.Verify(d => d.DropAllTablesAsync(It.IsAny<List<SourceTableConfig>>()), Times.Once);
        _destMock.Verify(d => d.CreateAllTablesAsync(It.IsAny<List<SourceTableConfig>>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_TableCreationNeeded_Creates()
    {
        _validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        _destMock.Setup(d => d.AllTablesExistAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(false);

        _destMock.Setup(d => d.GetAllLogTableNamesAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(new List<string> { "MigrationLog" });

        _srcMock.Setup(s => s.FetchRecordsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaRecord>
            {
                new() { Id = 1, SourceTable = "Items", SourceUrl = "https://example.com/1.jpg" }
            });

        _destMock.Setup(d => d.InsertBatchAsync(It.IsAny<List<MediaRecord>>()))
            .ReturnsAsync(1);

        _destMock.Setup(d => d.DetectAndResetUrlChangesAsync(It.IsAny<List<MediaRecord>>()))
            .ReturnsAsync(0);

        _destMock.Setup(d => d.GetAllPendingRecordsAsync(
            It.IsAny<List<SourceTableConfig>>(), It.IsAny<int>()))
            .ReturnsAsync(new List<MediaRecord>
            {
                new() { Id = 1, SourceTable = "Items", SourceUrl = "https://example.com/1.jpg" }
            });

        _destMock.Setup(d => d.FindExistingByHashAsync(
            It.IsAny<string>(), It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(((long Id, string? DestinationUrl, long? FileSize)?)null);

        _destMock.Setup(d => d.MarkSuccessAsync(
            It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<long>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _destMock.Setup(d => d.GetSummaryAsync(It.IsAny<List<SourceTableConfig>>()))
            .ReturnsAsync(new Dictionary<string, int> { { "success", 1 }, { "failed", 0 } });

        var settings = new MigrationSettings
        {
            Name = "test",
            Mode = MigrateMode,
            DryRun = false,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var orchestrator = CreateOrchestrator(settings);
        var result = await orchestrator.RunAsync();

        _destMock.Verify(d => d.CreateAllTablesAsync(It.IsAny<List<SourceTableConfig>>()), Times.Once);
    }
}
