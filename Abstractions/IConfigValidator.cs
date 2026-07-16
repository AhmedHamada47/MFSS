using MFSS.Models;

namespace MFSS.Abstractions;

public interface IConfigValidator
{
    IReadOnlyList<string> Validate(
        MigrationSettings settings,
        SourceDbConfig sourceDb,
        FileSystemConfig srcFs,
        FileSystemConfig destFs,
        DestinationDbConfig destDb,
        ThirdDbConfig thirdDb);
}
