using MFSS.Abstractions;
using MFSS.Models;
using MFSS.Orchestration;
using MFSS.Services;
using MFSS.StorageProviders;
using MFSS.Tests.Helpers;
using Moq;

namespace MFSS.Tests;

[Trait("Category", "Integration")]
public class OrchestratorAcceptanceTests : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly string _sourceDir;
    private readonly string _destDir;

    public OrchestratorAcceptanceTests()
    {
        _connectionString = TestDatabaseHelper.CreateTestDatabase("OrchAcc");
        _sourceDir = Path.Combine(Path.GetTempPath(), "mfss_orch_src_" + Guid.NewGuid().ToString("N"));
        _destDir = Path.Combine(Path.GetTempPath(), "mfss_orch_dest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    public async ValueTask DisposeAsync()
    {
        TestDatabaseHelper.DropTestDatabase(_connectionString);
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, true);
        if (Directory.Exists(_destDir)) Directory.Delete(_destDir, true);
        await ValueTask.CompletedTask;
    }

    [Fact]
    public async Task FullMigration_LocalToLocal_Success()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "photo.jpg"), "fake photo data");
        File.WriteAllText(Path.Combine(_sourceDir, "doc.pdf"), "fake pdf data");

        var settings = new MigrationSettings
        {
            Name = "acc-test",
            Mode = MigrationStatus.MigrateMode,
            DryRun = false,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var sourceDb = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "MediaFiles", UrlColumn = "FilePath", IdColumn = "MediaId" }
            }
        };

        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            CREATE TABLE MediaFiles (
                MediaId BIGINT PRIMARY KEY,
                FilePath NVARCHAR(2048) NULL
            )");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            INSERT INTO MediaFiles (MediaId, FilePath) VALUES
            (1, 'photo.jpg'),
            (2, 'doc.pdf')");

        var srcFs = new FileSystemConfig { Type = "local", BasePath = _sourceDir };
        var destFs = new FileSystemConfig { Type = "local", BasePath = _destDir };
        var destDb = new DestinationDbConfig
        {
            ConnectionString = _connectionString,
            SeparateTablesPerSource = false
        };

        var orchestrator = new MigrationOrchestrator(
            settings, sourceDb, srcFs, destFs, destDb, new ThirdDbConfig(),
            new EnvConfigResolver(), new ConfigValidator(),
            new SourceDbService(sourceDb),
            new DestinationDbService(destDb));

        var exitCode = await orchestrator.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal(2, Directory.GetFiles(_destDir, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task FullMigration_DryRun_NoFilesTransferred()
    {
        var settings = new MigrationSettings
        {
            Name = "acc-dry",
            Mode = MigrationStatus.MigrateMode,
            DryRun = true,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var sourceDb = new SourceDbConfig
        {
            ConnectionString = _connectionString,
            Tables = new List<SourceTableConfig>
            {
                new() { TableName = "MediaFiles", UrlColumn = "FilePath", IdColumn = "MediaId" }
            }
        };

        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            CREATE TABLE MediaFiles (
                MediaId BIGINT PRIMARY KEY,
                FilePath NVARCHAR(2048) NULL
            )");
        TestDatabaseHelper.ExecuteNonQuery(_connectionString, @"
            INSERT INTO MediaFiles (MediaId, FilePath) VALUES
            (1, 'nofile.jpg')");

        var orchestrator = new MigrationOrchestrator(
            settings, sourceDb,
            new FileSystemConfig { Type = "local", BasePath = _sourceDir },
            new FileSystemConfig { Type = "local", BasePath = _destDir },
            new DestinationDbConfig { ConnectionString = _connectionString },
            new ThirdDbConfig(),
            new EnvConfigResolver(), new ConfigValidator(),
            new SourceDbService(sourceDb),
            new DestinationDbService(new DestinationDbConfig { ConnectionString = _connectionString }));

        var exitCode = await orchestrator.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.Empty(Directory.GetFiles(_destDir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Rollback_RemovesFiles()
    {
        var testFile = Path.Combine(_destDir, "rollback_me.txt");
        File.WriteAllText(testFile, "to be deleted");

        var settings = new MigrationSettings
        {
            Name = "acc-roll",
            Mode = MigrationStatus.RollbackMode,
            DryRun = false,
            ParallelDownloads = 2,
            MaxRetries = 2,
            RateLimitPerSecond = 10,
            MaxFileSizeMB = 100
        };

        var destDbConfig = new DestinationDbConfig
        {
            ConnectionString = _connectionString,
            SeparateTablesPerSource = false
        };

        var destService = new DestinationDbService(destDbConfig);
        await destService.CreateAllTablesAsync(new List<SourceTableConfig>());
        await destService.InsertBatchAsync(new List<MediaRecord>
        {
            new() { Id = 1, SourceTable = "TestTable", SourceUrl = "http://source/test.jpg" }
        });
        await destService.MarkSuccessAsync(1, "TestTable", testFile, 100, "hash1");

        var validatorMock = new Mock<IConfigValidator>();
        validatorMock.Setup(v => v.Validate(
            It.IsAny<MigrationSettings>(), It.IsAny<SourceDbConfig>(),
            It.IsAny<FileSystemConfig>(), It.IsAny<FileSystemConfig>(),
            It.IsAny<DestinationDbConfig>(), It.IsAny<ThirdDbConfig>()))
            .Returns(new List<string>());

        var orchestrator = new MigrationOrchestrator(
            settings,
            new SourceDbConfig(),
            new FileSystemConfig { Type = "local" },
            new FileSystemConfig { Type = "local", BasePath = _destDir },
            destDbConfig,
            new ThirdDbConfig(),
            new EnvConfigResolver(), validatorMock.Object,
            destService: destService);

        var exitCode = await orchestrator.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(testFile));
    }

    [Fact]
    public async Task InvalidConfig_ReturnsExitCode1()
    {
        var settings = new MigrationSettings { Name = "", Mode = "invalid" };

        var orchestrator = new MigrationOrchestrator(
            settings,
            new SourceDbConfig(),
            new FileSystemConfig(),
            new FileSystemConfig(),
            new DestinationDbConfig(),
            new ThirdDbConfig(),
            new EnvConfigResolver(), new ConfigValidator());

        var exitCode = await orchestrator.RunAsync();

        Assert.Equal(1, exitCode);
    }
}
