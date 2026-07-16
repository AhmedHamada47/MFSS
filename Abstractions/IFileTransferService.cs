namespace MFSS.Abstractions;

public interface IFileTransferService : IDisposable
{
    Task<(string NewUrl, long Size, string Hash)> TransferAsync(string sourceUrl, CancellationToken ct = default);
    Task DeleteAsync(string destinationUrl, CancellationToken ct = default);
}
