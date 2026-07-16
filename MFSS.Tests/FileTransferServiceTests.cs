using MFSS.Models;
using MFSS.Services;
using MFSS.StorageProviders;

namespace MFSS.Tests;

public class FileTransferServiceTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;

    public FileTransferServiceTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), "mfss_test_src_" + Guid.NewGuid().ToString("N"));
        _destDir = Path.Combine(Path.GetTempPath(), "mfss_test_dest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, true);
        if (Directory.Exists(_destDir)) Directory.Delete(_destDir, true);
    }

    private FileTransferService CreateService()
    {
        var srcFs = new FileSystemConfig { Type = "local", BasePath = _sourceDir };
        var destFs = new FileSystemConfig { Type = "local", BasePath = _destDir };
        var srcProvider = new LocalStorageProvider(srcFs);
        var destProvider = new LocalStorageProvider(destFs);
        return new FileTransferService(srcProvider, destProvider, 10, 100);
    }

    [Fact]
    public async Task TransferAsync_LocalToLocal_TransfersFile()
    {
        var testFile = Path.Combine(_sourceDir, "test.txt");
        var content = "Hello, MFSS!";
        await File.WriteAllTextAsync(testFile, content);

        using var service = CreateService();
        var (newUrl, size, hash) = await service.TransferAsync("test.txt");

        Assert.True(File.Exists(newUrl));
        Assert.Equal(content.Length, size);
        Assert.NotEmpty(hash);
    }

    [Fact]
    public async Task TransferAsync_LocalToLocal_PreservesContent()
    {
        var testFile = Path.Combine(_sourceDir, "data.bin");
        var content = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        await File.WriteAllBytesAsync(testFile, content);

        using var service = CreateService();
        var (newUrl, _, _) = await service.TransferAsync("data.bin");

        var transferredContent = await File.ReadAllBytesAsync(newUrl);
        Assert.Equal(content, transferredContent);
    }

    [Fact]
    public async Task TransferAsync_LocalToLocal_ComputesCorrectHash()
    {
        var testFile = Path.Combine(_sourceDir, "hash_test.txt");
        var content = "Hash me!";
        await File.WriteAllTextAsync(testFile, content);

        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        using var service = CreateService();
        var (_, _, hash) = await service.TransferAsync("hash_test.txt");

        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public async Task TransferAsync_LocalToLocal_ReturnsCorrectSize()
    {
        var testFile = Path.Combine(_sourceDir, "size_test.txt");
        var content = "12345";
        await File.WriteAllTextAsync(testFile, content);

        using var service = CreateService();
        var (_, size, _) = await service.TransferAsync("size_test.txt");

        Assert.Equal(5, size);
    }

    [Fact]
    public async Task TransferAsync_LocalToLocal_CreatesDatePartitionedPath()
    {
        var testFile = Path.Combine(_sourceDir, "partition_test.jpg");
        await File.WriteAllBytesAsync(testFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        using var service = CreateService();
        var (newUrl, _, _) = await service.TransferAsync("partition_test.jpg");

        Assert.Contains(DateTime.UtcNow.ToString("yyyy/MM/dd"), newUrl);
        Assert.StartsWith(_destDir, newUrl);
    }

    [Fact]
    public async Task TransferAsync_ExceedsMaxSize_ThrowsException()
    {
        var testFile = Path.Combine(_sourceDir, "large.bin");
        var content = new byte[1024];
        await File.WriteAllBytesAsync(testFile, content);

        var srcFs = new FileSystemConfig { Type = "local", BasePath = _sourceDir };
        var destFs = new FileSystemConfig { Type = "local", BasePath = _destDir };
        var srcProvider = new LocalStorageProvider(srcFs);
        var destProvider = new LocalStorageProvider(destFs);

        using var service = new FileTransferService(srcProvider, destProvider, 10, 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferAsync("large.bin"));
    }

    [Fact]
    public async Task DeleteAsync_Local_DeletesFile()
    {
        var testFile = Path.Combine(_sourceDir, "to_delete.txt");
        await File.WriteAllTextAsync(testFile, "delete me");

        using var service = CreateService();
        var (newUrl, _, _) = await service.TransferAsync("to_delete.txt");

        Assert.True(File.Exists(newUrl));
        await service.DeleteAsync(newUrl);
        Assert.False(File.Exists(newUrl));
    }

    [Fact]
    public async Task DeleteAsync_Local_NonexistentFile_DoesNotThrow()
    {
        var srcFs = new FileSystemConfig { Type = "local", BasePath = _sourceDir };
        var destFs = new FileSystemConfig { Type = "local", BasePath = _destDir };
        var srcProvider = new LocalStorageProvider(srcFs);
        var destProvider = new LocalStorageProvider(destFs);

        using var service = new FileTransferService(srcProvider, destProvider, 10, 100);
        await service.DeleteAsync(Path.Combine(_destDir, "nonexistent.txt"));
    }

    [Fact]
    public async Task TransferAsync_MultipleFiles_AllSucceed()
    {
        for (int i = 0; i < 5; i++)
            await File.WriteAllTextAsync(Path.Combine(_sourceDir, $"file{i}.txt"), $"content{i}");

        using var service = CreateService();
        for (int i = 0; i < 5; i++)
        {
            var (newUrl, size, hash) = await service.TransferAsync($"file{i}.txt");
            Assert.True(File.Exists(newUrl));
            Assert.Equal($"content{i}".Length, size);
            Assert.NotEmpty(hash);
        }
    }

    [Theory]
    [InlineData("https://example.com/image.jpg", "image.jpg")]
    [InlineData("https://example.com/image.jpg?w=100", "image.jpg")]
    [InlineData("https://example.com/path/to/file.png", "file.png")]
    public void GetFileName_ValidUrl_ReturnsFileName(string url, string expected)
    {
        var result = StorageProviderExtensions.GetFileName(url, "abcdef0123456789abcdef0123456789");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://example.com/image", "abcdef0123456789.bin")]
    [InlineData("not-a-url", "abcdef0123456789.bin")]
    public void GetFileName_NoExtension_ReturnsHashFallback(string url, string expected)
    {
        var hash = "abcdef0123456789abcdef0123456789";
        var result = StorageProviderExtensions.GetFileName(url, hash);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExtractS3Key_ValidUrl_ReturnsKey()
    {
        var result = S3StorageProvider.ExtractS3Key("https://bucket.s3.amazonaws.com/path/to/file.jpg");
        Assert.Equal("path/to/file.jpg", result);
    }

    [Fact]
    public void ExtractAzureBlobName_ValidUrl_ReturnsBlobName()
    {
        var result = AzureStorageProvider.ExtractAzureBlobName("https://account.blob.core.windows.net/container/path/file.jpg");
        Assert.Equal("path/file.jpg", result);
    }

    [Fact]
    public void ExtractGcsObjectName_ValidUrl_ReturnsObjectName()
    {
        var result = GcsStorageProvider.ExtractGcsObjectName("https://storage.googleapis.com/bucket/path/to/file.jpg");
        Assert.Equal("path/to/file.jpg", result);
    }

    [Theory]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".png", "image/png")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".mp4", "video/mp4")]
    [InlineData(".mp3", "audio/mpeg")]
    [InlineData(".json", "application/json")]
    [InlineData(".xml", "application/xml")]
    [InlineData(".zip", "application/zip")]
    [InlineData(".doc", "application/msword")]
    [InlineData(".docx", "application/msword")]
    [InlineData(".txt", "application/octet-stream")]
    public void GetContentType_ReturnsCorrectType(string extension, string expectedContentType)
    {
        var result = MimeTypeMap.GetContentType($"file{extension}");
        Assert.Equal(expectedContentType, result);
    }
}
