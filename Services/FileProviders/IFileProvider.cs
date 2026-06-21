namespace MFSS.Services.FileProviders;

public interface IFileProvider
{
    Task<(string url, long size)> UploadAsync(Stream stream, string key, string contentType);
    Task<Stream> DownloadAsync(string sourceId, string key);
    Task DeleteAsync(string url);
    string GetPublicUrl(string key);
}
