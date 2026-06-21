using System.Text.RegularExpressions;

namespace MFSS.Services.Database;

public static class SqlSanitizer
{
    private static readonly Regex SafeRegex = new(@"^[\w\s\-]+$", RegexOptions.Compiled);

    public static string SanitizeIdentifier(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Identifier cannot be empty.");
        if (id.Length > 128) throw new ArgumentException($"Identifier too long: '{id}'");
        if (!SafeRegex.IsMatch(id)) throw new ArgumentException($"Unsafe identifier: '{id}'");
        var lower = id.ToLower();
        string[] blocked = { "drop ", "delete ", "insert ", "update ", "exec ", "--", ";", "/*", "xp_" };
        foreach (var b in blocked)
            if (lower.Contains(b)) throw new ArgumentException($"Blocked keyword in: '{id}'");
        return id;
    }

    public static string SanitizeFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return "";
        var lower = filter.ToLower();
        string[] blocked = { "drop ", "delete ", "insert ", "update ", "exec ", "xp_", "/*", "into ", "grant " };
        foreach (var b in blocked)
            if (lower.Contains(b)) throw new ArgumentException($"Blocked keyword in filter: '{filter}'");
        return filter;
    }
}
