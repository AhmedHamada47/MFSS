using MFSS.Models;

namespace MFSS.Abstractions;

public interface IEnvConfigResolver
{
    void ResolveAll(SourceDbConfig sourceDb, DestinationDbConfig destDb, ThirdDbConfig thirdDb, FileSystemConfig srcFs, FileSystemConfig destFs);
}
