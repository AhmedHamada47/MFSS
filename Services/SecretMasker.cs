namespace MFSS.Services;

public static class SecretMasker
{
    public static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return "(empty)";

        // Mask password in connection strings
        var masked = System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(password|pwd)\s*=\s*[^;]+",
            "$1=****",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return masked;
    }
}
