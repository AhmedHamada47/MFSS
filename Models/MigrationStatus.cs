namespace MFSS.Models;

public static class MigrationStatus
{
    public const string Pending = "pending";
    public const string Success = "success";
    public const string Failed = "failed";

    public const string MigrateMode = "migrate";
    public const string RollbackMode = "rollback";
}
