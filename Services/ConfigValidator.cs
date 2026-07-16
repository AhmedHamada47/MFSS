using FluentValidation;
using MFSS.Abstractions;
using MFSS.Configuration;
using MFSS.Models;

namespace MFSS.Services;

public class ConfigValidator : IConfigValidator
{
    private readonly MigrationSettingsValidator _settingsValidator;
    private readonly SourceDbConfigValidator _sourceDbValidator;
    private readonly FileSystemConfigValidator _srcFsValidator;
    private readonly FileSystemConfigValidator _destFsValidator;
    private readonly DestinationDbConfigValidator _destDbValidator;
    private readonly ThirdDbConfigValidator _thirdDbValidator;

    public ConfigValidator()
    {
        _settingsValidator = new MigrationSettingsValidator();
        _sourceDbValidator = new SourceDbConfigValidator();
        _srcFsValidator = new FileSystemConfigValidator("SourceFileSystem");
        _destFsValidator = new FileSystemConfigValidator("DestinationFileSystem");
        _destDbValidator = new DestinationDbConfigValidator();
        _thirdDbValidator = new ThirdDbConfigValidator();
    }

    public IReadOnlyList<string> Validate(
        MigrationSettings settings,
        SourceDbConfig sourceDb,
        FileSystemConfig srcFs,
        FileSystemConfig destFs,
        DestinationDbConfig destDb,
        ThirdDbConfig thirdDb)
    {
        var errors = new List<string>();

        errors.AddRange(_settingsValidator.Validate(settings).Errors.Select(e => e.ErrorMessage));
        errors.AddRange(_sourceDbValidator.Validate(sourceDb).Errors.Select(e => e.ErrorMessage));
        errors.AddRange(_srcFsValidator.Validate(srcFs).Errors.Select(e => e.ErrorMessage));
        errors.AddRange(_destFsValidator.Validate(destFs).Errors.Select(e => e.ErrorMessage));
        errors.AddRange(_destDbValidator.Validate(destDb).Errors.Select(e => e.ErrorMessage));
        errors.AddRange(_thirdDbValidator.Validate(thirdDb).Errors.Select(e => e.ErrorMessage));

        return errors;
    }
}
