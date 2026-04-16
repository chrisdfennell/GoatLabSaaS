using GoatLab.Server.Data;
using GoatLab.Server.Services;
using GoatLab.Server.Services.Billing;
using GoatLab.Server.Services.Email;
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
    private readonly IConfiguration _config;

    public AdminHealthController(
        GoatLabDbContext db,
        IAppEmailSender emailSender,
        IOptions<SmtpOptions> smtp,
        IOptions<StripeOptions> stripe,
        IConfiguration config)
    {
        _db = db;
        _emailSender = emailSender;
        _smtp = smtp;
        _stripe = stripe;
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
}
