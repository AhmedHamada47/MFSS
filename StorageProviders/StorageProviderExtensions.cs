using MFSS.Models;

namespace MFSS.StorageProviders;

public static class StorageProviderExtensions
{
    public static string GetFileName(string sourceUrl, string hash)
    {
        try
        {
            var fileName = Path.GetFileName(new Uri(sourceUrl).AbsolutePath);
            if (!string.IsNullOrEmpty(fileName) && Path.HasExtension(fileName))
                return fileName;
        }
        catch { }
        return $"{hash[..16]}.bin";
    }

    public static string GetDatePartitionedPath(string sourceUrl, string hash)
    {
        var fileName = GetFileName(sourceUrl, hash);
        return $"{DateTime.UtcNow:yyyy/MM/dd}/{hash[..8]}_{fileName}";
    }
}
