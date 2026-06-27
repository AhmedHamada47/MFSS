using Microsoft.Extensions.Configuration;
using MFSS.Models;
using MFSS.Services;
using System.CommandLine;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Graceful shutdown support
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n⚠️ Cancellation requested. Finishing current operations...");
};

// CLI options
var dryRunOption = new Option<bool>("--dry-run", "Preview migration without making changes");
var modeOption = new Option<string>("--mode", () => "", "Migration mode: migrate or rollback");
var configOption = new Option<string>("--config", () => "appsettings.json", "Path to configuration file");
var verboseOption = new Option<bool>("--verbose", "Show detailed progress for each file");

var root = new RootCommand("MFSS - Migration File Storage System\n\nA high-performance CLI tool for migrating files from HTTP sources to cloud storage (S3/Local) with full tracking, retry logic, circuit breaker, and rollback support.\n\nInstall: dotnet tool install -g MFSS\nUsage:   mfss --mode migrate --config appsettings.json")
{
    dryRunOption, modeOption, configOption, verboseOption
};

root.SetHandler(async (bool dryRun, string mode, string configPath, bool verbose) =>
{
    var ct = cts.Token;

    // Load config
    if (!File.Exists(configPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ Configuration file not found: {configPath}");
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

    var sourceDb = new SourceDbConfig();
    config.GetSection("SourceDb").Bind(sourceDb);

    var srcFs = new FileSystemConfig();
    config.GetSection("SourceFileSystem").Bind(srcFs);

    var destFs = new FileSystemConfig();
    config.GetSection("DestinationFileSystem").Bind(destFs);

    var destDb = new DestinationDbConfig();
    config.GetSection("DestinationDb").Bind(destDb);

    var thirdDb = new ThirdDbConfig();
    config.GetSection("ThirdDb").Bind(thirdDb);

    // CLI overrides
    if (dryRun) settings.DryRun = true;
    if (!string.IsNullOrEmpty(mode)) settings.Mode = mode;

    // Resolve env variables
    EnvConfigResolver.ResolveAll(sourceDb, destDb, thirdDb, srcFs, destFs);

    // Validate configuration
    var validationErrors = ConfigValidator.Validate(settings, sourceDb, srcFs, destFs, destDb, thirdDb);
    if (validationErrors.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ Configuration validation failed:");
        foreach (var error in validationErrors)
            Console.WriteLine($"   • {error}");
        Console.ResetColor();
        Environment.ExitCode = 1;
        return;
    }

    // Logger
    using var log = new Logger(settings.Name);

    log.Header($"🚀 MFSS - Migration File Storage System v{typeof(MFSS.Services.Logger).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}");
    log.Header("═══════════════════════════════════════════════════════");
    log.Info($"  Migration: {settings.Name}");
    log.Info($"  Mode: {settings.Mode}");
    log.Info($"  DryRun: {settings.DryRun}");
    log.Info($"  Source: {SecretMasker.MaskConnectionString(sourceDb.ConnectionString)}");
    log.Info($"  Destination: {destFs.Type} → {destFs.BucketName}");
    log.Info($"  Log DB: {SecretMasker.MaskConnectionString(destDb.ConnectionString)}");
    log.Info($"  Separate Log Tables: {destDb.SeparateTablesPerSource}");
    log.Info($"  Parallel Downloads: {settings.ParallelDownloads}");
    log.Info($"  Rate Limit: {settings.RateLimitPerSecond}/sec");
    log.Info("");

    // Validate tables
    var tables = sourceDb.GetEffectiveTables();
    log.Info($"  📋 Tables to process: {tables.Count}");
    foreach (var t in tables)
        log.Info($"     - {t.TableName}.{t.UrlColumn} (id: {t.IdColumn})");
    log.Info("");

    // Init services
    var srcService = new SourceDbService(sourceDb);
    var destService = new DestinationDbService(destDb);

    // Ensure destination log tables exist (one per source table when SeparateTablesPerSource is true)
    if (!destService.AllTablesExist(tables))
    {
        log.Info("📦 Creating migration log table(s)...");
        var logTableNames = destService.GetAllLogTableNames(tables);
        foreach (var tbl in logTableNames)
            log.Info($"     → {tbl}");

        if (!settings.DryRun) destService.CreateAllTables(tables);
        log.Success("  ✅ Migration log table(s) ready.");
    }
    else if (settings.FreshStart)
    {
        log.Warning("  ⚠️ FreshStart: dropping and recreating log table(s)...");
        if (!settings.DryRun)
        {
            destService.DropAllTables(tables);
            destService.CreateAllTables(tables);
        }
    }

    // ROLLBACK mode
    if (settings.Mode.Equals("rollback", StringComparison.OrdinalIgnoreCase))
    {
        log.Header("\n🔄 ROLLBACK MODE");
        using var transfer = new FileTransferService(srcFs, destFs, settings.RateLimitPerSecond, settings.MaxFileSizeMB);
        var successRecs = destService.GetSuccessRecords(tables);
        log.Info($"  Found {successRecs.Count} successful records to rollback.");

        if (settings.DryRun) { log.Warning("  [DRY-RUN] No changes made."); return; }

        int rolled = 0;
        foreach (var (id, url) in successRecs)
        {
            if (ct.IsCancellationRequested) { log.Warning("  ⚠️ Cancelled."); break; }
            try { await transfer.DeleteAsync(url, ct); rolled++; }
            catch (Exception ex) { log.Error($"  ❌ Rollback failed for id={id}: {ex.Message}"); }
        }
        log.Success($"\n🎉 Rollback complete: {rolled}/{successRecs.Count} files deleted from destination.");
        return;
    }

    // MIGRATE mode
    log.Header("\n📦 STEP 1: Fetching source records...");
    var records = srcService.FetchRecords();
    log.Info($"  Found {records.Count} records with URLs.\n");

    if (records.Count == 0) { log.Warning("  No records to migrate."); return; }

    // Show per-table breakdown
    var groupedByTable = records.GroupBy(r => r.SourceTable);
    foreach (var group in groupedByTable)
        log.Info($"     📋 {group.Key}: {group.Count()} records");
    log.Info("");

    // Insert into log table(s) — uses INSERT IGNORE for resumability
    log.Header("📦 STEP 2: Registering records in migration log...");
    if (!settings.DryRun)
    {
        var inserted = destService.InsertBatch(records);
        log.Success($"  ✅ {inserted} new records registered (duplicates skipped).\n");
    }
    else log.Warning("  [DRY-RUN] Skipped.\n");

    // Transfer files
    log.Header("🔄 STEP 3: Transferring files...\n");
    var pending = settings.DryRun
        ? records.Select(r => new MediaRecord { Id = r.Id, SourceUrl = r.SourceUrl, SourceTable = r.SourceTable }).ToList()
        : destService.GetAllPendingRecords(tables, settings.MaxRetries);
    log.Info($"  Pending: {pending.Count} files\n");

    if (settings.DryRun)
    {
        foreach (var r in pending.Take(10))
            log.Info($"  [DRY-RUN] Would transfer ({r.SourceTable}): {r.SourceUrl[..Math.Min(80, r.SourceUrl.Length)]}...");
        if (pending.Count > 10) log.Info($"  ... and {pending.Count - 10} more.");
        log.Warning("\n  [DRY-RUN] No files transferred.");
        return;
    }

    using var transferService = new FileTransferService(srcFs, destFs, settings.RateLimitPerSecond, settings.MaxFileSizeMB);
    var progress = new ProgressTracker(pending.Count);
    var breaker = new CircuitBreaker(5, TimeSpan.FromSeconds(30), log);

    var semaphore = new SemaphoreSlim(settings.ParallelDownloads);
    var tasks = pending.Select(async record =>
    {
        if (ct.IsCancellationRequested) return;

        await semaphore.WaitAsync(ct);
        try
        {
            if (ct.IsCancellationRequested) return;

            if (!breaker.AllowRequest())
            {
                if (verbose) log.Warning($"  ⏸️ Circuit open, skipping id={record.Id}");
                return;
            }

            for (int attempt = 0; attempt < settings.MaxRetries; attempt++)
            {
                if (ct.IsCancellationRequested) return;

                try
                {
                    var (newUrl, size, hash) = await transferService.TransferAsync(record.SourceUrl, ct);
                    destService.MarkSuccess(record.Id, record.SourceTable, newUrl, size, hash);
                    breaker.RecordSuccess();
                    progress.RecordSuccess(size);
                    if (verbose) log.Success($"  ✅ [{record.SourceTable}] id={record.Id} → {newUrl}");
                    break;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt >= settings.MaxRetries - 1)
                    {
                        destService.MarkFailed(record.Id, record.SourceTable, ex.Message, settings.MaxRetries);
                        breaker.RecordFailure();
                        progress.RecordFailure();
                        if (verbose) log.Error($"  ❌ [{record.SourceTable}] id={record.Id} - {ex.Message}");
                    }
                    else
                    {
                        var delay = RetryPolicy.GetDelay(attempt);
                        await Task.Delay(delay, ct);
                    }
                }
            }
        }
        finally { semaphore.Release(); }
    });

    try
    {
        await Task.WhenAll(tasks);
    }
    catch (OperationCanceledException)
    {
        log.Warning("\n  ⚠️ Migration cancelled by user.");
    }

    // Summary
    log.Header("\n═══════════════════════════════════════════════════════");
    log.Header("📊 MIGRATION SUMMARY");
    log.Info(progress.GetSummary());
    var summary = destService.GetSummary(tables);
    foreach (var kv in summary) log.Info($"  {kv.Key}: {kv.Value}");

    // Update third DB if enabled
    if (thirdDb.Enabled && !ct.IsCancellationRequested)
    {
        log.Header("\n📦 Updating third-party database...");
        var thirdService = new ThirdDbService(thirdDb, log);
        var successRecs = destService.GetSuccessRecords(tables);
        var (ok, fail) = thirdService.UpdateWithTransaction(successRecs);
        log.Info($"  Third DB: {ok} updated, {fail} failed.");
    }

    log.Success($"\n🎉 Migration complete! Log: {log.LogPath}");

    if (progress.HasFailures)
    {
        log.Warning("  ⚠️ Some files failed. Re-run to retry pending records (resumable).");
        Environment.ExitCode = 1;
    }

}, dryRunOption, modeOption, configOption, verboseOption);

await root.InvokeAsync(args);

