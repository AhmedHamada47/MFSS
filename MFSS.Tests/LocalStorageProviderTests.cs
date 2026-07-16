using MFSS.Models;
using MFSS.StorageProviders;

namespace MFSS.Tests;

public class LocalStorageProviderTests : IDisposable
{
    private readonly string _basePath;

    public LocalStorageProviderTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "mfss_provider_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, true);
    }

    [Fact]
    public async Task DownloadAsync_ExistingFile_ReturnsStream()
    {
        var filePath = Path.Combine(_basePath, "test.txt");
        await File.WriteAllTextAsync(filePath, "hello");

        var config = new FileSystemConfig { BasePath = _basePath };
        var provider = new LocalStorageProvider(config);

        using var stream = await provider.DownloadAsync("test.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("hello", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DownloadAsync_AbsolutePath_PassesThrough()
    {
        var filePath = Path.Combine(_basePath, "abs.txt");
        await File.WriteAllTextAsync(filePath, "absolute");

        var provider = new LocalStorageProvider(new FileSystemConfig());
        using var stream = await provider.DownloadAsync(filePath);
        using var reader = new StreamReader(stream);
        Assert.Equal("absolute", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task UploadAsync_CreatesFileWithContent()
    {
        var config = new FileSystemConfig { BasePath = _basePath };
        var provider = new LocalStorageProvider(config);

        using var content = new MemoryStream("uploaded content"u8.ToArray());
        var result = await provider.UploadAsync(content, "sub/dest.txt");

        Assert.True(File.Exists(result));
        Assert.Equal("uploaded content", await File.ReadAllTextAsync(result));
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesFile()
    {
        var filePath = Path.Combine(_basePath, "delete_me.txt");
        await File.WriteAllTextAsync(filePath, "delete me");

        var config = new FileSystemConfig { BasePath = _basePath };
        var provider = new LocalStorageProvider(config);
        await provider.DeleteAsync(filePath);

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task DeleteAsync_NonexistentFile_DoesNotThrow()
    {
        var provider = new LocalStorageProvider(new FileSystemConfig());
        await provider.DeleteAsync(Path.Combine(_basePath, "nonexistent.txt"));
    }
}
