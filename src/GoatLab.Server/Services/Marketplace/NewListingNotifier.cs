using GoatLab.Server.Data;
using GoatLab.Server.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Marketplace;

// Fans out a "new listing" email to every active follower of a tenant when
// one of its goats flips to IsListedForSale=true. Inline-iterates because
// follower counts per farm are expected to be small (single to low-double
// digits) — switch to Hangfire if any farm passes ~200 followers.
//
// Tenant filter is bypassed because GoatsController fires this AFTER its own
// SaveChanges and Sentry/audit logging; the controller already verified the
// goat is in the user's tenant.
public class NewListingNotifier
{
    private readonly GoatLabDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _config;
    private readonly ILogger<NewListingNotifier> _logger;

    public NewListingNotifier(
        GoatLabDbContext db,
        IAppEmailSender email,
        ITenantContext tenantContext,
        IConfiguration config,
        ILogger<NewListingNotifier> logger)
    {
        _db = db;
        _email = email;
        _tenantContext = tenantContext;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyAsync(int goatId, CancellationToken ct = default)
    {
        _tenantContext.BypassFilter = true;

        var goat = await _db.Goats.IgnoreQueryFilters()
            .Where(g => g.Id == goatId && g.IsListedForSale && !g.IsExternal)
            .Select(g => new
            {
                g.Id, g.Name, g.Breed, g.Gender, g.AskingPriceCents,
                TenantId = g.TenantId,
                TenantSlug = g.Tenant!.Slug,
                TenantName = g.Tenant.Name,
                PublicProfileEnabled = g.Tenant.PublicProfileEnabled,
                TenantDeleted = g.Tenant.DeletedAt != null,
                TenantSuspended = g.Tenant.SuspendedAt != null,
                PrimaryPhoto = g.Photos
                    .OrderByDescending(p => p.IsPrimary)
                    .ThenBy(p => p.UploadedAt)
                    .Select(p => p.FilePath)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (goat is null) return;
        if (!goat.PublicProfileEnabled || goat.TenantDeleted || goat.TenantSuspended)
        {
            // Don't email about a goat that won't be visible when the user
            // clicks the link.
            return;
        }

        var followers = await _db.FarmFollowers.IgnoreQueryFilters()
            .Where(f => f.TenantId == goat.TenantId && f.IsActive)
            .Select(f => new { f.Id, f.Email, f.UnsubscribeToken })
            .ToListAsync(ct);

        if (followers.Count == 0) return;

        var origin = (_config["PublicOrigin"] ?? "https://goatlab.app").TrimEnd('/');
        var listingUrl = $"{origin}/pub/{goat.TenantSlug}/{goat.Id}";
        var photoUrl = string.IsNullOrEmpty(goat.PrimaryPhoto) ? null : origin + "/" + goat.PrimaryPhoto;

        var idsToStamp = new List<int>();
        foreach (var f in followers)
        {
            try
            {
                var unsub = $"{origin}/follow/unsubscribe?token={f.UnsubscribeToken}";
                var (subject, html, text) = EmailTemplates.NewListingNotification(
                    goat.TenantName, goat.Name, goat.Breed, goat.Gender.ToString(),
                    goat.AskingPriceCents, photoUrl, listingUrl, unsub);
                await _email.SendAsync(f.Email, subject, html, text, ct);
                idsToStamp.Add(f.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify follower {Id} of new listing {GoatId}", f.Id, goat.Id);
            }
        }

        if (idsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            await _db.FarmFollowers.IgnoreQueryFilters()
                .Where(f => idsToStamp.Contains(f.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.LastNotifiedAt, now), ct);
        }
    }
}
