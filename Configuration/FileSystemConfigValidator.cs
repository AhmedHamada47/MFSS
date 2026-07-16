using FluentValidation;
using MFSS.Models;

namespace MFSS.Configuration;

public class FileSystemConfigValidator : AbstractValidator<FileSystemConfig>
{
    public FileSystemConfigValidator(string label)
    {
        When(x => !string.IsNullOrWhiteSpace(x.Type), () =>
        {
            RuleFor(x => x.Type).Must(t => new[] { "http", "local", "s3", "azure", "gcs" }.Contains(t.ToLowerInvariant()))
                .WithMessage(x => $"{label}.Type '{x.Type}' is not supported. Use 'http', 'local', 's3', 'azure', or 'gcs'.");

            When(x => x.Type.Equals("s3", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.BucketName).NotEmpty().WithMessage($"{label}.BucketName is required for S3.");
                RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Region) || !string.IsNullOrWhiteSpace(x.Endpoint))
                    .WithMessage($"{label}.Region or Endpoint is required for S3.");
                RuleFor(x => x.AccessKey).NotEmpty().WithMessage($"{label}.AccessKey is required for S3.");
                RuleFor(x => x.SecretKey).NotEmpty().WithMessage($"{label}.SecretKey is required for S3.");
            });

            When(x => x.Type.Equals("azure", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.AzureConnectionString).NotEmpty().WithMessage($"{label}.AzureConnectionString is required for Azure Blob Storage.");
                RuleFor(x => x.ContainerName).NotEmpty().WithMessage($"{label}.ContainerName is required for Azure Blob Storage.");
            });

            When(x => x.Type.Equals("gcs", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.GcsBucket).NotEmpty().WithMessage($"{label}.GcsBucket is required for Google Cloud Storage.");
            });

            When(x => x.Type.Equals("local", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.BasePath).NotEmpty().WithMessage($"{label}.BasePath is required for local storage.");
            });
        });
    }
}
