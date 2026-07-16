using MFSS.Models;

namespace MFSS.StorageProviders;

public class LocalStorageProvider : IStorageProvider
{
    private readonly FileSystemConfig _config;

    public LocalStorageProvider(FileSystemConfig config)
    {
        _config = config;
    }

    public Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default)
    {
        var filePath = Path.IsPathRooted(sourceUrl)
            ? sourceUrl
            : Path.Combine(_config.BasePath, sourceUrl);
        return Task.FromResult<Stream>(File.OpenRead(filePath));
    }

    public async Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_config.BasePath, destPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);
        return fullPath;
    }

    public Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        if (File.Exists(destinationUrl))
            File.Delete(destinationUrl);
        return Task.CompletedTask;
    }
}
