using MFSS.Models;

namespace MFSS.Services.FileProviders;

public class LocalProvider : IFileProvider
{
    private readonly string _folder;
    public LocalProvider(FileSystemConfig c) { _folder = c.LocalFolderPath; Directory.CreateDirectory(_folder); }

    public async Task<(string url, long size)> UploadAsync(Stream s, string key, string ct)
    {
        var safePath = Uri.UnescapeDataString(key.Replace("/", Path.DirectorySeparatorChar.ToString()));
        var path = Path.Combine(_folder, safePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var fs = File.Create(path))
        {
            await s.CopyToAsync(fs);
        }
        return (path, new FileInfo(path).Length);
    }

    public Task<Stream> DownloadAsync(string src, string key)
    {
        var path = File.Exists(src) ? src : Path.Combine(_folder, key);
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    public Task DeleteAsync(string url) { if (File.Exists(url)) File.Delete(url); return Task.CompletedTask; }
    public string GetPublicUrl(string key) => Path.Combine(_folder, key);
}
