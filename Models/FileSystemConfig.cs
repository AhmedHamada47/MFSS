namespace MFSS.Models;

public class FileSystemConfig
{
    public string Type { get; set; } = "local"; // local, s3, azure, gcs, http

    // Common
    public string BasePath { get; set; } = "";

    // S3
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";

    // Azure Blob Storage
    public string AzureConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "";

    // Google Cloud Storage
    public string GcsCredentialPath { get; set; } = "";
    public string GcsBucket { get; set; } = "";
    public string GcsProjectId { get; set; } = "";
}
