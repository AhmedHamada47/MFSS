using System.Net;
using Amazon.S3;
using MFSS.Abstractions;
using MFSS.Models;
using MFSS.Services;
using MFSS.StorageProviders;
using static MFSS.Models.MigrationStatus;

namespace MFSS.Orchestration;

public class MigrationOrchestrator
{
    private readonly MigrationSettings _settings;
    private readonly SourceDbConfig _sourceDb;
    private readonly FileSystemConfig _srcFs;
    private readonly FileSystemConfig _destFs;
    private readonly DestinationDbConfig _destDb;
    private readonly ThirdDbConfig _thirdDb;
    private readonly IEnvConfigResolver _envResolver;
    private readonly IConfigValidator _configValidator;
    private readonly ISourceDbService _srcService;
    private readonly IDestinationDbService _destService;
    private readonly bool _verbose;

    public MigrationOrchestrator(
        MigrationSettings settings,
        SourceDbConfig sourceDb,
        FileSystemConfig srcFs,
        FileSystemConfig destFs,
        DestinationDbConfig destDb,
        ThirdDbConfig thirdDb,
        IEnvConfigResolver envResolver,
        IConfigValidator configValidator,
        ISourceDbService? srcService = null,
        IDestinationDbService? destService = null,
        bool verbose = false)
    {
        _settings = settings;
        _sourceDb = sourceDb;
        _srcFs = srcFs;
        _destFs = destFs;
        _destDb = destDb;
        _thirdDb = thirdDb;
        _envResolver = envResolver;
        _configValidator = configValidator;
        _verbose = verbose;
        _srcService = srcService ?? new SourceDbService(sourceDb);
        _destService = destService ?? new DestinationDbService(destDb);
    }

    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        _envResolver.ResolveAll(_sourceDb, _destDb, _thirdDb, _srcFs, _destFs);

        var validationErrors = _configValidator.Validate(_settings, _sourceDb, _srcFs, _destFs, _destDb, _thirdDb);
        if (validationErrors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Configuration validation failed:");
            foreach (var error in validationErrors)
                Console.WriteLine($"   - {error}");
            Console.ResetColor();
            return 1;
        }

        using var log = new Logger(_settings.Name);
        PrintHeader(log);

        var tables = _sourceDb.GetEffectiveTables();

        if (!await EnsureLogTablesAsync(_destService, tables, log))
            return 1;

        if (_settings.Mode.Equals(RollbackMode, StringComparison.OrdinalIgnoreCase))
            return await ExecuteRollbackAsync(_destService, tables, log, ct);

        return await ExecuteMigrationAsync(_srcService, _destService, tables, log, ct);
    }

    private void PrintHeader(ILogger log)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        log.Header($"MFSS - Migration File Storage System v{version}");
        log.Header(new string('=', 50));
        log.Info($"  Migration: {_settings.Name}");
        log.Info($"  Mode: {_settings.Mode}");
        log.Info($"  DryRun: {_settings.DryRun}");
        log.Info($"  Source: {SecretMasker.MaskConnectionString(_sourceDb.ConnectionString)}");
        log.Info($"  Destination: {_destFs.Type} -> {_destFs.BucketName}");
        log.Info($"  Log DB: {SecretMasker.MaskConnectionString(_destDb.ConnectionString)}");
        log.Info($"  Separate Log Tables: {_destDb.SeparateTablesPerSource}");
        log.Info($"  Parallel Downloads: {_settings.ParallelDownloads}");
        log.Info($"  Rate Limit: {_settings.RateLimitPerSecond}/sec");

        var tables = _sourceDb.GetEffectiveTables();
        log.Info($"  Tables to process: {tables.Count}");
        foreach (var t in tables)
            log.Info($"     - {t.TableName}.{t.UrlColumn} (id: {t.IdColumn})");
    }

    private async Task<bool> EnsureLogTablesAsync(IDestinationDbService destService, List<SourceTableConfig> tables, ILogger log)
    {
        log.Info("Checking migration log table(s)...");
        if (!await destService.AllTablesExistAsync(tables))
        {
            log.Info("Creating migration log table(s)...");
            var logTableNames = await destService.GetAllLogTableNamesAsync(tables);
            foreach (var tbl in logTableNames)
                log.Info($"     -> {tbl}");

            if (!_settings.DryRun)
            {
                await destService.CreateAllTablesAsync(tables);
                log.Success("Migration log table(s) ready.");
            }
            else
            {
                log.Warning("  [DRY-RUN] Table creation skipped.");
            }
        }
        else if (_settings.FreshStart)
        {
            log.Warning("FreshStart: dropping and recreating log table(s)...");
            if (!_settings.DryRun)
            {
                await destService.DropAllTablesAsync(tables);
                await destService.CreateAllTablesAsync(tables);
                log.Success("Log tables recreated.");
            }
        }
        else
        {
            log.Success("All log tables exist.");
        }

        return true;
    }

    private async Task<int> ExecuteRollbackAsync(IDestinationDbService destService, List<SourceTableConfig> tables, ILogger log, CancellationToken ct)
    {
        log.Header("ROLLBACK MODE");

        using var factory = new StorageProviderFactory();
        var destProvider = factory.CreateDestinationProvider(_destFs);
        using var transfer = new FileTransferService(
            null!, destProvider, _settings.RateLimitPerSecond, _settings.MaxFileSizeMB);

        var successRecs = await destService.GetSuccessRecordsAsync(tables);
        log.Info($"  Found {successRecs.Count} successful records to rollback.");

        if (_settings.DryRun) { log.Warning("  [DRY-RUN] No changes made."); return 0; }

        int rolled = 0;
        foreach (var (id, url) in successRecs)
        {
            if (ct.IsCancellationRequested) { log.Warning("  Cancelled."); break; }
            try { await transfer.DeleteAsync(url, ct); rolled++; }
            catch (Exception ex) { log.Error($"  Rollback failed for id={id}: {ex.Message}"); }
        }
        log.Success($"Rollback complete: {rolled}/{successRecs.Count} files deleted from destination.");
        return rolled == successRecs.Count ? 0 : 1;
    }

    private async Task<int> ExecuteMigrationAsync(
        ISourceDbService srcService,
        IDestinationDbService destService,
        List<SourceTableConfig> tables,
        ILogger log,
        CancellationToken ct)
    {
        log.Header("STEP 1: Fetching source records...");
        var records = await srcService.FetchRecordsAsync(ct);
        log.Info($"  Found {records.Count} records with URLs.");

        if (records.Count == 0) { log.Warning("  No records to migrate."); return 0; }

        var groupedByTable = records.GroupBy(r => r.SourceTable);
        foreach (var group in groupedByTable)
            log.Info($"     - {group.Key}: {group.Count()} records");

        log.Header("STEP 2: Registering records in migration log...");
        if (!_settings.DryRun)
        {
            var inserted = await destService.InsertBatchAsync(records.ToList());
            log.Success($"  {inserted} new records registered (duplicates updated).");

            log.Info("  Checking for source URL changes...");
            var urlResets = await destService.DetectAndResetUrlChangesAsync(records.ToList());
            if (urlResets > 0)
                log.Warning($"  {urlResets} records had URL changes and were reset to pending.");
            else
                log.Success("  No URL changes detected.");
        }
        else log.Warning("  [DRY-RUN] Skipped.");

        log.Header("STEP 3: Transferring files...");
        var pending = _settings.DryRun
            ? records.Select(r => new MediaRecord { Id = r.Id, SourceUrl = r.SourceUrl, SourceTable = r.SourceTable }).ToList()
            : await destService.GetAllPendingRecordsAsync(tables, _settings.MaxRetries);
        log.Info($"  Pending: {pending.Count} files");

        if (_settings.DryRun)
        {
            foreach (var r in pending.Take(10))
                log.Info($"  [DRY-RUN] Would transfer ({r.SourceTable}): {TruncateUrl(r.SourceUrl)}...");
            if (pending.Count > 10) log.Info($"  ... and {pending.Count - 10} more.");
            log.Warning("  [DRY-RUN] No files transferred.");
            return 0;
        }

        return await TransferFilesAsync(destService, tables, pending, log, ct);
    }

    private async Task<int> TransferFilesAsync(
        IDestinationDbService destService,
        List<SourceTableConfig> tables,
        List<MediaRecord> pending,
        ILogger log,
        CancellationToken ct)
    {
        using var factory = new StorageProviderFactory();
        var sourceProvider = factory.CreateSourceProvider(_srcFs);
        var destProvider = factory.CreateDestinationProvider(_destFs);

        using var transferService = new FileTransferService(
            sourceProvider, destProvider, _settings.RateLimitPerSecond, _settings.MaxFileSizeMB);

        var progress = new ProgressTracker(pending.Count) as IProgressTracker;
        var breaker = new CircuitBreaker(_settings.CircuitBreakerThreshold, TimeSpan.FromSeconds(_settings.CircuitBreakerTimeoutSeconds), log) as ICircuitBreaker;

        var semaphore = new SemaphoreSlim(_settings.ParallelDownloads);
        var tasks = pending.Select(async record =>
        {
            if (ct.IsCancellationRequested) return;
            await semaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return;
                if (!breaker.AllowRequest())
                {
                    if (_verbose) log.Warning($"  Circuit open, skipping id={record.Id}");
                    return;
                }

                for (int attempt = 0; attempt < _settings.MaxRetries; attempt++)
                {
                    if (ct.IsCancellationRequested) return;
                    try
                    {
                        (string newUrl, long size, string hash) = await transferService.TransferAsync(record.SourceUrl, ct);

                        var existing = await destService.FindExistingByHashAsync(hash, tables);
                        if (existing != null && existing.Value.DestinationUrl != null)
                        {
                            await destService.MarkSuccessAsync(record.Id, record.SourceTable, existing.Value.DestinationUrl, existing.Value.FileSize ?? size, hash);
                            breaker.RecordSuccess();
                            progress.RecordSuccess(existing.Value.FileSize ?? size);
                            try { await transferService.DeleteAsync(newUrl, ct); } catch { }
                            if (_verbose) log.Info($"  [{record.SourceTable}] id={record.Id} deduplicated (hash match: {hash[..8]})");
                            break;
                        }

                        await destService.MarkSuccessAsync(record.Id, record.SourceTable, newUrl, size, hash);
                        breaker.RecordSuccess();
                        progress.RecordSuccess(size);
                        if (_verbose) log.Success($"  [{record.SourceTable}] id={record.Id} -> {newUrl}");
                        break;
                    }
                    catch (OperationCanceledException) { return; }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        await destService.MarkFailedAsync(record.Id, record.SourceTable, $"Source URL returned 404 (Not Found) — file no longer exists at origin: {record.SourceUrl}", _settings.MaxRetries);
                        breaker.RecordFailure();
                        progress.RecordFailure();
                        if (_verbose) log.Error($"  [{record.SourceTable}] id={record.Id} - 404: Source file deleted from origin");
                        break;
                    }
                    catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
                    {
                        await destService.MarkFailedAsync(record.Id, record.SourceTable, $"S3 key not found in bucket '{_destFs.BucketName}': {ex.Message}", _settings.MaxRetries);
                        breaker.RecordFailure();
                        progress.RecordFailure();
                        if (_verbose) log.Error($"  [{record.SourceTable}] id={record.Id} - S3 key missing: {ex.Message}");
                        break;
                    }
                    catch (Amazon.S3.AmazonS3Exception ex) when (ex.Message.Contains("STREAMING"))
                    {
                        await destService.MarkFailedAsync(record.Id, record.SourceTable, $"R2 upload failed — chunked encoding not supported. Fix: ensure DisablePayloadSigning=true in S3 config. {ex.Message}", _settings.MaxRetries);
                        breaker.RecordFailure();
                        progress.RecordFailure();
                        if (_verbose) log.Error($"  [{record.SourceTable}] id={record.Id} - R2 upload config error: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (attempt >= _settings.MaxRetries - 1)
                        {
                            await destService.MarkFailedAsync(record.Id, record.SourceTable, $"[{ex.GetType().Name}] {ex.Message}", _settings.MaxRetries);
                            breaker.RecordFailure();
                            progress.RecordFailure();
                            if (_verbose) log.Error($"  [{record.SourceTable}] id={record.Id} - {ex.GetType().Name}: {ex.Message}");
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

        var progressTask = !_verbose ? RenderProgressBarAsync(progress, log, ct) : Task.CompletedTask;

        try { await Task.WhenAll(tasks); }
        catch (OperationCanceledException)
        {
            log.Warning("  Migration cancelled by user.");
        }
        finally
        {
            if (!_verbose)
            {
                await Task.WhenAny(progressTask, Task.Delay(100, ct));
                log.Progress("");
                log.Info(FormatProgressLine(progress));
            }
        }

        await PrintSummaryAsync(destService, tables, progress, log, ct);
        return progress.HasFailures ? 1 : 0;
    }

    private static async Task RenderProgressBarAsync(IProgressTracker progress, ILogger log, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            log.Progress(FormatProgressLine(progress));
            await Task.Delay(500, ct);
        }
    }

    private static string FormatProgressLine(IProgressTracker progress)
    {
        int w; try { w = Console.WindowWidth; } catch { w = 80; }
        var barLen = Math.Max(10, w - 45);
        var done = progress.Success + progress.Failed;
        var pct = progress.Total > 0 ? (double)done / progress.Total : 0;
        var filled = (int)(pct * barLen);
        var bar = new string('=', filled) + new string(' ', barLen - filled);
        var mb = progress.TotalBytes / (1024.0 * 1024.0);
        return $"[{bar}] {done}/{progress.Total}  {mb:F1}MB";
    }

    private async Task PrintSummaryAsync(
        IDestinationDbService destService,
        List<SourceTableConfig> tables,
        IProgressTracker progress,
        ILogger log,
        CancellationToken ct)
    {
        log.Header(new string('=', 50));
        log.Header("MIGRATION SUMMARY");
        log.Info(progress.GetSummary());
        var summary = await destService.GetSummaryAsync(tables);
        foreach (var kv in summary) log.Info($"  {kv.Key}: {kv.Value}");

        if (_thirdDb.Enabled && !ct.IsCancellationRequested)
        {
            log.Header("Updating third-party database...");
            var thirdService = new ThirdDbService(_thirdDb, log) as IThirdDbService;
            var successRecs = await destService.GetSuccessRecordsAsync(tables);
            var (ok, fail) = await thirdService.UpdateWithTransactionAsync(successRecs, ct);
            log.Info($"  Third DB: {ok} updated, {fail} failed.");
        }

        log.Success($"Migration complete! Log: {log.LogPath}");
        if (progress.HasFailures)
            log.Warning("  Some files failed. Re-run to retry pending records (resumable).");
    }

    private static string TruncateUrl(string url, int maxLen = 80)
    {
        return url.Length <= maxLen ? url : url[..maxLen];
    }
}
