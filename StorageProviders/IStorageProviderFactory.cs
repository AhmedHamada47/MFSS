using MFSS.Models;

namespace MFSS.StorageProviders;

public interface IStorageProviderFactory
{
    IStorageProvider CreateSourceProvider(FileSystemConfig config);
    IStorageProvider CreateDestinationProvider(FileSystemConfig config);
}
