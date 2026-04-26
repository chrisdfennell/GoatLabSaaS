using Amazon.S3;
using Amazon.S3.Model;
using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Services.Backup;

public interface IBackupService
{
    Task RunAsync(CancellationToken cancellationToken);
}

// AppSettings keys used for backup status so /admin/health can surface "last
// success" / "last error" without a separate table. Read by AdminHealthController.
public static class BackupStatusKeys
{
    public const string LastSuccessAt = "backup.offsite.lastSuccessAt";
    public const string LastFileName = "backup.offsite.lastFileName";
    public const string LastSizeBytes = "backup.offsite.lastSizeBytes";
    public const string LastError = "backup.offsite.lastError";
    public const string LastErrorAt = "backup.offsite.lastErrorAt";
}

// Daily offsite backup: runs BACKUP DATABASE to the shared Hangfire/backups
// volume, uploads the resulting .bak to S3-compatible storage, then prunes
// objects older than RetentionDays from the configured prefix. ToolsController
// reuses the same path layout for ad-hoc admin backups; this job shares the
// naming scheme so both appear together in the bucket.
public class BackupService : IBackupService
{
    private readonly BackupOptions _opts;
    private readonly IConfiguration _config;
    private readonly ILogger<BackupService> _logger;
    private readonly GoatLabDbContext _db;

    public BackupService(
        IOptions<BackupOptions> opts,
        IConfiguration config,
        ILogger<BackupService> logger,
        GoatLabDbContext db)
    {
        _opts = opts.Value;
        _config = config;
        _logger = logger;
        _db = db;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_opts.Enabled)
        {
            _logger.LogInformation("Offsite backup skipped — Backup:Offsite:Enabled is false");
            return;
        }
        if (string.IsNullOrWhiteSpace(_opts.Bucket) || string.IsNullOrWhiteSpace(_opts.AccessKey))
        {
            _logger.LogWarning("Offsite backup misconfigured — missing bucket or access key");
            await RecordErrorAsync("Misconfigured: missing bucket or access key", cancellationToken);
            return;
        }

        var connStr = _config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            _logger.LogError("Offsite backup aborted — no DefaultConnection configured");
            await RecordErrorAsync("No DefaultConnection configured", cancellationToken);
            return;
        }

        var dbName = new SqlConnectionStringBuilder(connStr).InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
        {
            _logger.LogError("Offsite backup aborted — connection string has no database name");
            await RecordErrorAsync("Connection string has no database name", cancellationToken);
            return;
        }

        var appBackupDir = _config.GetValue<string>("Backup:AppPath") ?? "/app/backups";
        var sqlBackupDir = _config.GetValue<string>("Backup:SqlServerPath") ?? appBackupDir;
        Directory.CreateDirectory(appBackupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{dbName}-offsite-{timestamp}.bak";
        var sqlFile = Path.Combine(sqlBackupDir, fileName);
        var appFile = Path.Combine(appBackupDir, fileName);
        var quotedDb = $"[{dbName.Replace("]", "]]")}]";

        _logger.LogInformation("Offsite backup starting: {File}", fileName);

        try
        {
            await using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(cancellationToken);
                await using var cmd = conn.CreateCommand();
                cmd.CommandTimeout = 600;
                cmd.CommandText = $"BACKUP DATABASE {quotedDb} TO DISK = @p WITH INIT, FORMAT, COMPRESSION;";
                cmd.Parameters.AddWithValue("@p", sqlFile);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BACKUP DATABASE failed");
            await RecordErrorAsync($"BACKUP DATABASE failed: {ex.Message}", cancellationToken);
            return;
        }

        if (!File.Exists(appFile))
        {
            _logger.LogError("BACKUP succeeded but file not visible at {Path}", appFile);
            await RecordErrorAsync($"Backup file not visible at {appFile}", cancellationToken);
            return;
        }

        var s3 = BuildS3Client();
        var key = _opts.Prefix.TrimEnd('/') + "/" + fileName;
        long sizeBytes;

        try
        {
            sizeBytes = new FileInfo(appFile).Length;
            await using var stream = File.OpenRead(appFile);
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _opts.Bucket,
                Key = key,
                InputStream = stream,
                DisablePayloadSigning = true,
            }, cancellationToken);
            _logger.LogInformation("Uploaded {Key} ({Size} bytes) to s3://{Bucket}",
                key, sizeBytes, _opts.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offsite upload failed for {Key}", key);
            await RecordErrorAsync($"Upload failed: {ex.Message}", cancellationToken);
            return;
        }

        // Local retention: keep the local .bak so ad-hoc restores still work —
        // the nightly on-volume backups from ToolsController are already doing
        // local retention implicitly by overwriting. Don't double-manage here.

        if (_opts.RetentionDays > 0)
        {
            try { await PruneOldBackupsAsync(s3, cancellationToken); }
            catch (Exception ex)
            {
                // Pruning failure is non-fatal — the upload itself succeeded, so
                // record success but log the prune problem separately.
                _logger.LogWarning(ex, "Offsite backup prune failed (upload succeeded)");
            }
        }

        await RecordSuccessAsync(fileName, sizeBytes, cancellationToken);
    }

    // Stamp success: clears LastError + LastErrorAt so the health check flips
    // back to ok after a transient failure recovers.
    private async Task RecordSuccessAsync(string fileName, long sizeBytes, CancellationToken ct)
    {
        try
        {
            await UpsertSettingAsync(BackupStatusKeys.LastSuccessAt,
                DateTime.UtcNow.ToString("O"), ct);
            await UpsertSettingAsync(BackupStatusKeys.LastFileName, fileName, ct);
            await UpsertSettingAsync(BackupStatusKeys.LastSizeBytes, sizeBytes.ToString(), ct);
            await UpsertSettingAsync(BackupStatusKeys.LastError, null, ct);
            await UpsertSettingAsync(BackupStatusKeys.LastErrorAt, null, ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record backup success state");
        }
    }

    private async Task RecordErrorAsync(string error, CancellationToken ct)
    {
        try
        {
            // Truncate to the AppSetting Value MaxLength (2000).
            var trimmed = error.Length > 1900 ? error[..1900] : error;
            await UpsertSettingAsync(BackupStatusKeys.LastError, trimmed, ct);
            await UpsertSettingAsync(BackupStatusKeys.LastErrorAt,
                DateTime.UtcNow.ToString("O"), ct);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record backup error state");
        }
    }

    private async Task UpsertSettingAsync(string key, string? value, CancellationToken ct)
    {
        var row = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }

    private IAmazonS3 BuildS3Client()
    {
        var creds = new Amazon.Runtime.BasicAWSCredentials(_opts.AccessKey, _opts.SecretKey);
        var cfg = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_opts.Region),
            ForcePathStyle = true, // friendliest for non-AWS S3 gateways
        };
        if (!string.IsNullOrWhiteSpace(_opts.Endpoint))
            cfg.ServiceURL = _opts.Endpoint;
        return new AmazonS3Client(creds, cfg);
    }

    private async Task PruneOldBackupsAsync(IAmazonS3 s3, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_opts.RetentionDays);
        string? continuationToken = null;
        var deleted = 0;

        do
        {
            var list = await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _opts.Bucket,
                Prefix = _opts.Prefix,
                ContinuationToken = continuationToken,
            }, ct);

            foreach (var obj in list.S3Objects)
            {
                if (obj.LastModified < cutoff)
                {
                    await s3.DeleteObjectAsync(_opts.Bucket, obj.Key, ct);
                    deleted++;
                }
            }

            continuationToken = list.IsTruncated == true ? list.NextContinuationToken : null;
        } while (continuationToken is not null);

        if (deleted > 0)
            _logger.LogInformation("Pruned {Count} backup(s) older than {Days} days", deleted, _opts.RetentionDays);
    }
}
