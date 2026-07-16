using MFSS.Abstractions;
using MFSS.Models;
using MFSS.Orchestration;
using MFSS.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

Console.OutputEncoding = System.Text.Encoding.UTF8;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nCancellation requested. Finishing current operations...");
};

var dryRunOption = new Option<bool>("--dry-run", "Preview migration without making changes");
var modeOption = new Option<string>("--mode", () => "", "Migration mode: migrate or rollback");
var configOption = new Option<string>("--config", () => "appsettings.json", "Path to configuration file");
var verboseOption = new Option<bool>("--verbose", "Show detailed progress for each file");
var sourceDbOption = new Option<string>("--source-db", () => "", "Override source DB connection string");
var destBucketOption = new Option<string>("--dest-bucket", () => "", "Override destination S3/R2 bucket name");
var destEndpointOption = new Option<string>("--dest-endpoint", () => "", "Override destination S3-compatible endpoint (e.g. R2)");
var rateLimitOption = new Option<int>("--rate-limit", () => 0, "Override rate limit (files/sec)");

var root = new RootCommand("MFSS - Migration File Storage System\n\nA high-performance CLI tool for migrating files from HTTP/S3/Azure/GCS/Local sources to S3-compatible cloud storage (AWS S3, Cloudflare R2, MinIO) with full tracking, retry logic, circuit breaker, and rollback support.\n\nUsage: mfss --mode migrate --config appsettings.json\n       mfss --mode rollback\n       mfss --dry-run --verbose\n       mfss --dest-bucket my-bucket --dest-endpoint https://<id>.r2.cloudflarestorage.com\n\nS3-compatible (Cloudflare R2) destination example:\n  \"DestinationFileSystem\": {\n      \"Type\": \"s3\",\n      \"BucketName\": \"my-bucket\",\n      \"Endpoint\": \"https://<account-id>.r2.cloudflarestorage.com\",\n      \"BasePath\": \"https://pub-<hash>.r2.dev\",\n      \"AccessKey\": \"${MFSS_DEST_ACCESS_KEY}\",\n      \"SecretKey\": \"${MFSS_DEST_SECRET_KEY}\"\n  }")
{
    dryRunOption, modeOption, configOption, verboseOption,
    sourceDbOption, destBucketOption, destEndpointOption, rateLimitOption
};

root.SetHandler(async (bool dryRun, string mode, string configPath, bool verbose, string sourceDbConn, string destBucket, string destEndpoint, int rateLimit) =>
{
    var ct = cts.Token;

    if (!File.Exists(configPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Configuration file not found: {configPath}");
        Console.ResetColor();
        Console.WriteLine("  Create an appsettings.json or specify path with --config");
        Environment.ExitCode = 1;
        return;
    }

    var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(configPath, optional: false)
        .AddEnvironmentVariables("MFSS_")
        .Build();

    var settings = new MigrationSettings();
    config.GetSection("Migration").Bind(settings);
    var sourceDbCfg = new SourceDbConfig();
    config.GetSection("SourceDb").Bind(sourceDbCfg);
    var srcFs = new FileSystemConfig();
    config.GetSection("SourceFileSystem").Bind(srcFs);
    var destFs = new FileSystemConfig();
    config.GetSection("DestinationFileSystem").Bind(destFs);
    var destDb = new DestinationDbConfig();
    config.GetSection("DestinationDb").Bind(destDb);
    var thirdDb = new ThirdDbConfig();
    config.GetSection("ThirdDb").Bind(thirdDb);

    if (dryRun) settings.DryRun = true;
    if (!string.IsNullOrEmpty(mode)) settings.Mode = mode;
    if (!string.IsNullOrEmpty(sourceDbConn)) sourceDbCfg.ConnectionString = sourceDbConn;
    if (!string.IsNullOrEmpty(destBucket)) destFs.BucketName = destBucket;
    if (!string.IsNullOrEmpty(destEndpoint)) destFs.Endpoint = destEndpoint;
    if (rateLimit > 0) settings.RateLimitPerSecond = rateLimit;

    var services = new ServiceCollection();
    services.AddSingleton(settings);
    services.AddSingleton(sourceDbCfg);
    services.AddSingleton(srcFs);
    services.AddSingleton(destFs);
    services.AddSingleton(destDb);
    services.AddSingleton(thirdDb);
    services.AddSingleton<IEnvConfigResolver>(new EnvConfigResolver());
    services.AddSingleton<IConfigValidator>(new ConfigValidator());
    services.AddSingleton<ISourceDbService>(new SourceDbService(sourceDbCfg));
    services.AddSingleton<IDestinationDbService>(new DestinationDbService(destDb));
    services.AddSingleton(sp => new MigrationOrchestrator(
        sp.GetRequiredService<MigrationSettings>(),
        sp.GetRequiredService<SourceDbConfig>(),
        srcFs,
        destFs,
        sp.GetRequiredService<DestinationDbConfig>(),
        sp.GetRequiredService<ThirdDbConfig>(),
        sp.GetRequiredService<IEnvConfigResolver>(),
        sp.GetRequiredService<IConfigValidator>(),
        sp.GetRequiredService<ISourceDbService>(),
        sp.GetRequiredService<IDestinationDbService>(),
        verbose));

    var sp = services.BuildServiceProvider();
    var orchestrator = sp.GetRequiredService<MigrationOrchestrator>();

    Environment.ExitCode = await orchestrator.RunAsync(ct);

}, dryRunOption, modeOption, configOption, verboseOption, sourceDbOption, destBucketOption, destEndpointOption, rateLimitOption);

await root.InvokeAsync(args);
