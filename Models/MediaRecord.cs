namespace MFSS.Models;

public class MediaRecord
{
    public long Id { get; set; }
    public string SourceUrl { get; set; } = "";
    public string SourceTable { get; set; } = "";
    public string? DestinationUrl { get; set; }
    public long? FileSize { get; set; }
    public string? FileHash { get; set; }
    public string Status { get; set; } = "pending";
    public int RetryCount { get; set; } = 0;
    public string? ErrorMessage { get; set; }
}
