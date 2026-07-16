using FluentValidation;
using MFSS.Models;

namespace MFSS.Configuration;

public class SourceDbConfigValidator : AbstractValidator<SourceDbConfig>
{
    public SourceDbConfigValidator()
    {
        RuleFor(x => x.ConnectionString).NotEmpty().WithMessage("SourceDb.ConnectionString is required.");
        RuleFor(x => x.Tables).Must(t => t != null && t.Any(x => !string.IsNullOrEmpty(x.TableName)))
            .WithMessage("SourceDb.Tables must have at least one configured table.");
        RuleForEach(x => x.Tables).Where(t => !string.IsNullOrEmpty(t.TableName)).ChildRules(table =>
        {
            table.RuleFor(t => t.UrlColumn).NotEmpty().WithMessage(t => $"Table '{t.TableName}' must have a UrlColumn configured.");
            table.RuleFor(t => t.IdColumn).NotEmpty().WithMessage(t => $"Table '{t.TableName}' must have an IdColumn configured.");
        });
    }
}
