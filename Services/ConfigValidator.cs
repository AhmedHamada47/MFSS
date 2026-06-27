using MFSS.Models;

namespace MFSS.Services;

/// <summary>
/// Validates all configuration settings before migration begins.
/// Returns a list of validation errors or an empty list if all is valid.
/// </summary>
public static class ConfigValidator
{
    public static List<string> Validate(
        MigrationSettings settings,
        SourceDbConfig sourceDb,
        FileSystemConfig srcFs,
        FileSystemConfig destFs,
        DestinationDbConfig destDb,
        ThirdDbConfig thirdDb)
    {
        var errors = new List<string>();

        // Migration settings
        if (string.IsNullOrWhiteSpace(settings.Name))
            errors.Add("Migration.Name is required.");
        if (!settings.Mode.Equals("migrate", StringComparison.OrdinalIgnoreCase) &&
            !settings.Mode.Equals("rollback", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Migration.Mode must be 'migrate' or 'rollback', got: '{settings.Mode}'");
        if (settings.ParallelDownloads < 1 || settings.ParallelDownloads > 100)
            errors.Add($"Migration.ParallelDownloads must be 1-100, got: {settings.ParallelDownloads}");
        if (settings.MaxRetries < 1 || settings.MaxRetries > 20)
            errors.Add($"Migration.MaxRetries must be 1-20, got: {settings.MaxRetries}");
        if (settings.RateLimitPerSecond < 1 || settings.RateLimitPerSecond > 1000)
            errors.Add($"Migration.RateLimitPerSecond must be 1-1000, got: {settings.RateLimitPerSecond}");
        if (settings.MaxFileSizeMB < 1 || settings.MaxFileSizeMB > 10240)
            errors.Add($"Migration.MaxFileSizeMB must be 1-10240, got: {settings.MaxFileSizeMB}");

        // Source DB
        if (string.IsNullOrWhiteSpace(sourceDb.ConnectionString))
            errors.Add("SourceDb.ConnectionString is required.");
        if (sourceDb.GetEffectiveTables().Count == 0)
            errors.Add("SourceDb.Tables must have at least one configured table.");

        foreach (var table in sourceDb.GetEffectiveTables())
        {
            if (string.IsNullOrWhiteSpace(table.UrlColumn))
                errors.Add($"Table '{table.TableName}' must have a UrlColumn configured.");
            if (string.IsNullOrWhiteSpace(table.IdColumn))
                errors.Add($"Table '{table.TableName}' must have an IdColumn configured.");
        }

        // Destination file system
        if (string.IsNullOrWhiteSpace(destFs.Type))
            errors.Add("DestinationFileSystem.Type is required (local, s3, azure, or gcs).");
        else if (destFs.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(destFs.BucketName))
                errors.Add("DestinationFileSystem.BucketName is required for S3.");
            if (string.IsNullOrWhiteSpace(destFs.Region))
                errors.Add("DestinationFileSystem.Region is required for S3.");
            if (string.IsNullOrWhiteSpace(destFs.AccessKey))
                errors.Add("DestinationFileSystem.AccessKey is required for S3.");
            if (string.IsNullOrWhiteSpace(destFs.SecretKey))
                errors.Add("DestinationFileSystem.SecretKey is required for S3.");
        }
        else if (destFs.Type.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(destFs.AzureConnectionString))
                errors.Add("DestinationFileSystem.AzureConnectionString is required for Azure Blob Storage.");
            if (string.IsNullOrWhiteSpace(destFs.ContainerName))
                errors.Add("DestinationFileSystem.ContainerName is required for Azure Blob Storage.");
        }
        else if (destFs.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(destFs.GcsBucket))
                errors.Add("DestinationFileSystem.GcsBucket is required for Google Cloud Storage.");
        }
        else if (destFs.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(destFs.BasePath))
                errors.Add("DestinationFileSystem.BasePath is required for local storage.");
        }
        else
        {
            errors.Add($"DestinationFileSystem.Type '{destFs.Type}' is not supported. Use 'local', 's3', 'azure', or 'gcs'.");
        }

        // Source file system
        if (!string.IsNullOrWhiteSpace(srcFs.Type))
        {
            if (srcFs.Type.Equals("s3", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(srcFs.BucketName))
                    errors.Add("SourceFileSystem.BucketName is required for S3 source.");
                if (string.IsNullOrWhiteSpace(srcFs.Region))
                    errors.Add("SourceFileSystem.Region is required for S3 source.");
                if (string.IsNullOrWhiteSpace(srcFs.AccessKey))
                    errors.Add("SourceFileSystem.AccessKey is required for S3 source.");
                if (string.IsNullOrWhiteSpace(srcFs.SecretKey))
                    errors.Add("SourceFileSystem.SecretKey is required for S3 source.");
            }
            else if (srcFs.Type.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(srcFs.AzureConnectionString))
                    errors.Add("SourceFileSystem.AzureConnectionString is required for Azure Blob Storage source.");
                if (string.IsNullOrWhiteSpace(srcFs.ContainerName))
                    errors.Add("SourceFileSystem.ContainerName is required for Azure Blob Storage source.");
            }
            else if (srcFs.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(srcFs.GcsBucket))
                    errors.Add("SourceFileSystem.GcsBucket is required for Google Cloud Storage source.");
            }
            else if (!srcFs.Type.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                     !srcFs.Type.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"SourceFileSystem.Type '{srcFs.Type}' is not supported. Use 'http', 'local', 's3', 'azure', or 'gcs'.");
            }
        }

        // Destination DB (log)
        if (string.IsNullOrWhiteSpace(destDb.ConnectionString))
            errors.Add("DestinationDb.ConnectionString is required.");

        // Third DB (optional)
        if (thirdDb.Enabled)
        {
            if (string.IsNullOrWhiteSpace(thirdDb.ConnectionString))
                errors.Add("ThirdDb.ConnectionString is required when ThirdDb.Enabled is true.");
            if (string.IsNullOrWhiteSpace(thirdDb.UpdateQuery))
                errors.Add("ThirdDb.UpdateQuery is required when ThirdDb.Enabled is true.");
            if (!thirdDb.UpdateQuery.Contains("{id}") || !thirdDb.UpdateQuery.Contains("{url}"))
                errors.Add("ThirdDb.UpdateQuery must contain {id} and {url} placeholders.");
        }

        return errors;
    }
}
