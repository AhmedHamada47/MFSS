namespace MFSS.Models;

public class SourceDbConfig
{
    public string ConnectionString { get; set; } = "";
    public List<SourceTableConfig> Tables { get; set; } = new();

    public List<SourceTableConfig> GetEffectiveTables() => Tables.Where(t => !string.IsNullOrEmpty(t.TableName)).ToList();
}
