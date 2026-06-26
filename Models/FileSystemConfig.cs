namespace MFSS.Models;

public class FileSystemConfig
{
    public string Type { get; set; } = "local"; // local, s3, azure
    public string BasePath { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
