using MFSS.Models;

namespace MFSS.Services;

public static class EnvConfigResolver
{
    /// <summary>
    /// Resolves environment variable placeholders in connection strings and paths.
    /// Placeholders use the format: ${ENV_VAR_NAME}
    /// </summary>
    public static void ResolveAll(SourceDbConfig sourceDb, DestinationDbConfig destDb, ThirdDbConfig thirdDb, FileSystemConfig srcFs, FileSystemConfig destFs)
    {
        sourceDb.ConnectionString = Resolve(sourceDb.ConnectionString);
        destDb.ConnectionString = Resolve(destDb.ConnectionString);
        thirdDb.ConnectionString = Resolve(thirdDb.ConnectionString);
        srcFs.BasePath = Resolve(srcFs.BasePath);
        srcFs.AccessKey = Resolve(srcFs.AccessKey);
        srcFs.SecretKey = Resolve(srcFs.SecretKey);
        srcFs.AzureConnectionString = Resolve(srcFs.AzureConnectionString);
        srcFs.ContainerName = Resolve(srcFs.ContainerName);
        srcFs.GcsCredentialPath = Resolve(srcFs.GcsCredentialPath);
        srcFs.GcsBucket = Resolve(srcFs.GcsBucket);
        srcFs.GcsProjectId = Resolve(srcFs.GcsProjectId);
        destFs.BasePath = Resolve(destFs.BasePath);
        destFs.AccessKey = Resolve(destFs.AccessKey);
        destFs.SecretKey = Resolve(destFs.SecretKey);
        destFs.AzureConnectionString = Resolve(destFs.AzureConnectionString);
        destFs.ContainerName = Resolve(destFs.ContainerName);
        destFs.GcsCredentialPath = Resolve(destFs.GcsCredentialPath);
        destFs.GcsBucket = Resolve(destFs.GcsBucket);
        destFs.GcsProjectId = Resolve(destFs.GcsProjectId);
    }

    private static string Resolve(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        return System.Text.RegularExpressions.Regex.Replace(value, @"\$\{(\w+)\}", match =>
        {
            var envVar = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(envVar) ?? match.Value;
        });
    }
}
