using System.Text.RegularExpressions;

namespace MFSS.Services;

public static class SecretMasker
{
    public static string MaskConnectionString(string? cs)
    {
        if (string.IsNullOrEmpty(cs)) return "[empty]";
        var r = Regex.Replace(cs, @"(Password|Pwd)\s*=\s*([^;]+)", "$1=****", RegexOptions.IgnoreCase);
        r = Regex.Replace(r, @"(User\s*ID)\s*=\s*([^;]+)", "$1=****", RegexOptions.IgnoreCase);
        return r;
    }

    public static string MaskKey(string? k)
    {
        if (string.IsNullOrEmpty(k)) return "[empty]";
        if (k.StartsWith("${")) return k;
        return k.Length <= 4 ? "****" : k[..4] + "****";
    }

    public static string MaskIfSensitive(string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return "[empty]";
        string[] sensitive = { "Password", "Pwd", "Secret", "AccessKey", "ApiKey", "Token", "ConnectionString" };
        foreach (var s in sensitive)
            if (key.Contains(s, StringComparison.OrdinalIgnoreCase))
                return key.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase) ? MaskConnectionString(value) : MaskKey(value);
        return value;
    }
}
