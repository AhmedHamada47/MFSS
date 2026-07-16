namespace MFSS.Abstractions;

public interface IThirdDbService
{
    Task<(int Success, int Failed)> UpdateWithTransactionAsync(List<(long Id, string Url)> records, CancellationToken ct = default);
}
