using MFSS.Models;

namespace MFSS.Services;

public class FileTransferService : IDisposable
{
    private readonly FileSystemConfig _source;
    private readonly FileSystemConfig _destination;
    private readonly int _rateLimitPerSecond;
    private readonly int _maxFileSizeMB;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;

    public FileTransferService(FileSystemConfig source, FileSystemConfig destination, int rateLimitPerSecond, int maxFileSizeMB)
    {
        _source = source;
        _destination = destination;
        _rateLimitPerSecond = rateLimitPerSecond;
        _maxFileSizeMB = maxFileSizeMB;
        _httpClient = new HttpClient();
        _rateLimiter = new SemaphoreSlim(rateLimitPerSecond, rateLimitPerSecond);
    }

    public async Task<(string NewUrl, long Size, string Hash)> TransferAsync(string sourceUrl)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            // Download from source
            var response = await _httpClient.GetAsync(sourceUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync();

            // Check file size
            if (content.Length > _maxFileSizeMB * 1024 * 1024)
                throw new InvalidOperationException($"File exceeds max size of {_maxFileSizeMB}MB");

            // Compute hash
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

            // Generate destination path
            var fileName = Path.GetFileName(new Uri(sourceUrl).AbsolutePath);
            if (string.IsNullOrEmpty(fileName)) fileName = $"{hash[..16]}.bin";
            var destPath = $"{DateTime.UtcNow:yyyy/MM/dd}/{hash[..8]}_{fileName}";

            // Upload to destination (simplified - would use S3/Azure SDK in production)
            var newUrl = await UploadToDestination(content, destPath);

            return (newUrl, content.Length, hash);
        }
        finally
        {
            _ = Task.Delay(1000 / _rateLimitPerSecond).ContinueWith(_ => _rateLimiter.Release());
        }
    }

    public async Task DeleteAsync(string url)
    {
        // Placeholder for delete logic based on destination type
        await Task.CompletedTask;
        throw new NotImplementedException("Delete based on destination file system type");
    }

    private async Task<string> UploadToDestination(byte[] content, string destPath)
    {
        if (_destination.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(_destination.BasePath, destPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, content);
            return fullPath;
        }

        // S3/Azure would be handled here
        return $"https://{_destination.BucketName}.s3.{_destination.Region}.amazonaws.com/{destPath}";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimiter.Dispose();
    }
}
