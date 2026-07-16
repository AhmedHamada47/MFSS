namespace MFSS.StorageProviders;

public interface IStorageProvider
{
    Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default);
    Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default);
    Task DeleteAsync(string destinationUrl, CancellationToken ct = default);
}
