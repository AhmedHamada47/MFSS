namespace MFSS.Models;

public class DestinationDbConfig
{
    public string ConnectionString { get; set; } = "";
    /// <summary>
    /// When true, creates a separate migration log table per source table.
    /// Table names will be: MigrationLog_{SourceTableName}
    /// When false, all records go into a single MigrationLog table.
    /// </summary>
    public bool SeparateTablesPerSource { get; set; } = true;
}
