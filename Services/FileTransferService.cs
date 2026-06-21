using System.Security.Cryptography;
using System.Threading.RateLimiting;
using MFSS.Models;
using MFSS.Services.FileProviders;

namespace MFSS.Services;

public class FileTransferService : IDisposable
{
    private readonly IFileProvider _source;
    private readonly IFileProvider _dest;
    private readonly RateLimiter _limiter;
    private readonly int _maxBytes;

    public FileTransferService(FileSystemConfig srcFs, FileSystemConfig destFs, int rateLimit, int maxMB)
    {
        _source = FileProviderFactory.Create(srcFs);
        _dest = FileProviderFactory.Create(destFs);
        _maxBytes = maxMB * 1024 * 1024;
        _limiter = new TokenBucketRateLimiter(new() { TokenLimit = rateLimit, ReplenishmentPeriod = TimeSpan.FromSeconds(1), TokensPerPeriod = rateLimit, QueueLimit = 100 });
    }

    public async Task<(string newUrl, long size, string checksum)> TransferAsync(string sourceUrl)
    {
        using var lease = await _limiter.AcquireAsync();
        if (!lease.IsAcquired) throw new Exception("Rate limit exceeded");

        var tmp = Path.GetTempFileName();
        string hash; long size;
        try
        {
            var key = ExtractKey(sourceUrl);
            using (var src = await _source.DownloadAsync(sourceUrl, key))
            using (var fs = File.Create(tmp))
            using (var sha = SHA256.Create())
            {
                var buf = new byte[81920]; int read; long total = 0;
                while ((read = await src.ReadAsync(buf)) > 0)
                {
                    await fs.WriteAsync(buf.AsMemory(0, read));
                    sha.TransformBlock(buf, 0, read, null, 0);
                    total += read;
                    if (_maxBytes > 0 && total > _maxBytes) throw new Exception($"File too large: {total / 1048576}MB");
                }
                sha.TransformFinalBlock([], 0, 0);
                hash = BitConverter.ToString(sha.Hash!).Replace("-", "").ToLower();
            }
            size = new FileInfo(tmp).Length;
            if (size == 0) throw new Exception("Empty file");

            var ct = MimeTypes.Get(Path.GetExtension(key).TrimStart('.'));
            using var upload = File.OpenRead(tmp);
            var (url, _) = await _dest.UploadAsync(upload, key, ct);
            return (url, size, hash);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public async Task DeleteAsync(string url) => await _dest.DeleteAsync(url);

    public static string ExtractKey(string url)
    {
        var idx = url.IndexOf("/upload/");
        if (idx >= 0) { var a = url[(idx + 8)..]; if (a.StartsWith("v") && a.Contains("/")) a = a[(a.IndexOf("/") + 1)..]; return a; }
        try { return new Uri(url).AbsolutePath.TrimStart('/'); } catch { return Path.GetFileName(url); }
    }

    public void Dispose() { _limiter.Dispose(); if (_source is IDisposable s) s.Dispose(); if (_dest is IDisposable d) d.Dispose(); }
}

public static class MimeTypes
{
    public static string Get(string? e) => (e?.ToLower()) switch
    {
        "jpg" or "jpeg" => "image/jpeg", "png" => "image/png", "gif" => "image/gif",
        "webp" => "image/webp", "svg" => "image/svg+xml", "mp4" => "video/mp4",
        "pdf" => "application/pdf", "zip" => "application/zip", "json" => "application/json",
        _ => "application/octet-stream"
    };
}
