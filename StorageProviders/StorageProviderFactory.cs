using MFSS.Models;

namespace MFSS.StorageProviders;

public class StorageProviderFactory : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly List<IDisposable> _disposables = new();

    public StorageProviderFactory()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public IStorageProvider CreateSourceProvider(FileSystemConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "http" => new HttpStorageProvider(_httpClient, config),
            "local" => new LocalStorageProvider(config),
            "s3" => TrackDisposable(new S3StorageProvider(config)),
            "azure" => new AzureStorageProvider(config),
            "gcs" => TrackDisposable(new GcsStorageProvider(config)),
            _ => throw new NotSupportedException($"Source type '{config.Type}' is not supported.")
        };
    }

    public IStorageProvider CreateDestinationProvider(FileSystemConfig config)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "local" => new LocalStorageProvider(config),
            "s3" => TrackDisposable(new S3StorageProvider(config)),
            "azure" => new AzureStorageProvider(config),
            "gcs" => TrackDisposable(new GcsStorageProvider(config)),
            _ => throw new NotSupportedException($"Destination type '{config.Type}' is not supported.")
        };
    }

    private T TrackDisposable<T>(T instance) where T : IDisposable
    {
        _disposables.Add(instance);
        return instance;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        foreach (var d in _disposables)
            d.Dispose();
        _disposables.Clear();
    }
}
