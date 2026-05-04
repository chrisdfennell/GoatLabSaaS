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
        // Bracket the cross-tenant work so the flag is always restored, even
        // on early returns or throws. Otherwise the next service in the same
        // scope would silently skip tenant filtering.
        _tenantContext.BypassFilter = true;
        try
        {
            await NotifyInternalAsync(goatId, ct);
        }
        finally
        {
            _tenantContext.BypassFilter = false;
        }
    }

    private async Task NotifyInternalAsync(int goatId, CancellationToken ct)
    {
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

        // ---- Saved-search marketplace alerts ----
        // Fan out to anonymous buyers who registered a search criteria that
        // matches this listing. Goat data is already loaded above.

        var goatBreedSlug = GoatLab.Server.Controllers.PublicController.BreedSlug(goat.Breed);
        var sexStr = goat.Gender.ToString();

        // Coarse SQL prefilter: by breed slug. Sex / price / state are checked
        // in-memory because they involve nullable conditionals and case-insensitive
        // substring matching that's awkward in EF.
        var alertCandidates = await _db.MarketplaceAlerts.IgnoreQueryFilters()
            .Where(a => a.IsActive && (a.BreedSlug == null || a.BreedSlug == goatBreedSlug))
            .Select(a => new
            {
                a.Id, a.Email, a.UnsubscribeToken,
                a.BreedSlug, a.Sex, a.MinPriceCents, a.MaxPriceCents, a.StateMatch
            })
            .ToListAsync(ct);

        var alertIdsToStamp = new List<int>();
        foreach (var a in alertCandidates)
        {
            // Sex filter
            if (!string.IsNullOrEmpty(a.Sex)
                && !string.Equals(a.Sex, sexStr, StringComparison.OrdinalIgnoreCase)) continue;
            // Price floor / ceiling — only matchable when the listing has a price
            if (a.MinPriceCents.HasValue && (goat.AskingPriceCents ?? 0) < a.MinPriceCents.Value) continue;
            if (a.MaxPriceCents.HasValue && (goat.AskingPriceCents ?? int.MaxValue) > a.MaxPriceCents.Value) continue;
            // State substring match — null-safe on either side
            if (!string.IsNullOrEmpty(a.StateMatch))
            {
                var loc = await _db.Tenants.IgnoreQueryFilters()
                    .Where(t => t.Id == goat.TenantId).Select(t => t.Location).FirstOrDefaultAsync(ct);
                if (string.IsNullOrEmpty(loc)) continue;
                if (loc.IndexOf(a.StateMatch, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }

            try
            {
                var unsub = $"{origin}/alerts/unsubscribe?token={a.UnsubscribeToken}";
                var label = BuildCriteriaLabel(a.BreedSlug, a.Sex, a.MinPriceCents, a.MaxPriceCents, a.StateMatch);
                var (subject, html, text) = EmailTemplates.MarketplaceAlertMatch(
                    label, goat.TenantName, goat.Name, goat.Breed, sexStr,
                    goat.AskingPriceCents, photoUrl, listingUrl, unsub);
                await _email.SendAsync(a.Email, subject, html, text, ct);
                alertIdsToStamp.Add(a.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify saved-search alert {Id}", a.Id);
            }
        }

        if (alertIdsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            await _db.MarketplaceAlerts.IgnoreQueryFilters()
                .Where(a => alertIdsToStamp.Contains(a.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.LastNotifiedAt, now), ct);
        }
    }

    // Builds a human-readable label for the criteria, used in match emails
    // ("New match: Daisy at Cedar Farm matches your search: Nigerian Dwarf does
    // under $500 in TX").
    private static string BuildCriteriaLabel(string? breedSlug, string? sex, int? minPrice, int? maxPrice, string? state)
    {
        var parts = new List<string>();
        parts.Add(string.IsNullOrEmpty(breedSlug) ? "any breed" : breedSlug);
        if (!string.IsNullOrEmpty(sex)) parts.Add(sex.ToLowerInvariant());
        // Format cents → dollars at 2dp so "1234" ¢ shows as "$12.34", not "$12".
        // Using int division silently dropped cents in the alert-match email.
        if (minPrice.HasValue && maxPrice.HasValue)
            parts.Add($"${minPrice.Value / 100m:F2}-${maxPrice.Value / 100m:F2}");
        else if (minPrice.HasValue) parts.Add($"≥ ${minPrice.Value / 100m:F2}");
        else if (maxPrice.HasValue) parts.Add($"under ${maxPrice.Value / 100m:F2}");
        if (!string.IsNullOrEmpty(state)) parts.Add($"in {state}");
        return string.Join(" · ", parts);
    }
}
