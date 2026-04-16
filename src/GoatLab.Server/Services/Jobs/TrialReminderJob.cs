using GoatLab.Server.Data;
using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services.Email;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Jobs;

// Runs once a day via Hangfire. Finds tenants whose trial ends in <= 3 days,
// emails the tenant owner, and stamps TrialReminderSentAt so we don't resend.
// Cleared in the Stripe webhook when TrialEndsAt changes (resubscription).
public class TrialReminderJob
{
    private readonly GoatLabDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAppEmailSender _email;
    private readonly ILogger<TrialReminderJob> _logger;
    private readonly IConfiguration _config;

    public TrialReminderJob(
        GoatLabDbContext db,
        UserManager<ApplicationUser> userManager,
        IAppEmailSender email,
        ILogger<TrialReminderJob> logger,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _email = email;
        _logger = logger;
        _config = config;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(3);

        // Candidates: active trial ending within 3 days, no reminder yet this cycle.
        var candidates = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null
                     && t.SuspendedAt == null
                     && t.TrialEndsAt != null
                     && t.TrialEndsAt > now
                     && t.TrialEndsAt <= cutoff
                     && t.TrialReminderSentAt == null)
            .Include(t => t.Plan)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Trial reminder sweep: {Count} tenants due", candidates.Count);
        if (candidates.Count == 0) return;

        var publicUrl = _config.GetValue<string>("App:PublicUrl") ?? "";

        foreach (var tenant in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Find an owner to email. Prefer the first Owner; fall back to any member.
            var ownerUserId = await _db.TenantMembers
                .IgnoreQueryFilters()
                .Where(m => m.TenantId == tenant.Id && m.Role == TenantRole.Owner)
                .OrderBy(m => m.JoinedAt)
                .Select(m => m.UserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (ownerUserId is null) continue;

            var user = await _userManager.FindByIdAsync(ownerUserId);
            if (user?.Email is null || user.DeletedAt is not null) continue;

            var daysRemaining = Math.Max(1, (int)Math.Ceiling((tenant.TrialEndsAt!.Value - now).TotalDays));
            var billingUrl = string.IsNullOrEmpty(publicUrl) ? "/billing" : $"{publicUrl.TrimEnd('/')}/billing";
            var tpl = EmailTemplates.TrialEnding(user.DisplayName, tenant.Plan?.Name ?? "trial", daysRemaining, billingUrl);

            try
            {
                await _email.SendAsync(user.Email, tpl.Subject, tpl.Html, tpl.Text, cancellationToken);
                tenant.TrialReminderSentAt = now;
                _logger.LogInformation("Trial reminder sent: tenant {TenantId} user {UserId} days {Days}",
                    tenant.Id, ownerUserId, daysRemaining);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Trial reminder failed for tenant {TenantId}", tenant.Id);
                // Leave TrialReminderSentAt null so we retry tomorrow.
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
