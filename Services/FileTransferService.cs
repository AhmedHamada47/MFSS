using MFSS.Abstractions;
using MFSS.Models;
using MFSS.StorageProviders;

namespace MFSS.Services;

public class FileTransferService : IFileTransferService
{
    private readonly IStorageProvider _source;
    private readonly IStorageProvider _destination;
    private readonly int _maxFileSizeMB;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly Timer _rateLimiterRefill;

    public FileTransferService(
        IStorageProvider sourceProvider,
        IStorageProvider destinationProvider,
        int rateLimitPerSecond,
        int maxFileSizeMB)
    {
        _source = sourceProvider;
        _destination = destinationProvider;
        _maxFileSizeMB = maxFileSizeMB;
        _rateLimiter = new SemaphoreSlim(rateLimitPerSecond, rateLimitPerSecond);

        _rateLimiterRefill = new Timer(_ =>
        {
            try
            {
                if (_rateLimiter.CurrentCount < rateLimitPerSecond)
                    _rateLimiter.Release();
            }
            catch (ObjectDisposedException) { }
            catch (SemaphoreFullException) { }
        }, null, 0, Math.Max(1, 1000 / rateLimitPerSecond));
    }

    public async Task<(string NewUrl, long Size, string Hash)> TransferAsync(string sourceUrl, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var maxBytes = (long)_maxFileSizeMB * 1024 * 1024;
            using var sourceStream = await _source.DownloadAsync(sourceUrl, ct);

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
                {
                    await sourceStream.CopyToAsync(fileStream, ct);
                }

                var fileInfo = new FileInfo(tempFile);
                if (fileInfo.Length > maxBytes)
                    throw new InvalidOperationException($"File exceeds max size of {_maxFileSizeMB}MB (actual: {fileInfo.Length / (1024.0 * 1024.0):F2}MB)");

                string hash;
                using (var readStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(readStream, ct);
                    hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
                }

                var destPath = StorageProviderExtensions.GetDatePartitionedPath(sourceUrl, hash);

                string newUrl;
                using (var uploadStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    newUrl = await _destination.UploadAsync(uploadStream, destPath, ct);
                }

                return (newUrl, fileInfo.Length, hash);
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        finally
        {
            // Token-bucket: timer handles refill
        }
    }

    public async Task DeleteAsync(string destinationUrl, CancellationToken ct = default)
    {
        await _destination.DeleteAsync(destinationUrl, ct);
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
        _rateLimiterRefill.Dispose();
        (_source as IDisposable)?.Dispose();
        (_destination as IDisposable)?.Dispose();
    }
}
