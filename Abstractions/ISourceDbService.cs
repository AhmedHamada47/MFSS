using MFSS.Models;

namespace MFSS.Abstractions;

public interface ISourceDbService
{
    Task<IReadOnlyList<MediaRecord>> FetchRecordsAsync(CancellationToken ct = default);
}
