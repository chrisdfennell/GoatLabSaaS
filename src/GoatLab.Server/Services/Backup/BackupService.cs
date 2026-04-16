using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Services.Backup;

public interface IBackupService
{
    Task RunAsync(CancellationToken cancellationToken);
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

    public BackupService(IOptions<BackupOptions> opts, IConfiguration config, ILogger<BackupService> logger)
    {
        _opts = opts.Value;
        _config = config;
        _logger = logger;
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
            return;
        }

        var connStr = _config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            _logger.LogError("Offsite backup aborted — no DefaultConnection configured");
            return;
        }

        var dbName = new SqlConnectionStringBuilder(connStr).InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
        {
            _logger.LogError("Offsite backup aborted — connection string has no database name");
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

        await using (var conn = new SqlConnection(connStr))
        {
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 600;
            cmd.CommandText = $"BACKUP DATABASE {quotedDb} TO DISK = @p WITH INIT, FORMAT, COMPRESSION;";
            cmd.Parameters.AddWithValue("@p", sqlFile);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!File.Exists(appFile))
        {
            _logger.LogError("BACKUP succeeded but file not visible at {Path}", appFile);
            return;
        }

        var s3 = BuildS3Client();
        var key = _opts.Prefix.TrimEnd('/') + "/" + fileName;

        try
        {
            await using var stream = File.OpenRead(appFile);
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _opts.Bucket,
                Key = key,
                InputStream = stream,
                DisablePayloadSigning = true,
            }, cancellationToken);
            _logger.LogInformation("Uploaded {Key} ({Size} bytes) to s3://{Bucket}",
                key, new FileInfo(appFile).Length, _opts.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Offsite upload failed for {Key}", key);
            return;
        }

        // Local retention: keep the local .bak so ad-hoc restores still work —
        // the nightly on-volume backups from ToolsController are already doing
        // local retention implicitly by overwriting. Don't double-manage here.

        if (_opts.RetentionDays > 0)
        {
            await PruneOldBackupsAsync(s3, cancellationToken);
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
