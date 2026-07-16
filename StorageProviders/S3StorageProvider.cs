using Amazon.S3;
using Amazon.S3.Model;
using MFSS.Models;
using MFSS.Services;

namespace MFSS.StorageProviders;

public class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly FileSystemConfig _config;

    public S3StorageProvider(FileSystemConfig config)
    {
        _config = config;
        var s3Config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(config.Endpoint))
        {
            s3Config.ServiceURL = config.Endpoint;
            s3Config.ForcePathStyle = true;
        }
        else
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config.Region);
        }
        s3Config.SignatureVersion = "4";
        _client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
    }

    public async Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default)
    {
        var key = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? ExtractS3Key(sourceUrl)
            : sourceUrl;
        using var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key
        }, ct);
        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = destPath,
            InputStream = content,
            ContentType = MimeTypeMap.GetContentType(destPath),
            AutoCloseStream = false,
            DisablePayloadSigning = true
        };

        request.Headers.ContentLength = content.Length;
        await _client.PutObjectAsync(request, ct);

        if (!string.IsNullOrWhiteSpace(_config.BasePath))
            return $"{_config.BasePath.TrimEnd('/')}/{destPath}";
        if (!string.IsNullOrWhiteSpace(_config.Endpoint))
            return $"{_config.Endpoint.TrimEnd('/')}/{_config.BucketName}/{destPath}";
        return $"https://{_config.BucketName}.s3.{_config.Region}.amazonaws.com/{destPath}";
    }

    public async Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        var key = ExtractS3Key(destinationUrl);
        await _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key
        }, ct);
    }

    internal static string ExtractS3Key(string url) => new Uri(url).AbsolutePath.TrimStart('/');

    public void Dispose() => _client.Dispose();
}
