using Amazon.S3;
using Amazon.S3.Model;
using MFSS.Models;

namespace MFSS.Services;

/// <summary>
/// Handles file transfer from HTTP sources to local/S3 destinations.
/// Implements proper token-bucket rate limiting and supports cancellation.
/// </summary>
public class FileTransferService : IDisposable
{
    private readonly FileSystemConfig _source;
    private readonly FileSystemConfig _destination;
    private readonly int _rateLimitPerSecond;
    private readonly int _maxFileSizeMB;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly Timer _rateLimiterRefill;
    private readonly AmazonS3Client? _s3Client;

    public FileTransferService(FileSystemConfig source, FileSystemConfig destination, int rateLimitPerSecond, int maxFileSizeMB)
    {
        _source = source;
        _destination = destination;
        _rateLimitPerSecond = rateLimitPerSecond;
        _maxFileSizeMB = maxFileSizeMB;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _rateLimiter = new SemaphoreSlim(rateLimitPerSecond, rateLimitPerSecond);

        // Token-bucket refill: release one token every (1000/rate) ms
        _rateLimiterRefill = new Timer(_ =>
        {
            try
            {
                if (_rateLimiter.CurrentCount < _rateLimitPerSecond)
                    _rateLimiter.Release();
            }
            catch (ObjectDisposedException)
            {
                // Timer fired after disposal — safe to ignore
            }
        }, null, 0, Math.Max(1, 1000 / _rateLimitPerSecond));

        // Initialize S3 client if destination is S3
        if (_destination.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_destination.Region)
            };
            _s3Client = new AmazonS3Client(_destination.AccessKey, _destination.SecretKey, s3Config);
        }
    }

    /// <summary>
    /// Downloads a file from source URL and uploads it to the configured destination.
    /// Returns the new URL, file size, and SHA-256 hash.
    /// </summary>
    public async Task<(string NewUrl, long Size, string Hash)> TransferAsync(string sourceUrl, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            // Download from source
            using var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync(ct);

            // Check file size
            if (content.Length > (long)_maxFileSizeMB * 1024 * 1024)
                throw new InvalidOperationException($"File exceeds max size of {_maxFileSizeMB}MB (actual: {content.Length / (1024.0 * 1024.0):F2}MB)");

            // Compute SHA-256 hash
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

            // Generate destination path with date-based partitioning
            var fileName = GetFileName(sourceUrl, hash);
            var destPath = $"{DateTime.UtcNow:yyyy/MM/dd}/{hash[..8]}_{fileName}";

            // Upload to destination
            var newUrl = await UploadToDestination(content, destPath, ct);

            return (newUrl, content.Length, hash);
        }
        finally
        {
            // Token is not released here - the timer handles refill (token bucket pattern)
        }
    }

    /// <summary>
    /// Deletes a file from the destination storage.
    /// </summary>
    public async Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        if (_destination.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            if (_s3Client == null) throw new InvalidOperationException("S3 client not initialized");

            var key = ExtractS3Key(destinationUrl);
            var request = new DeleteObjectRequest
            {
                BucketName = _destination.BucketName,
                Key = key
            };
            await _s3Client.DeleteObjectAsync(request, ct);
        }
        else if (_destination.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(destinationUrl))
                File.Delete(destinationUrl);
        }
        else
        {
            throw new NotSupportedException($"Delete not supported for destination type: {_destination.Type}");
        }
    }

    private async Task<string> UploadToDestination(byte[] content, string destPath, CancellationToken ct)
    {
        if (_destination.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.Combine(_destination.BasePath, destPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllBytesAsync(fullPath, content, ct);
            return fullPath;
        }
        else if (_destination.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            if (_s3Client == null) throw new InvalidOperationException("S3 client not initialized");

            using var stream = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _destination.BucketName,
                Key = destPath,
                InputStream = stream,
                ContentType = GetContentType(destPath),
                AutoCloseStream = false
            };

            await _s3Client.PutObjectAsync(request, ct);
            return $"https://{_destination.BucketName}.s3.{_destination.Region}.amazonaws.com/{destPath}";
        }
        else
        {
            throw new NotSupportedException($"Destination type '{_destination.Type}' is not supported. Use 'local' or 's3'.");
        }
    }

    private static string GetFileName(string sourceUrl, string hash)
    {
        try
        {
            var fileName = Path.GetFileName(new Uri(sourceUrl).AbsolutePath);
            if (!string.IsNullOrEmpty(fileName) && fileName.Contains('.'))
                return fileName;
        }
        catch { }
        return $"{hash[..16]}.bin";
    }

    private static string ExtractS3Key(string url)
    {
        // Extract key from S3 URL: https://bucket.s3.region.amazonaws.com/key
        var uri = new Uri(url);
        return uri.AbsolutePath.TrimStart('/');
    }

    private static string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".doc" or ".docx" => "application/msword",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _rateLimiter.Dispose();
        _rateLimiterRefill.Dispose();
        _s3Client?.Dispose();
    }
}
