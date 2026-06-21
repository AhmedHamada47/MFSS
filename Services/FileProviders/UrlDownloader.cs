namespace MFSS.Services.FileProviders;

public class UrlDownloader : IFileProvider, IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public Task<(string, long)> UploadAsync(Stream s, string k, string ct) => throw new NotSupportedException("URL is download-only");
    public async Task<Stream> DownloadAsync(string src, string key)
    {
        var r = await _http.GetAsync(src.StartsWith("http") ? src : key, HttpCompletionOption.ResponseHeadersRead);
        r.EnsureSuccessStatusCode(); return await r.Content.ReadAsStreamAsync();
    }
    public Task DeleteAsync(string u) => throw new NotSupportedException();
    public string GetPublicUrl(string k) => k;
    public void Dispose() => _http.Dispose();
}
