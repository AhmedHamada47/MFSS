using FluentValidation;
using MFSS.Models;

namespace MFSS.Configuration;

public class ThirdDbConfigValidator : AbstractValidator<ThirdDbConfig>
{
    public ThirdDbConfigValidator()
    {
        When(x => x.Enabled, () =>
        {
            RuleFor(x => x.ConnectionString).NotEmpty().WithMessage("ThirdDb.ConnectionString is required when ThirdDb.Enabled is true.");
            RuleFor(x => x.UpdateQuery).NotEmpty().WithMessage("ThirdDb.UpdateQuery is required when ThirdDb.Enabled is true.");
            RuleFor(x => x.UpdateQuery).Must(q => q.Contains("{id}") && q.Contains("{url}"))
                .WithMessage("ThirdDb.UpdateQuery must contain {id} and {url} placeholders.");
        });
    }
}
