namespace MFSS.Models;

public class MigrationSettings
{
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "migrate";
    public bool DryRun { get; set; } = false;
    public bool FreshStart { get; set; } = false;
    public int MaxRetries { get; set; } = 10;
    public int ParallelDownloads { get; set; } = 5;
    public int DelayBetweenBatchesMs { get; set; } = 500;
    public int MaxFileSizeMB { get; set; } = 500;
    public int RateLimitPerSecond { get; set; } = 10;
}

public class SourceDbConfig
{
    public string ConnectionString { get; set; } = "";
    public string TableName { get; set; } = "";
    public string IdColumn { get; set; } = "";
    public string UrlColumn { get; set; } = "";
    public string Filter { get; set; } = "";
}

public class FileSystemConfig
{
    public string Type { get; set; } = "Url";
    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string PublicUrl { get; set; } = "";
    public string AzureConnectionString { get; set; } = "";
    public string AzureContainerName { get; set; } = "";
    public string GoogleCredentialsPath { get; set; } = "";
    public string GoogleCredentialsJson { get; set; } = "";
    public string LocalFolderPath { get; set; } = "";
}

public class DestinationDbConfig
{
    public string ConnectionString { get; set; } = "";
    public string TableName { get; set; } = "";
}

public class ThirdDbConfig
{
    public bool Enabled { get; set; } = false;
    public string ConnectionString { get; set; } = "";
    public string TableName { get; set; } = "";
    public string IdColumn { get; set; } = "";
    public string UrlColumn { get; set; } = "";
}

public class NotificationConfig
{
    public bool Enabled { get; set; } = false;
    public string WebhookUrl { get; set; } = "";
    public bool NotifyOnComplete { get; set; } = true;
    public bool NotifyOnFailure { get; set; } = true;
}

public class MediaRecord
{
    public int Id { get; set; }
    public string SourceUrl { get; set; } = "";
    public int RetryCount { get; set; } = 0;
}
