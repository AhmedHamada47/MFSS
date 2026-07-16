using MFSS.Models;

namespace MFSS.StorageProviders;

public class HttpStorageProvider : IStorageProvider
{
    private readonly HttpClient _httpClient;
    private readonly FileSystemConfig _config;

    public HttpStorageProvider(HttpClient httpClient, FileSystemConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<Stream> DownloadAsync(string sourceUrl, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(_config.BasePath) ? sourceUrl : $"{_config.BasePath.TrimEnd('/')}/{sourceUrl.TrimStart('/')}";
        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public Task<string> UploadAsync(Stream content, string destPath, CancellationToken ct = default)
    {
        throw new NotSupportedException("HTTP is a read-only source provider. Cannot upload via HTTP.");
    }

    public Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        throw new NotSupportedException("HTTP is a read-only source provider. Cannot delete via HTTP.");
    }
}
