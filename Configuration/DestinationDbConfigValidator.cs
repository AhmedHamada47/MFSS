using FluentValidation;
using MFSS.Models;

namespace MFSS.Configuration;

public class DestinationDbConfigValidator : AbstractValidator<DestinationDbConfig>
{
    public DestinationDbConfigValidator()
    {
        RuleFor(x => x.ConnectionString).NotEmpty().WithMessage("DestinationDb.ConnectionString is required.");
    }
}
