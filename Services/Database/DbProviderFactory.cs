namespace MFSS.Services.Database;

public static class DbProviderFactory
{
    public static IDbProvider Create(string cs) => new SqlServerProvider();
    public static string GetName(string cs) => "SQL Server";
}
