using MFSS.Models;
using MFSS.StorageProviders;

namespace MFSS.Tests;

public class StorageProviderFactoryTests
{
    [Fact]
    public void CreateSourceProvider_Http_ReturnsHttpStorageProvider()
    {
        using var factory = new StorageProviderFactory();
        var provider = factory.CreateSourceProvider(new FileSystemConfig { Type = "http" });
        Assert.IsType<HttpStorageProvider>(provider);
    }

    [Fact]
    public void CreateSourceProvider_Local_ReturnsLocalStorageProvider()
    {
        using var factory = new StorageProviderFactory();
        var provider = factory.CreateSourceProvider(new FileSystemConfig { Type = "local", BasePath = "C:\\temp" });
        Assert.IsType<LocalStorageProvider>(provider);
    }

    [Fact]
    public void CreateDestinationProvider_Local_ReturnsLocalStorageProvider()
    {
        using var factory = new StorageProviderFactory();
        var provider = factory.CreateDestinationProvider(new FileSystemConfig { Type = "local", BasePath = "C:\\temp" });
        Assert.IsType<LocalStorageProvider>(provider);
    }

    [Fact]
    public void CreateSourceProvider_UnsupportedType_Throws()
    {
        using var factory = new StorageProviderFactory();
        Assert.Throws<NotSupportedException>(() =>
            factory.CreateSourceProvider(new FileSystemConfig { Type = "ftp" }));
    }

    [Fact]
    public void CreateDestinationProvider_UnsupportedType_Throws()
    {
        using var factory = new StorageProviderFactory();
        Assert.Throws<NotSupportedException>(() =>
            factory.CreateDestinationProvider(new FileSystemConfig { Type = "http" }));
    }

    [Fact]
    public void CreateSourceProvider_S3_ReturnsS3StorageProvider()
    {
        using var factory = new StorageProviderFactory();
        var provider = factory.CreateSourceProvider(new FileSystemConfig
        {
            Type = "s3",
            BucketName = "bucket",
            Region = "us-east-1",
            AccessKey = "key",
            SecretKey = "secret"
        });
        Assert.IsType<S3StorageProvider>(provider);
    }

    [Fact]
    public void CreateDestinationProvider_S3_ReturnsS3StorageProvider()
    {
        using var factory = new StorageProviderFactory();
        var provider = factory.CreateDestinationProvider(new FileSystemConfig
        {
            Type = "s3",
            BucketName = "bucket",
            Region = "us-east-1",
            AccessKey = "key",
            SecretKey = "secret"
        });
        Assert.IsType<S3StorageProvider>(provider);
    }

    [Fact]
    public void CreateSourceProvider_Azure_ReturnsAzureStorageProvider()
    {
        var config = new FileSystemConfig
        {
            Type = "azure",
            AzureConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=dGhpcyBpcyBhIHRlc3Q=;EndpointSuffix=core.windows.net",
            ContainerName = "container"
        };
        var provider = new AzureStorageProvider(config);
        Assert.IsType<AzureStorageProvider>(provider);
    }
}
