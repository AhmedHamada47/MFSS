using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using MFSS.Models;

namespace MFSS.Services;

/// <summary>
/// Handles file transfer between any combination of storage providers:
/// HTTP, Local, S3, Azure Blob Storage, and Google Cloud Storage.
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
    private readonly AmazonS3Client? _s3SourceClient;
    private readonly AmazonS3Client? _s3DestClient;
    private readonly BlobContainerClient? _azureSourceClient;
    private readonly BlobContainerClient? _azureDestClient;
    private readonly StorageClient? _gcsSourceClient;
    private readonly StorageClient? _gcsDestClient;

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

        // Initialize source clients
        if (_source.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_source.Region)
            };
            _s3SourceClient = new AmazonS3Client(_source.AccessKey, _source.SecretKey, s3Config);
        }
        else if (_source.Type.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            _azureSourceClient = new BlobContainerClient(_source.AzureConnectionString, _source.ContainerName);
        }
        else if (_source.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase))
        {
            _gcsSourceClient = string.IsNullOrWhiteSpace(_source.GcsCredentialPath)
                ? StorageClient.Create()
                : StorageClient.Create(Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(_source.GcsCredentialPath));
        }

        // Initialize destination clients
        if (_destination.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_destination.Region)
            };
            _s3DestClient = new AmazonS3Client(_destination.AccessKey, _destination.SecretKey, s3Config);
        }
        else if (_destination.Type.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            _azureDestClient = new BlobContainerClient(_destination.AzureConnectionString, _destination.ContainerName);
        }
        else if (_destination.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase))
        {
            _gcsDestClient = string.IsNullOrWhiteSpace(_destination.GcsCredentialPath)
                ? StorageClient.Create()
                : StorageClient.Create(Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(_destination.GcsCredentialPath));
        }
    }

    /// <summary>
    /// Downloads a file from source and uploads it to the configured destination.
    /// Returns the new URL, file size, and SHA-256 hash.
    /// </summary>
    public async Task<(string NewUrl, long Size, string Hash)> TransferAsync(string sourceUrl, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            // Download from source (supports HTTP, S3, Azure, GCS, Local)
            var content = await DownloadFromSourceAsync(sourceUrl, ct);

            // Check file size
            if (content.Length > (long)_maxFileSizeMB * 1024 * 1024)
                throw new InvalidOperationException($"File exceeds max size of {_maxFileSizeMB}MB (actual: {content.Length / (1024.0 * 1024.0):F2}MB)");

            // Compute SHA-256 hash
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();

            // Generate destination path with date-based partitioning
            var fileName = GetFileName(sourceUrl, hash);
            var destPath = $"{DateTime.UtcNow:yyyy/MM/dd}/{hash[..8]}_{fileName}";

            // Upload to destination
            var newUrl = await UploadToDestinationAsync(content, destPath, ct);

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
            if (_s3DestClient == null) throw new InvalidOperationException("S3 client not initialized");
            var key = ExtractS3Key(destinationUrl);
            await _s3DestClient.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = _destination.BucketName,
                Key = key
            }, ct);
        }
        else if (_destination.Type.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            if (_azureDestClient == null) throw new InvalidOperationException("Azure client not initialized");
            var blobName = ExtractAzureBlobName(destinationUrl);
            await _azureDestClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
        }
        else if (_destination.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase))
        {
            if (_gcsDestClient == null) throw new InvalidOperationException("GCS client not initialized");
            var objectName = ExtractGcsObjectName(destinationUrl);
            await _gcsDestClient.DeleteObjectAsync(_destination.GcsBucket, objectName, cancellationToken: ct);
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

    private async Task<byte[]> DownloadFromSourceAsync(string sourceUrl, CancellationToken ct)
    {
        var sourceType = _source.Type.ToLowerInvariant();

        switch (sourceType)
        {
            case "http":
                using (var response = await _httpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync(ct);
                }

            case "s3":
                if (_s3SourceClient == null) throw new InvalidOperationException("S3 source client not initialized");
                var s3Key = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? ExtractS3Key(sourceUrl)
                    : sourceUrl;
                using (var s3Response = await _s3SourceClient.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = _source.BucketName,
                    Key = s3Key
                }, ct))
                using (var ms = new MemoryStream())
                {
                    await s3Response.ResponseStream.CopyToAsync(ms, ct);
                    return ms.ToArray();
                }

            case "azure":
                if (_azureSourceClient == null) throw new InvalidOperationException("Azure source client not initialized");
                var blobName = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? ExtractAzureBlobName(sourceUrl)
                    : sourceUrl;
                var blobClient = _azureSourceClient.GetBlobClient(blobName);
                using (var azMs = new MemoryStream())
                {
                    await blobClient.DownloadToAsync(azMs, ct);
                    return azMs.ToArray();
                }

            case "gcs":
                if (_gcsSourceClient == null) throw new InvalidOperationException("GCS source client not initialized");
                var objectName = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? ExtractGcsObjectName(sourceUrl)
                    : sourceUrl;
                using (var gcsMs = new MemoryStream())
                {
                    await _gcsSourceClient.DownloadObjectAsync(_source.GcsBucket, objectName, gcsMs, cancellationToken: ct);
                    return gcsMs.ToArray();
                }

            case "local":
                var filePath = Path.IsPathRooted(sourceUrl)
                    ? sourceUrl
                    : Path.Combine(_source.BasePath, sourceUrl);
                return await File.ReadAllBytesAsync(filePath, ct);

            default:
                throw new NotSupportedException($"Source type '{_source.Type}' is not supported. Use 'http', 'local', 's3', 'azure', or 'gcs'.");
        }
    }

    private async Task<string> UploadToDestinationAsync(byte[] content, string destPath, CancellationToken ct)
    {
        var destType = _destination.Type.ToLowerInvariant();

        switch (destType)
        {
            case "local":
                var fullPath = Path.Combine(_destination.BasePath, destPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllBytesAsync(fullPath, content, ct);
                return fullPath;

            case "s3":
                if (_s3DestClient == null) throw new InvalidOperationException("S3 client not initialized");
                using (var stream = new MemoryStream(content))
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = _destination.BucketName,
                        Key = destPath,
                        InputStream = stream,
                        ContentType = GetContentType(destPath),
                        AutoCloseStream = false
                    };
                    await _s3DestClient.PutObjectAsync(request, ct);
                    return $"https://{_destination.BucketName}.s3.{_destination.Region}.amazonaws.com/{destPath}";
                }

            case "azure":
                if (_azureDestClient == null) throw new InvalidOperationException("Azure client not initialized");
                var blobClient = _azureDestClient.GetBlobClient(destPath);
                using (var azStream = new MemoryStream(content))
                {
                    await blobClient.UploadAsync(azStream, overwrite: true, cancellationToken: ct);
                    return blobClient.Uri.ToString();
                }

            case "gcs":
                if (_gcsDestClient == null) throw new InvalidOperationException("GCS client not initialized");
                using (var gcsStream = new MemoryStream(content))
                {
                    var obj = await _gcsDestClient.UploadObjectAsync(
                        _destination.GcsBucket,
                        destPath,
                        GetContentType(destPath),
                        gcsStream,
                        cancellationToken: ct);
                    return $"https://storage.googleapis.com/{_destination.GcsBucket}/{destPath}";
                }

            default:
                throw new NotSupportedException($"Destination type '{_destination.Type}' is not supported. Use 'local', 's3', 'azure', or 'gcs'.");
        }
    }

    private static string GetFileName(string sourceUrl, string hash)
    {
        try
        {
            var fileName = Path.GetFileName(new Uri(sourceUrl).AbsolutePath);
            if (!string.IsNullOrEmpty(fileName) && Path.HasExtension(fileName))
                return fileName;
        }
        catch { }
        return $"{hash[..16]}.bin";
    }

    private static string ExtractS3Key(string url)
    {
        var uri = new Uri(url);
        return uri.AbsolutePath.TrimStart('/');
    }

    private static string ExtractAzureBlobName(string url)
    {
        // Azure URL format: https://<account>.blob.core.windows.net/<container>/<blob-path>
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        return segments.Length > 1 ? segments[1] : segments[0];
    }

    private static string ExtractGcsObjectName(string url)
    {
        // GCS URL format: https://storage.googleapis.com/<bucket>/<object-path>
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        return segments.Length > 1 ? segments[1] : segments[0];
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
        _s3SourceClient?.Dispose();
        _s3DestClient?.Dispose();
    }
}
