using FluentValidation;
using MFSS.Models;

namespace MFSS.Configuration;

public class MigrationSettingsValidator : AbstractValidator<MigrationSettings>
{
    public MigrationSettingsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Migration.Name is required.");
        RuleFor(x => x.Mode).Must(m => m.Equals(MigrationStatus.MigrateMode, StringComparison.OrdinalIgnoreCase)
            || m.Equals(MigrationStatus.RollbackMode, StringComparison.OrdinalIgnoreCase))
            .WithMessage(x => $"Migration.Mode must be 'migrate' or 'rollback', got: '{x.Mode}'");
        RuleFor(x => x.ParallelDownloads).InclusiveBetween(1, 100)
            .WithMessage(x => $"Migration.ParallelDownloads must be 1-100, got: {x.ParallelDownloads}");
        RuleFor(x => x.MaxRetries).InclusiveBetween(1, 20)
            .WithMessage(x => $"Migration.MaxRetries must be 1-20, got: {x.MaxRetries}");
        RuleFor(x => x.RateLimitPerSecond).InclusiveBetween(1, 1000)
            .WithMessage(x => $"Migration.RateLimitPerSecond must be 1-1000, got: {x.RateLimitPerSecond}");
        RuleFor(x => x.MaxFileSizeMB).InclusiveBetween(1, 10240)
            .WithMessage(x => $"Migration.MaxFileSizeMB must be 1-10240, got: {x.MaxFileSizeMB}");
    }
}
