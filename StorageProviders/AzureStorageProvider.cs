using Azure.Storage.Blobs;
using MFSS.Models;

namespace MFSS.StorageProviders;

public class AzureStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _containerClient;
    private readonly FileSystemConfig _config;

    public AzureStorageProvider(FileSystemConfig config)
    {
        _config = config;
        _containerClient = new BlobContainerClient(config.AzureConnectionString, config.ContainerName);
    }

    public async Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default)
    {
        var blobName = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? ExtractAzureBlobName(sourceUrl)
            : sourceUrl;
        var blobClient = _containerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(destPath);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: ct);
        return blobClient.Uri.ToString();
    }

    public async Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        var blobName = ExtractAzureBlobName(destinationUrl);
        await _containerClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: ct);
    }

    internal static string ExtractAzureBlobName(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
        return segments.Length > 1 ? segments[1] : segments[0];
    }
}
