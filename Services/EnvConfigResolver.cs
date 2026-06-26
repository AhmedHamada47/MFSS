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
        destFs.BasePath = Resolve(destFs.BasePath);
        destFs.AccessKey = Resolve(destFs.AccessKey);
        destFs.SecretKey = Resolve(destFs.SecretKey);
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
