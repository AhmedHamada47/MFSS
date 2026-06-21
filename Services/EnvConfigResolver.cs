using System.Text.RegularExpressions;
using MFSS.Models;

namespace MFSS.Services;

public static class EnvConfigResolver
{
    public static string Resolve(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return Regex.Replace(v, @"\$\{(\w+)\}", m =>
            Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? m.Value);
    }

    public static void ResolveAll(SourceDbConfig src, DestinationDbConfig dest, ThirdDbConfig third,
        FileSystemConfig srcFs, FileSystemConfig destFs)
    {
        src.ConnectionString = Resolve(src.ConnectionString);
        dest.ConnectionString = Resolve(dest.ConnectionString);
        third.ConnectionString = Resolve(third.ConnectionString);
        destFs.Endpoint = Resolve(destFs.Endpoint);
        destFs.AccessKey = Resolve(destFs.AccessKey);
        destFs.SecretKey = Resolve(destFs.SecretKey);
        srcFs.Endpoint = Resolve(srcFs.Endpoint);
        srcFs.AccessKey = Resolve(srcFs.AccessKey);
        srcFs.SecretKey = Resolve(srcFs.SecretKey);
    }
}
