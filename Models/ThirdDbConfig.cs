namespace MFSS.Models;

public class ThirdDbConfig
{
    public bool Enabled { get; set; } = false;
    public string ConnectionString { get; set; } = "";
    public string UpdateQuery { get; set; } = "";
}
