using MFSS.Models;
using MFSS.Services;

namespace MFSS.Tests;

public class ConfigValidatorTests
{
    private static MigrationSettings ValidSettings() => new()
    {
        Name = "test",
        Mode = "migrate",
        ParallelDownloads = 4,
        MaxRetries = 3,
        RateLimitPerSecond = 10,
        MaxFileSizeMB = 100
    };

    private static SourceDbConfig ValidSourceDb() => new()
    {
        ConnectionString = "Server=localhost;Database=test;User=root;******;",
        Tables = new List<SourceTableConfig>
        {
            new() { TableName = "products", UrlColumn = "image_url", IdColumn = "id" }
        }
    };

    private static FileSystemConfig ValidSrcFs() => new() { Type = "http" };
    private static FileSystemConfig ValidDestFs() => new()
    {
        Type = "s3", BucketName = "bucket", Region = "us-east-1",
        AccessKey = "key", SecretKey = "secret"
    };
    private static DestinationDbConfig ValidDestDb() => new()
    {
        ConnectionString = "Server=localhost;Database=logs;User=root;******;"
    };
    private static ThirdDbConfig ValidThirdDb() => new() { Enabled = false };

    [Fact]
    public void Validate_AllValid_ReturnsEmptyList()
    {
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), ValidThirdDb());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidMode_ReturnsError()
    {
        var settings = ValidSettings();
        settings.Mode = "invalid";
        var errors = ConfigValidator.Validate(
            settings, ValidSourceDb(), ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("Mode"));
    }

    [Fact]
    public void Validate_NoSourceTables_ReturnsError()
    {
        var sourceDb = new SourceDbConfig
        {
            ConnectionString = "Server=localhost;",
            Tables = new List<SourceTableConfig>()
        };
        var errors = ConfigValidator.Validate(
            ValidSettings(), sourceDb, ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("at least one"));
    }

    [Fact]
    public void Validate_S3MissingBucket_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "s3", Region = "us-east-1", AccessKey = "k", SecretKey = "s" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("BucketName"));
    }

    [Fact]
    public void Validate_LocalMissingBasePath_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "local" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("BasePath"));
    }

    [Fact]
    public void Validate_ThirdDbEnabledMissingQuery_ReturnsError()
    {
        var thirdDb = new ThirdDbConfig
        {
            Enabled = true,
            ConnectionString = "Server=localhost;",
            UpdateQuery = ""
        };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), thirdDb);
        Assert.Contains(errors, e => e.Contains("UpdateQuery"));
    }

    [Fact]
    public void Validate_ThirdDbQueryMissingPlaceholders_ReturnsError()
    {
        var thirdDb = new ThirdDbConfig
        {
            Enabled = true,
            ConnectionString = "Server=localhost;",
            UpdateQuery = "UPDATE table SET col = 'value'"
        };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), thirdDb);
        Assert.Contains(errors, e => e.Contains("{id}") || e.Contains("{url}"));
    }

    [Fact]
    public void Validate_ParallelDownloadsOutOfRange_ReturnsError()
    {
        var settings = ValidSettings();
        settings.ParallelDownloads = 0;
        var errors = ConfigValidator.Validate(
            settings, ValidSourceDb(), ValidSrcFs(),
            ValidDestFs(), ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("ParallelDownloads"));
    }

    [Fact]
    public void Validate_UnsupportedDestType_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "ftp" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("not supported"));
    }

    [Fact]
    public void Validate_AzureMissingConnectionString_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "azure", ContainerName = "my-container" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("AzureConnectionString"));
    }

    [Fact]
    public void Validate_AzureMissingContainer_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "azure", AzureConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("ContainerName"));
    }

    [Fact]
    public void Validate_GcsMissingBucket_ReturnsError()
    {
        var destFs = new FileSystemConfig { Type = "gcs" };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Contains(errors, e => e.Contains("GcsBucket"));
    }

    [Fact]
    public void Validate_AzureValid_ReturnsNoError()
    {
        var destFs = new FileSystemConfig
        {
            Type = "azure",
            AzureConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;",
            ContainerName = "my-container"
        };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_GcsValid_ReturnsNoError()
    {
        var destFs = new FileSystemConfig
        {
            Type = "gcs",
            GcsBucket = "my-bucket"
        };
        var errors = ConfigValidator.Validate(
            ValidSettings(), ValidSourceDb(), ValidSrcFs(),
            destFs, ValidDestDb(), ValidThirdDb());
        Assert.Empty(errors);
    }
}
