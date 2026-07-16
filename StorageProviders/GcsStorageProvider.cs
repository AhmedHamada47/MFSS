using Google.Cloud.Storage.V1;
using MFSS.Models;
using MFSS.Services;

namespace MFSS.StorageProviders;

public class GcsStorageProvider : IStorageProvider, IDisposable
{
    private readonly StorageClient _client;
    private readonly FileSystemConfig _config;

    public GcsStorageProvider(FileSystemConfig config)
    {
        _config = config;
        _client = string.IsNullOrWhiteSpace(config.GcsCredentialPath)
            ? StorageClient.Create()
            : StorageClient.Create(Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(config.GcsCredentialPath));
    }

    public Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default)
    {
        var objectName = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? ExtractGcsObjectName(sourceUrl)
            : sourceUrl;
        var ms = new MemoryStream();
        return _client.DownloadObjectAsync(_config.GcsBucket, objectName, ms, cancellationToken: ct)
            .ContinueWith(t =>
            {
                ms.Position = 0;
                return (Stream)ms;
            }, ct);
    }

    public async Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default)
    {
        await _client.UploadObjectAsync(
            _config.GcsBucket,
            destPath,
            MimeTypeMap.GetContentType(destPath),
            content,
            cancellationToken: ct);
        return $"https://storage.googleapis.com/{_config.GcsBucket}/{destPath}";
    }

    public async Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        var objectName = ExtractGcsObjectName(destinationUrl);
        await _client.DeleteObjectAsync(_config.GcsBucket, objectName, cancellationToken: ct);
    }

    internal static string ExtractGcsObjectName(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        return segments.Length > 1 ? segments[1] : segments[0];
    }

    public void Dispose() => _client.Dispose();
}
