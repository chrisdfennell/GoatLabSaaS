using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Backup;
using GoatLab.Server.Services.Billing;
using GoatLab.Server.Services.Email;
using GoatLab.Server.Services.Jobs;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoatLab.Server.Controllers;

// Admin-only diagnostics. NOT unit-test results — those live in CI. This is
// live system health: can the app reach its dependencies, are jobs running on
// schedule, is the mail server actually configured. Renders as /admin/health.
[ApiController]
[Route("api/admin/health")]
[Authorize(Policy = SuperAdminPolicy.Name)]
public class AdminHealthController : ControllerBase
{
    private readonly GoatLabDbContext _db;
    private readonly IAppEmailSender _emailSender;
    private readonly IOptions<SmtpOptions> _smtp;
    private readonly IOptions<StripeOptions> _stripe;
    private readonly IOptions<BackupOptions> _backup;
    private readonly IConfiguration _config;

    public AdminHealthController(
        GoatLabDbContext db,
        IAppEmailSender emailSender,
        IOptions<SmtpOptions> smtp,
        IOptions<StripeOptions> stripe,
        IOptions<BackupOptions> backup,
        IConfiguration config)
    {
        _db = db;
        _emailSender = emailSender;
        _smtp = smtp;
        _stripe = stripe;
        _backup = backup;
        _config = config;
    }

    public record CheckDto(string Name, string Status, string? Detail);
    public record RecurringJobDto(string Id, string Cron, DateTime? LastExecution, DateTime? NextExecution, string? LastJobState);
    public record HealthReportDto(
        List<CheckDto> Checks,
        List<RecurringJobDto> Jobs,
        DateTime GeneratedAtUtc);

    [HttpGet]
    public async Task<ActionResult<HealthReportDto>> Get(CancellationToken ct)
    {
        var checks = new List<CheckDto>();
        var jobs = new List<RecurringJobDto>();

        // Database connectivity — a tiny round trip.
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(ct);
            checks.Add(new CheckDto("Database", canConnect ? "ok" : "error",
                canConnect ? "Connected" : "Cannot connect"));
        }
        catch (Exception ex)
        {
            checks.Add(new CheckDto("Database", "error", ex.Message));
        }

        // Pending migrations (stale schema detector).
        try
        {
            var pending = await _db.Database.GetPendingMigrationsAsync(ct);
            var list = pending.ToList();
            checks.Add(new CheckDto("Migrations",
                list.Count == 0 ? "ok" : "warn",
                list.Count == 0 ? "No pending" : $"{list.Count} pending: {string.Join(", ", list)}"));
        }
        catch (Exception ex)
        {
            checks.Add(new CheckDto("Migrations", "error", ex.Message));
        }

        // SMTP: we don't send a test message here (would spam), just report
        // whether the real sender is wired up or we're using NullEmailSender.
        var smtpConfigured = !string.IsNullOrWhiteSpace(_smtp.Value.Host);
        var senderKind = _emailSender.GetType().Name;
        checks.Add(new CheckDto(
            "SMTP",
            smtpConfigured ? "ok" : "warn",
            smtpConfigured
                ? $"Host={_smtp.Value.Host}, From={_smtp.Value.FromAddress} ({senderKind})"
                : $"No Smtp:Host set — using {senderKind} (emails are dropped)"));

        // Stripe: key presence only — hitting Stripe on every check is wasteful.
        var stripeConfigured = !string.IsNullOrWhiteSpace(_stripe.Value.SecretKey)
                            && !string.IsNullOrWhiteSpace(_stripe.Value.WebhookSecret);
        checks.Add(new CheckDto(
            "Stripe",
            stripeConfigured ? "ok" : "warn",
            stripeConfigured ? "Secret + webhook secret configured" : "Missing SecretKey or WebhookSecret"));

        // Sentry
        var sentryConfigured = !string.IsNullOrWhiteSpace(_config.GetValue<string>("Sentry:Dsn"));
        checks.Add(new CheckDto(
            "Sentry",
            sentryConfigured ? "ok" : "warn",
            sentryConfigured ? "DSN configured" : "No Sentry:Dsn — error events are not sent"));

        // Offsite backup. Stamps written by BackupService on success/failure;
        // we map them to ok/warn/error so a stale or failed backup is loud on
        // this page even though the Hangfire job itself "ran" (it ran, then
        // returned early because Enabled=false or the upload threw).
        checks.Add(BuildOffsiteBackupCheck());

        // Hangfire: recurring-job registry + last run.
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            var recurring = connection.GetRecurringJobs();
            foreach (var rj in recurring)
            {
                jobs.Add(new RecurringJobDto(
                    rj.Id,
                    rj.Cron,
                    rj.LastExecution,
                    rj.NextExecution,
                    rj.LastJobState));
            }
            checks.Add(new CheckDto("Hangfire",
                recurring.Count > 0 ? "ok" : "warn",
                $"{recurring.Count} recurring job(s) registered"));
        }
        catch (Exception ex)
        {
            checks.Add(new CheckDto("Hangfire", "error", ex.Message));
        }

        return new HealthReportDto(checks, jobs, DateTime.UtcNow);
    }

    public record RunBackupResultDto(bool Queued, string? JobId, string? Message);

    // Manual fire of the offsite backup. Enqueues a Hangfire background job so
    // the controller returns immediately — a real BACKUP DATABASE on a
    // production-sized DB takes minutes, not seconds. The returned JobId can
    // be inspected at /admin/jobs.
    [HttpPost("backup/run")]
    public ActionResult<RunBackupResultDto> RunBackupNow()
    {
        if (!_backup.Value.Enabled)
        {
            return BadRequest(new RunBackupResultDto(false, null,
                "Offsite backup is disabled. Set BACKUP_OFFSITE_ENABLED=true and configure bucket credentials first."));
        }
        if (string.IsNullOrWhiteSpace(_backup.Value.Bucket) || string.IsNullOrWhiteSpace(_backup.Value.AccessKey))
        {
            return BadRequest(new RunBackupResultDto(false, null,
                "Offsite backup is misconfigured — missing bucket or access key."));
        }

        var jobId = BackgroundJob.Enqueue<DatabaseBackupJob>(job => job.RunAsync(CancellationToken.None));
        return new RunBackupResultDto(true, jobId, "Backup queued. Check /admin/jobs or refresh this page in a few minutes.");
    }

    // Builds the offsite-backup health card. Read-only; uses _db.AppSettings
    // rows that BackupService stamps on every success/failure.
    private CheckDto BuildOffsiteBackupCheck()
    {
        if (!_backup.Value.Enabled)
        {
            return new CheckDto("Offsite backup", "warn",
                "Disabled — set Backup:Offsite:Enabled=true (env BACKUP_OFFSITE_ENABLED=true) and configure bucket credentials. " +
                "Without offsite backups, a host failure is unrecoverable.");
        }

        var settings = _db.AppSettings
            .Where(s => s.Key == BackupStatusKeys.LastSuccessAt
                     || s.Key == BackupStatusKeys.LastFileName
                     || s.Key == BackupStatusKeys.LastSizeBytes
                     || s.Key == BackupStatusKeys.LastError
                     || s.Key == BackupStatusKeys.LastErrorAt)
            .ToDictionary(s => s.Key, s => s.Value);

        DateTime? lastSuccess = TryParseUtc(settings.GetValueOrDefault(BackupStatusKeys.LastSuccessAt));
        DateTime? lastError = TryParseUtc(settings.GetValueOrDefault(BackupStatusKeys.LastErrorAt));
        var lastFile = settings.GetValueOrDefault(BackupStatusKeys.LastFileName);
        var lastSize = settings.GetValueOrDefault(BackupStatusKeys.LastSizeBytes);
        var lastErrMsg = settings.GetValueOrDefault(BackupStatusKeys.LastError);

        var bucketLabel = $"s3://{_backup.Value.Bucket}/{_backup.Value.Prefix.TrimEnd('/')}";

        // Brand-new install — enabled but never ran.
        if (lastSuccess is null && lastError is null)
        {
            return new CheckDto("Offsite backup", "warn",
                $"Configured for {bucketLabel} but has not run yet. Daily at 04:00 UTC, or click \"Run now\".");
        }

        // Most recent activity was a failure that hasn't recovered.
        if (lastError is not null && (lastSuccess is null || lastError > lastSuccess))
        {
            var ageHours = (int)(DateTime.UtcNow - lastError.Value).TotalHours;
            return new CheckDto("Offsite backup", "error",
                $"Last attempt failed {ageHours}h ago: {lastErrMsg}. Target: {bucketLabel}.");
        }

        // Last success exists. Stale if older than 36h (job runs daily).
        var age = DateTime.UtcNow - lastSuccess!.Value;
        var sizeText = long.TryParse(lastSize, out var bytes) ? FormatBytes(bytes) : "?";
        var detail = $"Last success: {lastSuccess.Value:yyyy-MM-dd HH:mm} UTC ({(int)age.TotalHours}h ago). " +
                     $"File: {lastFile} ({sizeText}). Target: {bucketLabel}.";

        return age.TotalHours > 36
            ? new CheckDto("Offsite backup", "error", "Stale — " + detail)
            : new CheckDto("Offsite backup", "ok", detail);
    }

    private static DateTime? TryParseUtc(string? raw)
        => DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;

    private static string FormatBytes(long bytes)
    {
        const long mb = 1024 * 1024;
        if (bytes >= 1024 * mb) return $"{bytes / (double)(1024 * mb):F1} GB";
        if (bytes >= mb) return $"{bytes / (double)mb:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
