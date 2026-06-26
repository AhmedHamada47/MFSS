namespace MFSS.Models;

public class MigrationSettings
{
    public string Name { get; set; } = "default";
    public string Mode { get; set; } = "migrate";
    public bool DryRun { get; set; } = false;
    public bool FreshStart { get; set; } = false;
    public int ParallelDownloads { get; set; } = 4;
    public int MaxRetries { get; set; } = 3;
    public int RateLimitPerSecond { get; set; } = 10;
    public int MaxFileSizeMB { get; set; } = 100;
}
