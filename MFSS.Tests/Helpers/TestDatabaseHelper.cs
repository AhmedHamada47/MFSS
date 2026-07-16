using Microsoft.Data.SqlClient;

namespace MFSS.Tests.Helpers;

public static class TestDatabaseHelper
{
    private const string MasterConnectionString = @"Data Source=(localdb)\ProjectModels;Database=master;Integrated Security=True;TrustServerCertificate=True;";

    public static string CreateTestDatabase(string testName)
    {
        var dbName = $"MFSS_Test_{testName}_{Guid.NewGuid():N}";
        using var conn = new SqlConnection(MasterConnectionString);
        conn.Open();
        using var cmd = new SqlCommand($"CREATE DATABASE [{dbName}]", conn);
        cmd.ExecuteNonQuery();
        return $@"Data Source=(localdb)\ProjectModels;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
    }

    public static void DropTestDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var dbName = builder.InitialCatalog;
        try
        {
            using var conn = new SqlConnection(MasterConnectionString);
            conn.Open();
            using var cmd = new SqlCommand($@"
                IF EXISTS (SELECT 1 FROM sys.databases WHERE name = @dbName)
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END", conn);
            cmd.Parameters.AddWithValue("@dbName", dbName);
            cmd.ExecuteNonQuery();
        }
        catch
        {
        }
    }

    public static void ExecuteNonQuery(string connectionString, string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    public static object? ExecuteScalar(string connectionString, string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn);
        return cmd.ExecuteScalar();
    }
}
